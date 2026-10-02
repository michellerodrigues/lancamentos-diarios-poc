using Dapper;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using Microsoft.Data.SqlClient;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

public sealed class UsuarioRepository(IDbConnectionFactory factory, TimeProvider relogio) : IUsuarioRepository
{
    private const string Colunas = "Id, Email, Nome, SenhaHash, GoogleId, ContaId, Role, CriadoEm";

    /// <summary>2627 = violacao de constraint unica; 2601 = chave duplicada em indice unico.</summary>
    private static readonly int[] ErrosDeChaveDuplicada = [2601, 2627];

    private sealed record TokenRow(Guid UsuarioId, DateTimeOffset? ConsumidoEm);

    private CommandDefinition Comando(string sql, object? parametros, CancellationToken cancellationToken) =>
        new(sql, parametros,
            commandTimeout: factory.TimeoutComandoSegundos,
            cancellationToken: cancellationToken);

    public Task<UsuarioEntity?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ObterAsync($"SELECT {Colunas} FROM dbo.Usuarios WHERE Id = @Valor;", id, cancellationToken);

    public Task<UsuarioEntity?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default) =>
        ObterAsync($"SELECT {Colunas} FROM dbo.Usuarios WHERE Email = @Valor;", email, cancellationToken);

    public Task<UsuarioEntity?> ObterPorGoogleIdAsync(string googleId, CancellationToken cancellationToken = default) =>
        ObterAsync($"SELECT {Colunas} FROM dbo.Usuarios WHERE GoogleId = @Valor;", googleId, cancellationToken);

    private async Task<UsuarioEntity?> ObterAsync(string sql, object valor, CancellationToken cancellationToken)
    {
        await using var conexao = await factory.AbrirAsync(cancellationToken);

        return await conexao.QuerySingleOrDefaultAsync<UsuarioEntity>(
            Comando(sql, new { Valor = valor }, cancellationToken));
    }

    public async Task<ResultadoInsercaoUsuarioEnum> InserirAsync(
        UsuarioEntity usuario, CancellationToken cancellationToken = default)
    {
        // INSERT ... SELECT com NOT EXISTS: a conta precisa estar livre tambem na
        // tabela de lancamentos. Uma conta sem dono mas com historico nao e de ninguem
        // novo. Os indices unicos cobrem a corrida entre dois cadastros simultaneos.
        const string sql = """
            INSERT INTO dbo.Usuarios (Id, Email, Nome, SenhaHash, GoogleId, ContaId, Role, CriadoEm)
            SELECT @Id, @Email, @Nome, @SenhaHash, @GoogleId, @ContaId, @Role, @CriadoEm
            WHERE NOT EXISTS (SELECT 1 FROM dbo.LancamentosDiarios WHERE ContaId = @ContaId);
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        try
        {
            var afetadas = await conexao.ExecuteAsync(Comando(sql, usuario, cancellationToken));

            return afetadas == 1 ? ResultadoInsercaoUsuarioEnum.Inserido : ResultadoInsercaoUsuarioEnum.ContaEmUso;
        }
        catch (SqlException ex) when (ErrosDeChaveDuplicada.Contains(ex.Number))
        {
            // O nome da constraint vem na mensagem; e o que diz qual campo colidiu.
            if (ex.Message.Contains("UX_Usuarios_Email", StringComparison.Ordinal))
                return ResultadoInsercaoUsuarioEnum.EmailEmUso;

            if (ex.Message.Contains("UX_Usuarios_GoogleId", StringComparison.Ordinal))
                return ResultadoInsercaoUsuarioEnum.GoogleIdEmUso;

            return ResultadoInsercaoUsuarioEnum.ContaEmUso;
        }
    }

    public async Task VincularGoogleAsync(
        Guid usuarioId, string googleId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.Usuarios SET GoogleId = @GoogleId WHERE Id = @Id AND GoogleId IS NULL;";

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new { Id = usuarioId, GoogleId = googleId }, cancellationToken));
    }

    public async Task AtualizarSenhaAsync(
        Guid usuarioId, string senhaHash, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.Usuarios SET SenhaHash = @SenhaHash WHERE Id = @Id;";

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new { Id = usuarioId, SenhaHash = senhaHash }, cancellationToken));
    }

    public async Task GravarTokenAsync(
        Guid usuarioId,
        FinalidadeTokenEnum finalidade,
        byte[] hash,
        DateTimeOffset expiraEm,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.TokensDeUsuario (Id, UsuarioId, Finalidade, TokenHash, CriadoEm, ExpiraEm)
            VALUES (@Id, @UsuarioId, @Finalidade, @TokenHash, @CriadoEm, @ExpiraEm);
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            Finalidade = (byte)finalidade,
            TokenHash = hash,
            CriadoEm = relogio.GetUtcNow(),
            ExpiraEm = expiraEm
        }, cancellationToken));
    }

    public async Task<ConsumoDeToken> ConsumirTokenAsync(
        byte[] hash, FinalidadeTokenEnum finalidade, CancellationToken cancellationToken = default)
    {
        // Marcar e ler num comando so, com OUTPUT: sem janela entre conferir e usar.
        const string sqlConsumir = """
            UPDATE dbo.TokensDeUsuario
            SET ConsumidoEm = @Agora
            OUTPUT inserted.UsuarioId
            WHERE TokenHash = @Hash
              AND Finalidade = @Finalidade
              AND ConsumidoEm IS NULL
              AND ExpiraEm > @Agora;
            """;

        // Nada consumido: descobre se o token existia e ja tinha sido usado.
        const string sqlSituacao = """
            SELECT UsuarioId, ConsumidoEm
            FROM dbo.TokensDeUsuario
            WHERE TokenHash = @Hash AND Finalidade = @Finalidade;
            """;

        var parametros = new { Hash = hash, Finalidade = (byte)finalidade, Agora = relogio.GetUtcNow() };

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        var dono = await conexao.QuerySingleOrDefaultAsync<Guid?>(
            Comando(sqlConsumir, parametros, cancellationToken));

        if (dono is not null) return new ConsumoDeToken(SituacaoTokenEnum.Valido, dono);

        var token = await conexao.QuerySingleOrDefaultAsync<TokenRow>(
            Comando(sqlSituacao, parametros, cancellationToken));

        return token is { ConsumidoEm: not null }
            ? new ConsumoDeToken(SituacaoTokenEnum.JaConsumido, token.UsuarioId)
            : new ConsumoDeToken(SituacaoTokenEnum.Invalido, null);
    }

    public async Task RevogarTokensAsync(
        Guid usuarioId, FinalidadeTokenEnum finalidade, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.TokensDeUsuario
            SET ConsumidoEm = @Agora
            WHERE UsuarioId = @UsuarioId AND Finalidade = @Finalidade AND ConsumidoEm IS NULL;
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(Comando(sql, new
        {
            UsuarioId = usuarioId,
            Finalidade = (byte)finalidade,
            Agora = relogio.GetUtcNow()
        }, cancellationToken));
    }
}
