namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;

/// <summary>
/// Usuarios e os tokens opacos deles. Os tokens sao guardados so como hash SHA-256:
/// quem le o banco nao consegue usar o que esta la.
/// </summary>
public interface IUsuarioRepository
{
    Task<UsuarioEntity?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>O e-mail chega normalizado (<see cref="UsuarioEntity.NormalizarEmail"/>).</summary>
    Task<UsuarioEntity?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UsuarioEntity?> ObterPorGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recusa com <see cref="ResultadoInsercaoUsuarioEnum.ContaEmUso"/> tambem quando a conta
    /// nao tem dono mas ja tem lancamento: o usuario novo herdaria o extrato de outra pessoa.
    /// </summary>
    Task<ResultadoInsercaoUsuarioEnum> InserirAsync(UsuarioEntity usuario, CancellationToken cancellationToken = default);

    Task VincularGoogleAsync(Guid usuarioId, string googleId, CancellationToken cancellationToken = default);

    Task AtualizarSenhaAsync(Guid usuarioId, string senhaHash, CancellationToken cancellationToken = default);

    Task GravarTokenAsync(
        Guid usuarioId,
        FinalidadeTokenEnum finalidade,
        byte[] hash,
        DateTimeOffset expiraEm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca o token como usado e devolve o dono, num comando so: dois pedidos com o
    /// mesmo token nao conseguem consumi-lo os dois.
    /// </summary>
    Task<ConsumoDeToken> ConsumirTokenAsync(
        byte[] hash, FinalidadeTokenEnum finalidade, CancellationToken cancellationToken = default);

    /// <summary>Invalida todos os tokens ainda nao usados do usuario com esta finalidade.</summary>
    Task RevogarTokensAsync(
        Guid usuarioId, FinalidadeTokenEnum finalidade, CancellationToken cancellationToken = default);
}
