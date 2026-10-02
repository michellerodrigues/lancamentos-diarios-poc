// Bancada de capacidade do relay.
//
// Roda contra o SQL Server e o emulador do Pub/Sub do docker compose, mas num banco
// (LancamentosBench) e num topico (bench-lancamentos) proprios, para o relay e o
// Consumer da demo nao encostarem nas linhas daqui.
//
//   dotnet run -c Release -- atual
//   dotnet run -c Release -- lote <contas> <porConta>
//   dotnet run -c Release -- unitario <n>
//   dotnet run -c Release -- consumidor <n>
//   dotnet run -c Release -- commit
//   dotnet run -c Release -- blocos <maxContas> <porConta> <contas> <lancPorConta> [orcamentoSeg]
//   dotnet run -c Release -- exemplo <maxContas> <porConta>
//
// Sem PUBSUB_EMULATOR_HOST, e com BENCH_PROJECT apontando para um projeto real, a
// mesma bancada mede o Pub/Sub de verdade. BENCH_DB troca o banco (Cloud SQL, RDS).
// Com CriarRecursos=true ela cria o topico bench-lancamentos: no projeto real, a
// credencial precisa de permissao para isso.
//
// Resultados e analise: docs/capacidade-do-relay.md.

using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events;
using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var projeto = Environment.GetEnvironmentVariable("BENCH_PROJECT") ?? "meuslancamentosdiarios-local";
var cs = Environment.GetEnvironmentVariable("BENCH_DB")
         ?? "Server=localhost,1433;Database=LancamentosBench;User Id=sa;Password=Lancamentos@2026;TrustServerCertificate=True;";
if (projeto == "meuslancamentosdiarios-local")
    Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST",
        Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST") ?? "localhost:8085");

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Banco:ConnectionString"] = cs,
    ["Banco:CriarBanco"] = "true",
    ["Banco:TimeoutComandoSegundos"] = "300",
    ["PubSub:ProjectId"] = projeto,
    ["PubSub:Topico"] = "bench-lancamentos",
    ["PubSub:Subscription"] = "bench-consolidacao",
    ["PubSub:CriarRecursos"] = "true",
    ["Relay:TamanhoLote"] = "50",
    ["Relay:MaxTentativas"] = "5",
}).Build();

var sp = new ServiceCollection()
    .AddLogging()
    .AddLancamentosData(config)
    .AddPubSub(config)
    .AddRelay(config)
    .BuildServiceProvider();

await sp.GetRequiredService<DatabaseBootstrapper>().GarantirEsquemaAsync();
await sp.GetRequiredService<PubSubProvisionador>().GarantirRecursosAsync();

var repo = sp.GetRequiredService<ILancamentoRepository>();
var topico = TopicName.FromProjectTopic(projeto, "bench-lancamentos");

// Tempo acumulado por fase dos blocos: reserva, publicacao, marcacao.
var fases = new double[3];

var modo = args.FirstOrDefault() ?? "atual";
switch (modo)
{
    case "atual": await Atual(); break;
    case "lote": await Lote(int.Parse(args[1]), int.Parse(args[2])); break;
    case "unitario": await Unitario(int.Parse(args[1])); break;
    case "consumidor": await Consumidor(int.Parse(args[1])); break;
    case "commit": await Commit(); break;
    case "blocos": await Blocos(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]),
        args.Length > 5 ? double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 35); break;
    case "exemplo": await Exemplo(int.Parse(args[1]), int.Parse(args[2])); break;
    default: Console.WriteLine($"modo desconhecido: {modo}"); break;
}
return;

