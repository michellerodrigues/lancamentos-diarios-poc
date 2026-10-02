using System.Data;
using Dapper;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using Microsoft.Data.SqlClient;

namespace MeusLancamentosDiarios.Integrator.Events.Data;

public sealed class LancamentoRepository(IDbConnectionFactory factory) : ILancamentoRepository
{
    /// <summary>Estagios que ainda nao chegaram ao fim do fluxo — o que as telas chamam de "pendente".</summary>
    private static readonly int[] EstagiosPendentes =
    [
        (int)StageLancamentoEnum.Cadastrado,
        (int)StageLancamentoEnum.Lido,
        (int)StageLancamentoEnum.Enfileirado,
        (int)StageLancamentoEnum.EmProcessamento
    ];

    /// <summary>
    /// Estagios que bloqueiam um sucessor. Erro entra na lista de proposito: uma linha
    /// parada em Erro nao pode ser ultrapassada em silencio, senao a conta consolidaria
    /// fora de ordem sem ninguem perceber. Ela trava a conta ate alguem resolver.
    /// </summary>
    private static readonly int[] EstagiosBloqueantes =
    [
        (int)StageLancamentoEnum.Cadastrado,
        (int)StageLancamentoEnum.Lido,
        (int)StageLancamentoEnum.Erro
    ];

    /// <summary>Corridas perdidas no indice unico de (ContaId, Sequencia) antes de desistir.</summary>
    private const int TentativasSequencia = 5;

    /// <summary>2627 = violacao de constraint unica; 2601 = chave duplicada em indice unico.</summary>
    private static readonly int[] ErrosDeChaveDuplicada = [2601, 2627];

    /// <summary>Tamanho da coluna Erro. Mensagem de excecao nao tem limite; a coluna tem.</summary>
    private const int TamanhoMaximoErro = 1000;

    private const string Colunas =
        "Id, ContaId, Sequencia, Tipo, ValorCentavos, DataLancamento, DataRegistro, Observacao, " +
        "Stage, StageAtualizadoEm, TentativasEnvio, TentativasProcessamento, ProximaTentativaEm, MensagemId, Erro";

    /// <summary>As mesmas colunas prefixadas com inserted., para a clausula OUTPUT.</summary>
    private const string ColunasInseridas =
        "inserted.Id, inserted.ContaId, inserted.Sequencia, inserted.Tipo, inserted.ValorCentavos, " +
        "inserted.DataLancamento, inserted.DataRegistro, inserted.Observacao, inserted.Stage, " +
        "inserted.StageAtualizadoEm, inserted.TentativasEnvio, inserted.TentativasProcessamento, " +
        "inserted.ProximaTentativaEm, inserted.MensagemId, inserted.Erro";

    private sealed record SaldoRow(long SaldoCentavos, long UltimaSequencia, DateTimeOffset AtualizadoEm);

    private sealed record PendentesRow(long Creditos, long Debitos, int Quantidade);

    private CommandDefinition Comando(
        string sql,
        object? parametros,
        CancellationToken cancellationToken,
        IDbTransaction? transacao = null) =>
        new(sql, parametros, transacao,
            commandTimeout: factory.TimeoutComandoSegundos,
            cancellationToken: cancellationToken);

    private const string SqlProximaSequencia = """
        SELECT COALESCE(MAX(Sequencia), 0) + 1
        FROM dbo.LancamentosDiarios WITH (UPDLOCK, HOLDLOCK)
        WHERE ContaId = @ContaId;
        """;

    private const string SqlInserir = """
        INSERT INTO dbo.LancamentosDiarios
            (Id, ContaId, Sequencia, Tipo, ValorCentavos, DataLancamento, DataRegistro,
             Observacao, Stage, StageAtualizadoEm, TentativasEnvio, TentativasProcessamento)
        VALUES
            (@Id, @ContaId, @Sequencia, @Tipo, @ValorCentavos, @DataLancamento, @DataRegistro,
             @Observacao, @Stage, @StageAtualizadoEm, 0, 0);
        """;

    private static object ParametrosDeInsercao(LancamentoEntity lancamento, long sequencia) => new
    {
        lancamento.Id,
        lancamento.ContaId,
        Sequencia = sequencia,
        Tipo = (int)lancamento.Tipo,
        lancamento.ValorCentavos,
        lancamento.DataLancamento,
        lancamento.DataRegistro,
        lancamento.Observacao,
        Stage = (int)StageLancamentoEnum.Cadastrado,
        StageAtualizadoEm = DateTimeOffset.UtcNow
    };

