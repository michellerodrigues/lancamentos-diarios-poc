using MeusLancamentosDiarios.Integrator.Common.Helpers;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ObterSaldo;

public sealed class ObterSaldoHandler(ILancamentoRepository repositorio, IOptions<SaldoOptions> opcoes)
    : IQueryHandler<ObterSaldoQuery, SaldoResponse>
{
    public async Task<SaldoResponse> HandleAsync(
        ObterSaldoQuery consulta, CancellationToken cancellationToken)
    {
        var conta = consulta.ContaId.Trim().ToUpperInvariant();

        var saldo = await repositorio.ObterSaldoAsync(conta, cancellationToken);
        var pendentes = await repositorio.ListarPendentesAsync(
            conta, consulta.LimitePendentes ?? opcoes.Value.LimitePendentesPadrao, cancellationToken);

        return new SaldoResponse
        {
            ContaId = conta,
            SaldoConsolidado = DinheiroHelper.ParaReais(saldo.SaldoConsolidadoCentavos),
            UltimaSequenciaConsolidada = saldo.UltimaSequenciaConsolidada,
            AtualizadoEm = saldo.AtualizadoEm,
            CreditosPendentes = DinheiroHelper.ParaReais(saldo.CreditosPendentesCentavos),
            DebitosPendentes = DinheiroHelper.ParaReais(saldo.DebitosPendentesCentavos),
            SaldoProjetado = DinheiroHelper.ParaReais(saldo.SaldoProjetadoCentavos),
            ConsolidacaoEmAndamento = saldo.ConsolidacaoEmAndamento,
            Pendentes = pendentes.Select(p => new LancamentoPendenteResponse
            {
                Id = p.Id,
                Sequencia = p.Sequencia,
                Tipo = p.Tipo,
                Valor = DinheiroHelper.ParaReais(p.ValorCentavos),
                DataLancamento = p.DataLancamento,
                DataRegistro = p.DataRegistro,
                Observacao = p.Observacao,
                Stage = p.Stage
            }).ToList()
        };
    }
}
