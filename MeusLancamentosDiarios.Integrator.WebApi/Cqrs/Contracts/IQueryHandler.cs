using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

public interface IQueryHandler<in TQuery, TResultado>
    where TQuery : IQuery<TResultado>
{
    Task<TResultado> HandleAsync(TQuery consulta, CancellationToken cancellationToken);
}
