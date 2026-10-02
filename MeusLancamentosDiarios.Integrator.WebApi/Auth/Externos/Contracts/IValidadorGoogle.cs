namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;

public interface IValidadorGoogle
{
    /// <summary>Null quando a credencial nao e valida para este Client ID.</summary>
    Task<IdentidadeGoogle?> ValidarAsync(string credencial, CancellationToken cancellationToken = default);
}
