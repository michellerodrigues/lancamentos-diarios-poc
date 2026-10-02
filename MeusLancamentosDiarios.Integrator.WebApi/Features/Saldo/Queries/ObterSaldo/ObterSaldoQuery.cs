using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ObterSaldo;

public sealed record ObterSaldoQuery : IQuery<SaldoResponse>
{
    /// <summary>Conta cujo saldo sera lido. O saldo e sempre por conta.</summary>
    public required string ContaId { get; init; }

    /// <summary>
    /// Quantos lancamentos pendentes detalhar na resposta. Nulo usa o
    /// Saldo:LimitePendentesPadrao do appsettings.
    /// </summary>
    public int? LimitePendentes { get; init; }
}
