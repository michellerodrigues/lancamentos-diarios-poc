using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;


namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ListarContas;

public sealed class ListarContasHandler(ILancamentoRepository repositorio)
    : IQueryHandler<ListarContasQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> HandleAsync(ListarContasQuery consulta, CancellationToken cancellationToken) =>
        repositorio.ListarContasAsync(cancellationToken);
}
