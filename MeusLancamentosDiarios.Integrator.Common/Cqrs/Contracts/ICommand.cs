namespace MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

/// <summary>
/// Intencao de escrita. O parametro de tipo e o que o handler devolve.
///
/// Fica no Common para o comando do Messages ser a propria mensagem: o BFF
/// serializa, a WebApi recebe e despacha. Quem trata continua dentro da WebApi.
/// </summary>
public interface ICommand<TResultado>;
