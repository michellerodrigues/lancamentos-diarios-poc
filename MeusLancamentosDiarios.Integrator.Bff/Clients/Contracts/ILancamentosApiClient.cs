using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;

/// <summary>
/// A unica porta do BFF para a WebApi. Fala nos contratos do projeto Messages e leva
/// o Bearer do usuario junto.
/// </summary>
public interface ILancamentosApiClient
{
    Task<RespostaApi<SaldoResponse>> ObterSaldoAsync(string contaId, CancellationToken cancellationToken);

    Task<RespostaApi<IReadOnlyList<string>>> ListarContasAsync(CancellationToken cancellationToken);

    Task<RespostaApi<CriarLancamentoResponse>> CriarLancamentoAsync(
        CriarLancamentoCommand comando, CancellationToken cancellationToken);

    /// <summary>
    /// Repasse sem transformacao, para o que o BFF nao formata (autenticacao): o
    /// status e o corpo da WebApi chegam ao front como sairam de la.
    /// </summary>
    Task<RespostaRepassada> RepassarAsync(
        HttpMethod metodo, string caminho, object? corpo, CancellationToken cancellationToken);
}
