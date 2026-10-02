using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

/// <summary>
/// Ponto unico por onde endpoints despacham comandos e consultas.
/// Os endpoints nunca conhecem o handler concreto.
/// </summary>
public interface IDispatcher
{
    Task<TResultado> EnviarAsync<TResultado>(ICommand<TResultado> comando, CancellationToken cancellationToken = default);

    Task<TResultado> ConsultarAsync<TResultado>(IQuery<TResultado> consulta, CancellationToken cancellationToken = default);
}
