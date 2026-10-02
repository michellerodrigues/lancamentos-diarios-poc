using System.Reflection;
using FluentValidation;
using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs;

/// <summary>
/// Resolve o handler pelo tipo do comando/consulta e roda a validacao antes.
///
/// Um dispatcher proprio em vez do MediatR: sao poucas linhas, evita a dependencia
/// e nao arrasta a mudanca de licenciamento do MediatR para dentro do projeto.
/// </summary>
public sealed class Dispatcher(IServiceProvider provider) : IDispatcher
{
    public Task<TResultado> EnviarAsync<TResultado>(
        ICommand<TResultado> comando, CancellationToken cancellationToken = default) =>
        DespacharAsync<TResultado>(comando, typeof(ICommandHandler<,>), cancellationToken);

    public Task<TResultado> ConsultarAsync<TResultado>(
        IQuery<TResultado> consulta, CancellationToken cancellationToken = default) =>
        DespacharAsync<TResultado>(consulta, typeof(IQueryHandler<,>), cancellationToken);

    private async Task<TResultado> DespacharAsync<TResultado>(
        object mensagem, Type handlerAberto, CancellationToken cancellationToken)
    {
        await ValidarAsync(mensagem, cancellationToken);

        var tipoHandler = handlerAberto.MakeGenericType(mensagem.GetType(), typeof(TResultado));
        var handler = provider.GetService(tipoHandler)
            ?? throw new InvalidOperationException(
                $"Nenhum handler registrado para {mensagem.GetType().Name}.");

        var metodo = tipoHandler.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)!;

        try
        {
            return await (Task<TResultado>)metodo.Invoke(handler, [mensagem, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Sem isso, a excecao real do handler chegaria embrulhada e o
            // middleware de erro nao conseguiria classificar o status.
            throw ex.InnerException;
        }
    }

    private async Task ValidarAsync(object mensagem, CancellationToken cancellationToken)
    {
        var tipoValidador = typeof(IValidator<>).MakeGenericType(mensagem.GetType());

        if (provider.GetService(tipoValidador) is not IValidator validador) return;

        var contexto = new ValidationContext<object>(mensagem);
        var resultado = await validador.ValidateAsync(contexto, cancellationToken);

        if (!resultado.IsValid)
        {
            throw new ValidationException(resultado.Errors);
        }
    }
}
