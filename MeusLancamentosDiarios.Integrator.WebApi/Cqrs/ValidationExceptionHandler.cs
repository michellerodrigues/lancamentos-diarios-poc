using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs;

/// <summary>
/// Converte a falha do FluentValidation em 400 com ValidationProblemDetails,
/// no mesmo formato que o ASP.NET Core usa para erros de binding.
/// </summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validacao) return false;

        var erros = validacao.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problema = new ValidationProblemDetails(erros)
        {
            Status = StatusCodes.Status400BadRequest,
            // Generico: o mesmo handler atende lancamento, lote e autenticacao.
            Title = "Dados invalidos.",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problema, cancellationToken);

        return true;
    }
}