// ---------------------------------------------------------------------------------
// Modelo de hoje: RelayLancamentos.ExecutarCicloAsync, sem mudar nada.
// ---------------------------------------------------------------------------------
async Task Atual()
{
    var relay = sp.GetRequiredService<RelayLancamentos>();

    // Aquece o cliente do Pub/Sub e a conexao, para nao medir o primeiro handshake.
    await Semear(1, 1); await relay.ExecutarCicloAsync();

    // 1) O maximo que um ciclo de hoje faz: 50 contas com 1 lancamento cada.
    await Semear(50, 1);
    var sw = Stopwatch.StartNew();
    var n = await relay.ExecutarCicloAsync();
    sw.Stop();
    Relatar("atual.ciclo_cheio", new { contas = 50, publicados = n, ms = sw.ElapsedMilliseconds, msPorLancamento = Math.Round(sw.Elapsed.TotalMilliseconds / Math.Max(n, 1), 2) });

    // 2) Uma conta com 60 pendentes: quantos ciclos para esvaziar.
    await Semear(1, 60);
    var ciclos = 0; var total = 0;
    sw.Restart();
    while (true)
    {
        var p = await relay.ExecutarCicloAsync();
        if (p == 0) break;
        ciclos++; total += p;
    }
    sw.Stop();
    Relatar("atual.conta_com_60", new { ciclos, publicados = total, msSomandoOsCiclos = sw.ElapsedMilliseconds });
}

// ---------------------------------------------------------------------------------
// Modelo proposto: reserva tudo de uma vez, publica em lote por conta, marca em lote.
// ---------------------------------------------------------------------------------
async Task Lote(int contas, int porConta)
{
    var publisher = await NovoPublisher();
    await Semear(1, 1); await CicloEmLote(publisher, int.MaxValue, silencioso: true); // aquecimento

    await Semear(contas, porConta);
    await CicloEmLote(publisher, int.MaxValue, silencioso: false, contas, porConta);
    await publisher.ShutdownAsync(TimeSpan.FromSeconds(10));
}

async Task CicloEmLote(PublisherClient publisher, int limite, bool silencioso, int contas = 0, int porConta = 0)
{
    var total = Stopwatch.StartNew();

    // 1. Reserva: todas as linhas elegiveis de todas as contas, num comando so.
    var sw = Stopwatch.StartNew();
    var reservados = await ReservarEmLote(limite);
    var msReserva = sw.ElapsedMilliseconds;

    // 2. Publicacao: dentro da conta, na ordem da sequencia e sem esperar uma pela
    // outra; o cliente preserva a ordem por ordering key. Contas em paralelo.
    sw.Restart();
    var envios = new List<(Guid Id, Task<string> Tarefa)>(reservados.Count);
    foreach (var conta in reservados.GroupBy(l => l.ContaId))
        foreach (var l in conta.OrderBy(l => l.Sequencia))
            envios.Add((l.Id, publisher.PublishAsync(Mensagem(l))));
    await Task.WhenAll(envios.Select(e => e.Tarefa));
    var msPublicacao = sw.ElapsedMilliseconds;

    // 3. Marcacao: Lido -> Enfileirado em lote, em blocos de 2.000.
    sw.Restart();
    foreach (var bloco in envios.Chunk(2000))
        await MarcarEmLote(bloco.Select(e => (e.Id, e.Tarefa.Result)));
    var msMarcacao = sw.ElapsedMilliseconds;

    total.Stop();
    if (silencioso) return;

    var n = reservados.Count;
    Relatar("lote", new
    {
        contas, porConta, lancamentos = n,
        msReserva, msPublicacao, msMarcacao, msTotal = total.ElapsedMilliseconds,
        lancamentosPorSegundo = Math.Round(n / total.Elapsed.TotalSeconds),
        usPorLancamento = new
        {
            reserva = Math.Round(msReserva * 1000.0 / n, 1),
            publicacao = Math.Round(msPublicacao * 1000.0 / n, 1),
            marcacao = Math.Round(msMarcacao * 1000.0 / n, 1),
        },
        conferencia = await Contar()
    });
}

