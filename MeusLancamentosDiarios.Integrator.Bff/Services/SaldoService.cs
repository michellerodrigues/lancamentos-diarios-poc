using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Services;

public sealed class SaldoService(
    ILancamentosApiClient api,
    IConversor<SaldoResponse, SaldoTela> conversor)
    : ISaldoService
{
    public async Task<RespostaApi<SaldoTela>> ObterAsync(string contaId, CancellationToken cancellationToken)
    {
        var resposta = await api.ObterSaldoAsync(contaId, cancellationToken);
        return resposta.Mapear(conversor.Converter);
    }

    public Task<RespostaApi<IReadOnlyList<string>>> ListarContasAsync(CancellationToken cancellationToken) =>
        api.ListarContasAsync(cancellationToken);
}
