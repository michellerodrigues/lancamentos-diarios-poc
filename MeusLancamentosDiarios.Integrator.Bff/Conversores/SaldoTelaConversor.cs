using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Helpers;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Conversores;

/// <summary>O saldo da WebApi vira tudo que a tela de Saldo Consolidado precisa, num payload so.</summary>
public sealed class SaldoTelaConversor(IConversor<LancamentoPendenteResponse, PendenteTela> pendentes)
    : IConversor<SaldoResponse, SaldoTela>
{
    /// <summary>Cadencia sugerida ao front enquanto ha lancamento em transito.</summary>
    public const int IntervaloPollingMs = 2_000;

    public SaldoTela Converter(SaldoResponse saldo) => new()
    {
        ContaId = saldo.ContaId,
        SaldoConsolidado = saldo.SaldoConsolidado,
        SaldoConsolidadoFormatado = LancamentoHelper.Moeda(saldo.SaldoConsolidado),
        AtualizadoEm = saldo.AtualizadoEm,
        UltimaAtualizacaoFormatada = LancamentoHelper.DataHora(saldo.AtualizadoEm),
        UltimaSequenciaConsolidada = saldo.UltimaSequenciaConsolidada,
        SaldoProjetado = saldo.SaldoProjetado,
        SaldoProjetadoFormatado = LancamentoHelper.Moeda(saldo.SaldoProjetado),
        CreditosPendentes = saldo.CreditosPendentes,
        CreditosPendentesFormatado = LancamentoHelper.Moeda(saldo.CreditosPendentes),
        DebitosPendentes = saldo.DebitosPendentes,
        DebitosPendentesFormatado = LancamentoHelper.Moeda(saldo.DebitosPendentes),
        ConsolidacaoEmAndamento = saldo.ConsolidacaoEmAndamento,

        // Zero desliga o polling no front assim que nada mais esta em transito.
        IntervaloPollingMs = saldo.ConsolidacaoEmAndamento ? IntervaloPollingMs : 0,

        Pendentes = saldo.Pendentes.Select(pendentes.Converter).ToList()
    };
}