async Task<List<LancamentoEntity>> ReservarEmLote(int limite)
{
    // Igual a consulta de hoje, com uma diferenca: um anterior pendente que tambem
    // e elegivel nao bloqueia mais. Bloqueia so o que nao pode sair agora (Erro,
    // Lido de outra instancia, Cadastrado em backoff). Assim a conta sai inteira,
    // na ordem, em vez de um lancamento por ciclo.
    //
    // Posicao + TOP: se o limite cortar, cada conta leva um prefixo da sua fila, e
    // as contas avancam por igual.
    const string sql = """
        WITH elegiveis AS (
            SELECT l.*, ROW_NUMBER() OVER (PARTITION BY l.ContaId ORDER BY l.Sequencia) AS Posicao
            FROM dbo.LancamentosDiarios AS l WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE (l.Stage = 1 OR (l.Stage = 2 AND l.StageAtualizadoEm < @ReservaVencida))
              AND (l.ProximaTentativaEm IS NULL OR l.ProximaTentativaEm <= @Agora)
              AND NOT EXISTS (
                    SELECT 1 FROM dbo.LancamentosDiarios AS anterior
                    WHERE anterior.ContaId = l.ContaId
                      AND anterior.Sequencia < l.Sequencia
                      AND (anterior.Stage = 6
                           OR (anterior.Stage = 2 AND anterior.StageAtualizadoEm >= @ReservaVencida)
                           OR (anterior.Stage = 1 AND anterior.ProximaTentativaEm > @Agora)))
        ),
        candidatos AS (
            SELECT TOP (@Limite) * FROM elegiveis ORDER BY Posicao, ContaId
        )
        UPDATE candidatos SET Stage = 2, StageAtualizadoEm = @Agora
        OUTPUT inserted.Id, inserted.ContaId, inserted.Sequencia, inserted.Tipo, inserted.ValorCentavos,
               inserted.DataLancamento, inserted.DataRegistro, inserted.Observacao, inserted.Stage,
               inserted.StageAtualizadoEm, inserted.TentativasEnvio, inserted.TentativasProcessamento,
               inserted.ProximaTentativaEm, inserted.MensagemId, inserted.Erro;
        """;

    var agora = DateTimeOffset.UtcNow;
    await using var cn = new SqlConnection(cs);
    var linhas = await cn.QueryAsync<LancamentoEntity>(new CommandDefinition(sql, new
    {
        Limite = limite,
        Agora = agora,
        ReservaVencida = agora - TimeSpan.FromMinutes(1)
    }, commandTimeout: 300));
    return linhas.ToList();
}

async Task MarcarEmLote(IEnumerable<(Guid Id, string MensagemId)> itens)
{
    const string sql = """
        UPDATE l
        SET Stage = 3, StageAtualizadoEm = @Agora, MensagemId = j.MensagemId,
            TentativasEnvio = l.TentativasEnvio + 1, ProximaTentativaEm = NULL, Erro = NULL
        FROM dbo.LancamentosDiarios AS l
        JOIN OPENJSON(@Json) WITH (Id uniqueidentifier '$.i', MensagemId varchar(100) '$.m') AS j
          ON j.Id = l.Id
        WHERE l.Stage = 2;
        """;

    var json = JsonSerializer.Serialize(itens.Select(i => new { i = i.Id, m = i.MensagemId }));
    await using var cn = new SqlConnection(cs);
    await cn.ExecuteAsync(new CommandDefinition(sql, new { Agora = DateTimeOffset.UtcNow, Json = json }, commandTimeout: 300));
}

// ---------------------------------------------------------------------------------
// Blocos com dois limites: no maximo X contas por bloco e N lancamentos por conta.
// A rodada repete blocos ate esvaziar ou ate estourar o orcamento de tempo.
// ---------------------------------------------------------------------------------
async Task Blocos(int maxContas, int porConta, int contas, int lancPorConta, double orcamentoSeg)
{
    var publisher = await NovoPublisher();
    await Semear(1, 1); await Bloco(publisher, maxContas, porConta); // aquecimento

    await Semear(contas, lancPorConta);
    Array.Clear(fases);
    var sw = Stopwatch.StartNew();
    int blocos = 0, total = 0;
    while (sw.Elapsed.TotalSeconds < orcamentoSeg)
    {
        var n = await Bloco(publisher, maxContas, porConta);
        if (n == 0) break;
        blocos++; total += n;
    }
    sw.Stop();

    Relatar("blocos", new
    {
        maxContas, porConta, contas, lancPorConta, orcamentoSeg,
        blocos, publicados = total, restantes = contas * lancPorConta - total,
        ms = sw.ElapsedMilliseconds,
        msPorBloco = Math.Round(sw.Elapsed.TotalMilliseconds / Math.Max(blocos, 1), 1),
        msPorBlocoPorFase = new
        {
            reserva = Math.Round(fases[0] / Math.Max(blocos, 1), 1),
            publicacao = Math.Round(fases[1] / Math.Max(blocos, 1), 1),
            marcacao = Math.Round(fases[2] / Math.Max(blocos, 1), 1)
        },
        lancamentosPorSegundo = Math.Round(total / sw.Elapsed.TotalSeconds)
    });
    await publisher.ShutdownAsync(TimeSpan.FromSeconds(10));
}

