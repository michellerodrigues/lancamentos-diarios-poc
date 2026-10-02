namespace MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

/// <summary>Intencao de leitura. O parametro de tipo e o que o handler devolve.</summary>
public interface IQuery<TResultado>;
