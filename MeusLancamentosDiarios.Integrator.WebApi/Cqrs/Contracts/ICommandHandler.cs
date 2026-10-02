using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

public interface ICommandHandler<in TCommand, TResultado>
    where TCommand : ICommand<TResultado>
{
    Task<TResultado> HandleAsync(TCommand comando, CancellationToken cancellationToken);
}