// O exemplo da conversa: A com 60, B com 30, C com 1. Mostra o que sai em cada bloco.
async Task Exemplo(int maxContas, int porConta)
{
    var publisher = await NovoPublisher();
    await SemearContas([("A", 60), ("B", 30), ("C", 1)]);
    for (var bloco = 1; ; bloco++)
    {
        var saiu = await Bloco(publisher, maxContas, porConta, detalhar: true);
        if (saiu == 0) break;
    }
    await publisher.ShutdownAsync(TimeSpan.FromSeconds(5));
}

async Task<int> Bloco(PublisherClient publisher, int maxContas, int porConta, bool detalhar = false)
{
    var fase = Stopwatch.StartNew();
    var reservados = await ReservarBloco(maxContas, porConta);
    fases[0] += fase.Elapsed.TotalMilliseconds;
    if (reservados.Count == 0) return 0;
    fase.Restart();

    var envios = new List<(Guid Id, Task<string> Tarefa)>(reservados.Count);
    foreach (var conta in reservados.GroupBy(l => l.ContaId))
        foreach (var l in conta.OrderBy(l => l.Sequencia))
            envios.Add((l.Id, publisher.PublishAsync(Mensagem(l))));
    await Task.WhenAll(envios.Select(e => e.Tarefa));
    fases[1] += fase.Elapsed.TotalMilliseconds;
    fase.Restart();
    await MarcarEmLote(envios.Select(e => (e.Id, e.Tarefa.Result)));
    fases[2] += fase.Elapsed.TotalMilliseconds;

    if (detalhar)
        Console.WriteLine("bloco " + string.Join("  ", reservados.GroupBy(l => l.ContaId).OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: #{g.Min(l => l.Sequencia)} a #{g.Max(l => l.Sequencia)}")));
    return reservados.Count;
}

async Task<List<LancamentoEntity>> ReservarBloco(int maxContas, int porConta)
{
    // Mesma regra de elegibilidade do ReservarEmLote. Por cima dela, dois limites:
    // as contas entram pela mais antiga esperando (a cabeca da fila de cada uma), e
    // cada conta leva no maximo o comeco da sua fila, na ordem da sequencia.
    const string sql = """
        WITH elegiveis AS (
            SELECT l.*, ROW_NUMBER() OVER (PARTITION BY l.ContaId ORDER BY l.Sequencia) AS Posicao
            FROM dbo.LancamentosDiarios AS l WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE (l.Stage = 1 OR (l.Stage = 2 AND l.StageAtualizadoEm < @ReservaVencida))
              AND (l.ProximaTentativaEm IS NULL OR l.ProximaTentativaEm <= @Agora)
              AND NOT EXISTS (
                    SELECT 1 FROM dbo.LancamentosDiarios AS anterior
                    WHERE anterior.ContaId = l.ContaId
                      AND anterior.Sequencia < l.Sequencia
                      AND (anterior.Stage = 6
                           OR (anterior.Stage = 2 AND anterior.StageAtualizadoEm >= @ReservaVencida)
                           OR (anterior.Stage = 1 AND anterior.ProximaTentativaEm > @Agora)))
        ),
        contas AS (
            SELECT TOP (@MaxContas) ContaId FROM elegiveis WHERE Posicao = 1 ORDER BY DataRegistro, ContaId
        ),
        candidatos AS (
            SELECT * FROM elegiveis
            WHERE Posicao <= @PorConta AND ContaId IN (SELECT ContaId FROM contas)
        )
        UPDATE candidatos SET Stage = 2, StageAtualizadoEm = @Agora
        OUTPUT inserted.Id, inserted.ContaId, inserted.Sequencia, inserted.Tipo, inserted.ValorCentavos,
               inserted.DataLancamento, inserted.DataRegistro, inserted.Observacao, inserted.Stage,
               inserted.StageAtualizadoEm, inserted.TentativasEnvio, inserted.TentativasProcessamento,
               inserted.ProximaTentativaEm, inserted.MensagemId, inserted.Erro;
        """;

    var agora = DateTimeOffset.UtcNow;
    await using var cn = new SqlConnection(cs);
    var linhas = await cn.QueryAsync<LancamentoEntity>(new CommandDefinition(sql, new
    {
        MaxContas = maxContas, PorConta = porConta, Agora = agora,
        ReservaVencida = agora - TimeSpan.FromMinutes(1)
    }, commandTimeout: 300));
    return linhas.ToList();
}

// ---------------------------------------------------------------------------------
// Custo de uma ida por vez: publicar esperando cada uma e marcar linha a linha.
// E o que o ExecutarCicloAsync de hoje faz para cada lancamento do lote.
// ---------------------------------------------------------------------------------
async Task Unitario(int n)
{
    var publisher = await NovoPublisher();
    await Semear(n, 1);
    var reservados = await ReservarEmLote(int.MaxValue);
    await publisher.PublishAsync(Mensagem(reservados[0])); // aquecimento

    var sw = Stopwatch.StartNew();
    var ids = new List<(Guid, string)>();
    foreach (var l in reservados) ids.Add((l.Id, await publisher.PublishAsync(Mensagem(l))));
    var msPub = sw.Elapsed.TotalMilliseconds;

    sw.Restart();
    foreach (var (id, m) in ids) await repo.MarcarEnfileiradoAsync(id, m);
    var msMarca = sw.Elapsed.TotalMilliseconds;

    Relatar("unitario", new
    {
        lancamentos = n,
        msPorPublicacao = Math.Round(msPub / n, 2),
        msPorMarcacao = Math.Round(msMarca / n, 2),
        bytesPorMensagem = Mensagem(reservados[0]).CalculateSize()
    });
    await publisher.ShutdownAsync(TimeSpan.FromSeconds(5));
}

// ---------------------------------------------------------------------------------
// Consumidor, dentro de UMA conta: em serie, porque e assim que o broker entrega.
// Mede so o banco (IniciarProcessamento + Consolidar), sem o transporte.
// ---------------------------------------------------------------------------------
async Task Consumidor(int n)
{
    var consolidador = sp.GetRequiredService<ILancamentoRepository>();
    await Semear(1, n + 1);
    await using (var cn = new SqlConnection(cs))
        await cn.ExecuteAsync("UPDATE dbo.LancamentosDiarios SET Stage = 3");

    var ids = (await new SqlConnection(cs).QueryAsync<Guid>(
        "SELECT Id FROM dbo.LancamentosDiarios ORDER BY Sequencia")).ToList();

    // aquecimento
    var primeiro = await consolidador.IniciarProcessamentoAsync(ids[0]);
    await consolidador.ConsolidarAsync(primeiro!);

    var sw = Stopwatch.StartNew();
    foreach (var id in ids.Skip(1))
    {
        var l = await consolidador.IniciarProcessamentoAsync(id);
        await consolidador.ConsolidarAsync(l!);
    }
    sw.Stop();
    Relatar("consumidor", new
    {
        lancamentos = n,
        msPorLancamento = Math.Round(sw.Elapsed.TotalMilliseconds / n, 2),
        lancamentosPorSegundoPorConta = Math.Round(n / sw.Elapsed.TotalSeconds)
    });
}

// Ida e volta pura (SELECT 1) contra um UPDATE de uma linha com commit proprio:
// separa a latencia de rede do custo de gravar o log de transacao.
async Task Commit()
{
    await Semear(1, 1);
    await using var cn = new SqlConnection(cs);
    await cn.OpenAsync();
    var id = await cn.ExecuteScalarAsync<Guid>("SELECT TOP 1 Id FROM dbo.LancamentosDiarios");
    for (var i = 0; i < 20; i++) await cn.ExecuteScalarAsync<int>("SELECT 1");
    var sw = Stopwatch.StartNew();
    for (var i = 0; i < 300; i++) await cn.ExecuteScalarAsync<int>("SELECT 1");
    var msSelect = sw.Elapsed.TotalMilliseconds / 300;
    sw.Restart();
    for (var i = 0; i < 300; i++) await cn.ExecuteAsync("UPDATE dbo.LancamentosDiarios SET TentativasEnvio = TentativasEnvio + 1 WHERE Id = @id", new { id });
    var msUpdate = sw.Elapsed.TotalMilliseconds / 300;
    Relatar("commit", new { msIdaEVolta = Math.Round(msSelect, 2), msUpdateComCommit = Math.Round(msUpdate, 2) });
}

// ---------------------------------------------------------------------------------

async Task<PublisherClient> NovoPublisher() => await new PublisherClientBuilder
{
    TopicName = topico,
    EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
    Settings = new PublisherClient.Settings { EnableMessageOrdering = true }
}.BuildAsync();

PubsubMessage Mensagem(LancamentoEntity l)
{
    // Mesmo formato do PubSubPublicador: corpo JSON do evento + atributos.
    var evento = new LancamentoRegistradoEvent
    {
        LancamentoId = l.Id, ContaId = l.ContaId, Sequencia = l.Sequencia, Tipo = l.Tipo,
        ValorCentavos = l.ValorCentavos, DataLancamento = l.DataLancamento,
        DataRegistro = l.DataRegistro, Observacao = l.Observacao
    };
    return new PubsubMessage
    {
        Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(evento, EventosJson.Options)),
        OrderingKey = l.ContaId,
        Attributes =
        {
            ["lancamentoId"] = l.Id.ToString("D"), ["contaId"] = l.ContaId,
            ["sequencia"] = l.Sequencia.ToString(), ["tipo"] = l.Tipo.ToString()
        }
    };
}

Task Semear(int contas, int porConta) =>
    SemearContas(Enumerable.Range(0, contas).Select(c => ($"C{c:D7}", porConta)).ToArray());

async Task SemearContas((string Conta, int Quantos)[] contas)
{
    await using var cn = new SqlConnection(cs);
    await cn.OpenAsync();
    await cn.ExecuteAsync("TRUNCATE TABLE dbo.LancamentosDiarios; TRUNCATE TABLE dbo.SaldoConsolidado;");

    var t = new DataTable();
    foreach (var (nome, tipo) in new (string, Type)[]
             {
                 ("Id", typeof(Guid)), ("ContaId", typeof(string)), ("Sequencia", typeof(long)), ("Tipo", typeof(int)),
                 ("ValorCentavos", typeof(long)), ("DataLancamento", typeof(DateTime)), ("DataRegistro", typeof(DateTimeOffset)),
                 ("Observacao", typeof(string)), ("Stage", typeof(int)), ("StageAtualizadoEm", typeof(DateTimeOffset)),
                 ("TentativasEnvio", typeof(int)), ("TentativasProcessamento", typeof(int))
             })
        t.Columns.Add(nome, tipo);

    var agora = DateTimeOffset.UtcNow.AddMinutes(-5);
    foreach (var (conta, porConta) in contas)
    {
        for (var s = 1; s <= porConta; s++)
            t.Rows.Add(Guid.CreateVersion7(), conta, (long)s, s % 3 == 0 ? 2 : 1, 1000L + s,
                DateTime.Today, agora, "bancada de capacidade", 1, agora, 0, 0);
    }

    using var bulk = new SqlBulkCopy(cn) { DestinationTableName = "dbo.LancamentosDiarios", BulkCopyTimeout = 300 };
    foreach (DataColumn col in t.Columns) bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
    await bulk.WriteToServerAsync(t);
}

async Task<object> Contar()
{
    await using var cn = new SqlConnection(cs);
    var porStage = await cn.QueryAsync<(int Stage, int Qtd)>(
        "SELECT Stage, COUNT(*) FROM dbo.LancamentosDiarios GROUP BY Stage");
    return porStage.ToDictionary(x => ((StageLancamentoEnum)x.Stage).ToString(), x => x.Qtd);
}

void Relatar(string nome, object dados) =>
    Console.WriteLine($"{nome} {JsonSerializer.Serialize(dados)}");
