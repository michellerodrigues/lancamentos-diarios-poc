using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;

namespace MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;

public interface ISaldoService
{
    /// <summary>O saldo pronto para a tela. A posse da conta ja foi conferida pelo filtro.</summary>
    Task<RespostaApi<SaldoTela>> ObterAsync(string contaId, CancellationToken cancellationToken);

    Task<RespostaApi<IReadOnlyList<string>>> ListarContasAsync(CancellationToken cancellationToken);
}