    public async Task<long> InserirAsync(LancamentoEntity lancamento, CancellationToken cancellationToken = default)
    {
        await using var conexao = await factory.AbrirAsync(cancellationToken);

        for (var tentativa = 1; ; tentativa++)
        {
            // UPDLOCK + HOLDLOCK no MAX segura a faixa ate o commit, o que ja evita a
            // corrida na maior parte dos casos. O indice unico e a garantia final.
            await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);

            var sequencia = await conexao.ExecuteScalarAsync<long>(Comando(
                SqlProximaSequencia, new { lancamento.ContaId }, cancellationToken, transacao));

            try
            {
                await conexao.ExecuteAsync(Comando(
                    SqlInserir, ParametrosDeInsercao(lancamento, sequencia), cancellationToken, transacao));

                await transacao.CommitAsync(cancellationToken);

                lancamento.Sequencia = sequencia;
                return sequencia;
            }
            catch (SqlException ex)
                when (ErrosDeChaveDuplicada.Contains(ex.Number) && tentativa < TentativasSequencia)
            {
                // Outra insercao levou esta sequencia. Recalcula e tenta de novo.
                await transacao.RollbackAsync(cancellationToken);
            }
        }
    }

    public async Task InserirLoteAsync(
        IReadOnlyList<LancamentoEntity> lancamentos, CancellationToken cancellationToken = default)
    {
        // As contas sao travadas sempre na mesma ordem. Dois lotes concorrentes que
        // tocam as mesmas contas em ordens diferentes entrariam em deadlock.
        var contas = lancamentos
            .Select(l => l.ContaId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        for (var tentativa = 1; ; tentativa++)
        {
            await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);

            var proxima = new Dictionary<string, long>(StringComparer.Ordinal);

            foreach (var conta in contas)
            {
                proxima[conta] = await conexao.ExecuteScalarAsync<long>(Comando(
                    SqlProximaSequencia, new { ContaId = conta }, cancellationToken, transacao));
            }

            // Dentro de cada conta, a sequencia segue a ordem da lista.
            var sequencias = new long[lancamentos.Count];
            var linhas = new object[lancamentos.Count];

            for (var i = 0; i < lancamentos.Count; i++)
            {
                sequencias[i] = proxima[lancamentos[i].ContaId]++;
                linhas[i] = ParametrosDeInsercao(lancamentos[i], sequencias[i]);
            }

            try
            {
                // Uma linha por execucao, todas na mesma transacao: o lote entra
                // inteiro ou nao entra.
                await conexao.ExecuteAsync(Comando(SqlInserir, linhas, cancellationToken, transacao));
                await transacao.CommitAsync(cancellationToken);

                for (var i = 0; i < lancamentos.Count; i++)
                {
                    lancamentos[i].Sequencia = sequencias[i];
                }

                return;
            }
            catch (SqlException ex)
                when (ErrosDeChaveDuplicada.Contains(ex.Number) && tentativa < TentativasSequencia)
            {
                await transacao.RollbackAsync(cancellationToken);
            }
        }
    }

    public async Task<bool> SemPredecessorPendenteAsync(
        string contaId, long sequencia, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.LancamentosDiarios
                WHERE ContaId = @ContaId
                  AND Sequencia < @Sequencia
                  AND Stage IN @Bloqueantes
            ) THEN 0 ELSE 1 END;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        return await conexao.ExecuteScalarAsync<bool>(Comando(sql, new
        {
            ContaId = contaId,
            Sequencia = sequencia,
            Bloqueantes = EstagiosBloqueantes
        }, cancellationToken));
    }

    public async Task<bool> ReservarUmAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = @Lido, StageAtualizadoEm = @Agora
            WHERE Id = @Id AND Stage = @Cadastrado;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        var afetadas = await conexao.ExecuteAsync(Comando(sql, new
        {
            Lido = (int)StageLancamentoEnum.Lido,
            Agora = DateTimeOffset.UtcNow,
            Id = id,
            Cadastrado = (int)StageLancamentoEnum.Cadastrado
        }, cancellationToken));

        // Zero linhas: o relay chegou primeiro nesta mesma linha.
        return afetadas == 1;
    }

    public async Task<IReadOnlyList<LancamentoEntity>> ReservarParaPublicacaoAsync(
        int limite, TimeSpan reservaExpiraEm, CancellationToken cancellationToken = default)
    {
        // Selecionar e reservar num comando so, com OUTPUT devolvendo as linhas ja
        // reservadas. Nao ha janela entre ler e marcar.
        //
        // READPAST faz instancias concorrentes do relay pularem linhas que outra ja
        // travou, em vez de esperarem por elas; UPDLOCK garante que o que esta
        // instancia leu e o que ela vai atualizar.
        //
        // As tres condicoes:
        //
        // 1. Cadastrado, ou preso em Lido tempo demais — reserva de quem nao concluiu.
        //    Sem a segunda parte a linha sumiria do fluxo: nenhuma outra consulta
        //    olha para Lido.
        // 2. Fora do backoff: uma falha recente nao e tentada de novo na hora.
        // 3. NOT EXISTS de predecessor bloqueante: nunca publica a sequencia 2 de uma
        //    conta enquanto a 1 nao saiu. O Pub/Sub ordena o que recebe; o buraco tem
        //    de ser evitado aqui, porque la ele seria invisivel.
        const string sql = $"""
            WITH candidatos AS (
                SELECT TOP (@Limite) l.*
                FROM dbo.LancamentosDiarios AS l WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE (
                        l.Stage = @Cadastrado
                     OR (l.Stage = @Lido AND l.StageAtualizadoEm < @ReservaVencida)
                      )
                  AND (l.ProximaTentativaEm IS NULL OR l.ProximaTentativaEm <= @Agora)
                  AND NOT EXISTS (
                        SELECT 1 FROM dbo.LancamentosDiarios AS anterior
                        WHERE anterior.ContaId = l.ContaId
                          AND anterior.Sequencia < l.Sequencia
                          AND anterior.Stage IN @Bloqueantes
                      )
                ORDER BY l.ContaId, l.Sequencia
            )
            UPDATE candidatos
            SET Stage = @Lido, StageAtualizadoEm = @Agora
            OUTPUT {ColunasInseridas};
            """;

        var agora = DateTimeOffset.UtcNow;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        var reservados = await conexao.QueryAsync<LancamentoEntity>(Comando(sql, new
        {
            Limite = limite,
            Cadastrado = (int)StageLancamentoEnum.Cadastrado,
            Lido = (int)StageLancamentoEnum.Lido,
            ReservaVencida = agora - reservaExpiraEm,
            Agora = agora,
            Bloqueantes = EstagiosBloqueantes
        }, cancellationToken));

        return reservados.ToList();
    }

    public async Task MarcarEnfileiradoAsync(
        Guid id, string mensagemId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = @Enfileirado,
                StageAtualizadoEm = @Agora,
                MensagemId = @MensagemId,
                TentativasEnvio = TentativasEnvio + 1,
                ProximaTentativaEm = NULL,
                Erro = NULL
            WHERE Id = @Id AND Stage = @Lido;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            Enfileirado = (int)StageLancamentoEnum.Enfileirado,
            Agora = DateTimeOffset.UtcNow,
            MensagemId = mensagemId,
            Id = id,
            Lido = (int)StageLancamentoEnum.Lido
        }, cancellationToken));
    }

    public async Task LiberarReservaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = @Cadastrado, StageAtualizadoEm = @Agora
            WHERE Id = @Id AND Stage = @Lido;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            Cadastrado = (int)StageLancamentoEnum.Cadastrado,
            Agora = DateTimeOffset.UtcNow,
            Id = id,
            Lido = (int)StageLancamentoEnum.Lido
        }, cancellationToken));
    }

    public async Task DevolverParaFilaAsync(
        Guid id,
        string motivo,
        int maxTentativas,
        DateTimeOffset proximaTentativaEm,
        CancellationToken cancellationToken = default)
    {
        // Volta para Cadastrado para uma proxima tentativa; ao estourar o limite a
        // linha para em Erro — e, por ser estagio bloqueante, trava os sucessores da
        // mesma conta ate alguem intervir.
        //
        // ProximaTentativaEm segura a retentativa: sem ela, uma indisponibilidade de
        // dez segundos do broker queima as cinco tentativas e para o lancamento em
        // Erro, derrubando a conta inteira por uma falha passageira.
        const string sql = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = CASE WHEN TentativasEnvio + 1 >= @MaxTentativas THEN @Erro ELSE @Cadastrado END,
                StageAtualizadoEm = @Agora,
                TentativasEnvio = TentativasEnvio + 1,
                ProximaTentativaEm = @ProximaTentativaEm,
                Erro = @Motivo
            WHERE Id = @Id AND Stage = @Lido;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            MaxTentativas = maxTentativas,
            Erro = (int)StageLancamentoEnum.Erro,
            Cadastrado = (int)StageLancamentoEnum.Cadastrado,
            Agora = DateTimeOffset.UtcNow,
            ProximaTentativaEm = proximaTentativaEm,
            Motivo = Truncar(motivo),
            Id = id,
            Lido = (int)StageLancamentoEnum.Lido
        }, cancellationToken));
    }

    public async Task<LancamentoEntity?> IniciarProcessamentoAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Aceita Lido alem de Enfileirado: o Pub/Sub pode entregar a mensagem antes
        // de o publicador ter gravado a transicao para Enfileirado.
        //
        // OUTPUT devolve a linha reservada no mesmo comando — sem ida e volta extra
        // e sem janela entre marcar e ler.
        const string sql = $"""
            UPDATE dbo.LancamentosDiarios
            SET Stage = @EmProcessamento,
                StageAtualizadoEm = @Agora,
                TentativasProcessamento = TentativasProcessamento + 1
            OUTPUT {ColunasInseridas}
            WHERE Id = @Id AND Stage IN (@Lido, @Enfileirado);
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        // Sem linha: entrega duplicada ou lancamento ja finalizado.
        return await conexao.QuerySingleOrDefaultAsync<LancamentoEntity>(Comando(sql, new
        {
            EmProcessamento = (int)StageLancamentoEnum.EmProcessamento,
            Agora = DateTimeOffset.UtcNow,
            Id = id,
            Lido = (int)StageLancamentoEnum.Lido,
            Enfileirado = (int)StageLancamentoEnum.Enfileirado
        }, cancellationToken));
    }

    public async Task<bool> ConsolidarAsync(
        LancamentoEntity lancamento, CancellationToken cancellationToken = default)
    {
        const string sqlFinalizar = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = @Consolidado, StageAtualizadoEm = @Agora, Erro = NULL
            WHERE Id = @Id AND Stage = @EmProcessamento;
            """;

        // UPDATE e, se nao existia linha, INSERT. Preferido ao MERGE, que tem
        // historico de problemas de concorrencia no SQL Server.
        const string sqlAplicarSaldo = """
            UPDATE dbo.SaldoConsolidado WITH (UPDLOCK, SERIALIZABLE)
            SET SaldoCentavos   = SaldoCentavos + @Delta,
                UltimaSequencia = @Sequencia,
                AtualizadoEm    = @Agora
            WHERE ContaId = @ContaId;

            IF @@ROWCOUNT = 0
                INSERT INTO dbo.SaldoConsolidado (ContaId, SaldoCentavos, UltimaSequencia, AtualizadoEm)
                VALUES (@ContaId, @Delta, @Sequencia, @Agora);
            """;

        var agora = DateTimeOffset.UtcNow;

        await using var conexao = await factory.AbrirAsync(cancellationToken);
        await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);

        // Marcar antes de somar: se a linha ja nao esta em EmProcessamento, o valor
        // nao entra no saldo — e o que impede a soma dobrada numa entrega repetida.
        var afetadas = await conexao.ExecuteAsync(Comando(sqlFinalizar, new
        {
            Consolidado = (int)StageLancamentoEnum.Consolidado,
            Agora = agora,
            lancamento.Id,
            EmProcessamento = (int)StageLancamentoEnum.EmProcessamento
        }, cancellationToken, transacao));

        if (afetadas == 0)
        {
            await transacao.RollbackAsync(cancellationToken);
            return false;
        }

        await conexao.ExecuteAsync(Comando(sqlAplicarSaldo, new
        {
            lancamento.ContaId,
            Delta = lancamento.DeltaCentavos,
            lancamento.Sequencia,
            Agora = agora
        }, cancellationToken, transacao));

        await transacao.CommitAsync(cancellationToken);
        return true;
    }

    public async Task DevolverParaProcessamentoAsync(
        Guid id, string motivo, int maxTentativas, CancellationToken cancellationToken = default)
    {
        // Volta para Enfileirado para que a reentrega do Pub/Sub encontre a linha
        // num estagio que IniciarProcessamentoAsync aceita. Sem isso, a linha ficaria
        // presa em EmProcessamento e toda reentrega seria descartada como duplicata.
        const string sql = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = CASE WHEN TentativasProcessamento >= @MaxTentativas THEN @Erro ELSE @Enfileirado END,
                StageAtualizadoEm = @Agora,
                Erro = @Motivo
            WHERE Id = @Id AND Stage = @EmProcessamento;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            MaxTentativas = maxTentativas,
            Erro = (int)StageLancamentoEnum.Erro,
            Enfileirado = (int)StageLancamentoEnum.Enfileirado,
            Agora = DateTimeOffset.UtcNow,
            Motivo = Truncar(motivo),
            Id = id,
            EmProcessamento = (int)StageLancamentoEnum.EmProcessamento
        }, cancellationToken));
    }

    public async Task MarcarErroAsync(Guid id, string motivo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.LancamentosDiarios
            SET Stage = @Erro, StageAtualizadoEm = @Agora, Erro = @Motivo
            WHERE Id = @Id;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            Erro = (int)StageLancamentoEnum.Erro,
            Agora = DateTimeOffset.UtcNow,
            Motivo = Truncar(motivo),
            Id = id
        }, cancellationToken));
    }

    public async Task<IReadOnlyList<LancamentoEntity>> ListarPendentesAsync(
        string contaId, int limite, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT TOP (@Limite) {Colunas}
            FROM dbo.LancamentosDiarios
            WHERE ContaId = @ContaId AND Stage IN @Estagios
            ORDER BY Sequencia DESC;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        var pendentes = await conexao.QueryAsync<LancamentoEntity>(Comando(sql, new
        {
            ContaId = contaId,
            Estagios = EstagiosPendentes,
            Limite = limite
        }, cancellationToken));

        return pendentes.ToList();
    }

    public async Task<SaldoAtual> ObterSaldoAsync(
        string contaId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SaldoCentavos, UltimaSequencia, AtualizadoEm
            FROM dbo.SaldoConsolidado
            WHERE ContaId = @ContaId;

            SELECT
                COALESCE(SUM(CASE WHEN Tipo = @Credito THEN ValorCentavos ELSE 0 END), 0) AS Creditos,
                COALESCE(SUM(CASE WHEN Tipo = @Debito  THEN ValorCentavos ELSE 0 END), 0) AS Debitos,
                COUNT(*) AS Quantidade
            FROM dbo.LancamentosDiarios
            WHERE ContaId = @ContaId AND Stage IN @Estagios;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        using var grade = await conexao.QueryMultipleAsync(Comando(sql, new
        {
            ContaId = contaId,
            Credito = (int)TipoLancamentoEnum.Credito,
            Debito = (int)TipoLancamentoEnum.Debito,
            Estagios = EstagiosPendentes
        }, cancellationToken));

        // Null quando a conta ainda nao consolidou nada — nao ha linha no read model.
        var consolidado = await grade.ReadSingleOrDefaultAsync<SaldoRow>();
        var pendentes = await grade.ReadSingleAsync<PendentesRow>();

        return new SaldoAtual
        {
            ContaId = contaId,
            SaldoConsolidadoCentavos = consolidado?.SaldoCentavos ?? 0,
            UltimaSequenciaConsolidada = consolidado?.UltimaSequencia ?? 0,
            AtualizadoEm = consolidado?.AtualizadoEm,
            CreditosPendentesCentavos = pendentes.Creditos,
            DebitosPendentesCentavos = pendentes.Debitos,
            QuantidadePendentes = pendentes.Quantidade
        };
    }

    public async Task<IReadOnlyList<string>> ListarContasAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DISTINCT ContaId FROM dbo.LancamentosDiarios ORDER BY ContaId;";

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        var contas = await conexao.QueryAsync<string>(Comando(sql, null, cancellationToken));

        return contas.ToList();
    }

    public async Task<LancamentoEntity?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {Colunas} FROM dbo.LancamentosDiarios WHERE Id = @Id;";

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        return await conexao.QuerySingleOrDefaultAsync<LancamentoEntity>(
            Comando(sql, new { Id = id }, cancellationToken));
    }

    /// <summary>A coluna Erro tem tamanho fixo; mensagem de excecao nao tem.</summary>
    private static string Truncar(string texto) =>
        texto.Length <= TamanhoMaximoErro ? texto : texto[..TamanhoMaximoErro];
}
