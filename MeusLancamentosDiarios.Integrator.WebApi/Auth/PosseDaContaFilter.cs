using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Common.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

/// <summary>
/// A regra de posse, fora dos endpoints: o Cliente so alcanca a propria conta, o
/// Admin alcanca qualquer uma. A role ja foi conferida pela policy; isto confere
/// sobre qual conta.
///
/// Olha a conta em tres lugares: o parametro de rota <c>contaId</c>, os argumentos
/// que implementam <see cref="IReferenciaConta"/> (o corpo) e, na saida, o recurso
/// devolvido, para o que so revela a conta depois de lido (um lancamento pelo id).
/// O BFF tem uma copia: a regra (PodeAcessarConta) vem do Common, mas o filtro
/// depende do ASP.NET Core, e o Common fica sem ele para servir tambem aos workers.
/// </summary>
public sealed class PosseDaContaFilter : IEndpointFilter
{
    public const string ParametroDeRota = "contaId";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        var usuario = contexto.HttpContext.User;

        if (ContasPedidas(contexto).Any(conta => !usuario.PodeAcessarConta(conta)))
        {
            return Results.Forbid();
        }

        var resultado = await proximo(contexto);

        if (resultado is IValueHttpResult { Value: IReferenciaConta { ContaId: { } lida } }
            && !string.IsNullOrWhiteSpace(lida)
            && !usuario.PodeAcessarConta(lida))
        {
            return Results.Forbid();
        }

        return resultado;
    }

    private static IEnumerable<string> ContasPedidas(EndpointFilterInvocationContext contexto)
    {
        if (contexto.HttpContext.Request.RouteValues.TryGetValue(ParametroDeRota, out var daRota)
            && daRota is string conta
            && !string.IsNullOrWhiteSpace(conta))
        {
            yield return conta;
        }

        foreach (var argumento in contexto.Arguments)
        {
            // Conta vazia passa: o validador responde 400 com a mensagem certa.
            if (argumento is IReferenciaConta { ContaId: { } doCorpo } && !string.IsNullOrWhiteSpace(doCorpo))
            {
                yield return doCorpo;
            }
        }
    }
}
