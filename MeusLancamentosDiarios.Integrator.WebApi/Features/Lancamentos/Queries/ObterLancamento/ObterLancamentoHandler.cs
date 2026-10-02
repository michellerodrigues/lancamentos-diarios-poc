using MeusLancamentosDiarios.Integrator.Common.Helpers;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Queries.ObterLancamento;

public sealed class ObterLancamentoHandler(ILancamentoRepository repositorio)
    : IQueryHandler<ObterLancamentoQuery, LancamentoDetalheResponse?>
{
    public async Task<LancamentoDetalheResponse?> HandleAsync(
        ObterLancamentoQuery consulta, CancellationToken cancellationToken)
    {
        var lancamento = await repositorio.ObterPorIdAsync(consulta.Id, cancellationToken);

        return lancamento is null ? null : new LancamentoDetalheResponse
        {
            Id = lancamento.Id,
            ContaId = lancamento.ContaId,
            Sequencia = lancamento.Sequencia,
            Tipo = lancamento.Tipo,
            Valor = DinheiroHelper.ParaReais(lancamento.ValorCentavos),
            DataLancamento = lancamento.DataLancamento,
            DataRegistro = lancamento.DataRegistro,
            Observacao = lancamento.Observacao,
            Stage = lancamento.Stage,
            StageAtualizadoEm = lancamento.StageAtualizadoEm,
            TentativasEnvio = lancamento.TentativasEnvio,
            TentativasProcessamento = lancamento.TentativasProcessamento,
            Erro = lancamento.Erro
        };
    }
}
