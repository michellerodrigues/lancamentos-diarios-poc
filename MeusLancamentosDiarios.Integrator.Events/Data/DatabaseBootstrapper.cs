using Dapper;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Events.Data;

/// <summary>
/// Cria banco e esquema na primeira execução. Sem EF Core não há migrations, então o
/// script é idempotente e pode rodar na subida dos três processos ao mesmo tempo.
/// </summary>
public sealed class DatabaseBootstrapper(
    IDbConnectionFactory factory,
    IOptions<BancoOptions> options,
    ILogger<DatabaseBootstrapper> logger)
{
    private readonly BancoOptions _opcoes = options.Value;

    private const string Esquema = """
        IF OBJECT_ID(N'dbo.LancamentosDiarios', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.LancamentosDiarios (
                Id                      uniqueidentifier NOT NULL
                                        CONSTRAINT PK_LancamentosDiarios PRIMARY KEY NONCLUSTERED,
                ContaId                 varchar(20)      NOT NULL,
                Sequencia               bigint           NOT NULL,
                Tipo                    int              NOT NULL,
                ValorCentavos           bigint           NOT NULL,
                DataLancamento          date             NOT NULL,
                DataRegistro            datetimeoffset(7) NOT NULL,
                Observacao              nvarchar(200)    NULL,
                Stage                   int              NOT NULL,
                StageAtualizadoEm       datetimeoffset(7) NOT NULL,
                TentativasEnvio         int              NOT NULL CONSTRAINT DF_Lanc_TentEnvio DEFAULT (0),
                TentativasProcessamento int              NOT NULL CONSTRAINT DF_Lanc_TentProc  DEFAULT (0),
                ProximaTentativaEm      datetimeoffset(7) NULL,
                MensagemId              varchar(100)     NULL,
                Erro                    nvarchar(1000)   NULL
            );
        END;

        -- Não existem duas sequências iguais na mesma conta. A sequência é calculada
        -- como MAX + 1 dentro da transação de inserção; este índice é o que transforma
        -- uma corrida entre dois inserts simultâneos em erro, em vez de duplicata.
        --
        -- CLUSTERED de propósito: quase toda consulta do fluxo percorre a conta em
        -- ordem de sequência, e é essa a ordem física que interessa.
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'UX_LancamentosDiarios_Conta_Sequencia'
              AND object_id = OBJECT_ID(N'dbo.LancamentosDiarios'))
        BEGIN
            CREATE UNIQUE CLUSTERED INDEX UX_LancamentosDiarios_Conta_Sequencia
                ON dbo.LancamentosDiarios (ContaId, Sequencia);
        END;

        -- Serve a varredura do relay: pega por estágio, na ordem da conta.
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'IX_LancamentosDiarios_Stage'
              AND object_id = OBJECT_ID(N'dbo.LancamentosDiarios'))
        BEGIN
            CREATE INDEX IX_LancamentosDiarios_Stage
                ON dbo.LancamentosDiarios (Stage, ContaId, Sequencia)
                INCLUDE (ProximaTentativaEm, StageAtualizadoEm);
        END;

        -- Read model, uma linha por conta.
        IF OBJECT_ID(N'dbo.SaldoConsolidado', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SaldoConsolidado (
                ContaId         varchar(20)      NOT NULL
                                CONSTRAINT PK_SaldoConsolidado PRIMARY KEY,
                SaldoCentavos   bigint           NOT NULL CONSTRAINT DF_Saldo_Centavos DEFAULT (0),
                UltimaSequencia bigint           NOT NULL CONSTRAINT DF_Saldo_Sequencia DEFAULT (0),
                AtualizadoEm    datetimeoffset(7) NOT NULL
            );
        END;
        """;

    public async Task GarantirEsquemaAsync(CancellationToken cancellationToken = default)
    {
        if (_opcoes.CriarBanco)
        {
            await GarantirBancoAsync(cancellationToken);
        }

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(new CommandDefinition(
            Esquema,
            commandTimeout: factory.TimeoutComandoSegundos,
            cancellationToken: cancellationToken));

        logger.LogInformation("Esquema garantido em {Banco}", factory.Descricao);
    }

    /// <summary>
    /// CREATE DATABASE não roda dentro de transação e não aceita parâmetro no nome,
    /// então o nome é interpolado — vindo da connection string, não de entrada externa.
    /// </summary>
    private async Task GarantirBancoAsync(CancellationToken cancellationToken)
    {
        var banco = _opcoes.NomeDoBanco;

        if (string.IsNullOrWhiteSpace(banco))
        {
            throw new InvalidOperationException(
                "A connection string precisa nomear o banco (Database=...).");
        }

        await using var servidor = await factory.AbrirNoServidorAsync(cancellationToken);

        var sql = $"""
            IF DB_ID(@Banco) IS NULL
                EXEC('CREATE DATABASE [{banco.Replace("]", "]]")}]');
            """;

        await servidor.ExecuteAsync(new CommandDefinition(
            sql,
            new { Banco = banco },
            commandTimeout: factory.TimeoutComandoSegundos,
            cancellationToken: cancellationToken));
    }
}
