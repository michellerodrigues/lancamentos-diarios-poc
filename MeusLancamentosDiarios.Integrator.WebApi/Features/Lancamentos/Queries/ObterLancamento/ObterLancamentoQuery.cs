using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Queries.ObterLancamento;

/// <summary>Um lancamento pelo id. Null quando nao existe.</summary>
public sealed record ObterLancamentoQuery : IQuery<LancamentoDetalheResponse?>
{
    public required Guid Id { get; init; }
}
