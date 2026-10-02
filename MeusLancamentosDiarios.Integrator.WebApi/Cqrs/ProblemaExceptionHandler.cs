using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs;

public sealed class ProblemaExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ProblemaException problema) return false;

        httpContext.Response.StatusCode = problema.Status;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = problema.Status,
            Title = problema.Message,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
