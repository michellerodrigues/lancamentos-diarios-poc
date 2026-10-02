using System.Text.Json;
using MeusLancamentosDiarios.Integrator.Bff.Clients;

namespace MeusLancamentosDiarios.Integrator.Bff.Endpoints;

/// <summary>
/// Onde a resposta do servico vira HTTP. O sucesso vira o resultado que o endpoint
/// pede; a falha repassa o status e o ProblemDetails da WebApi como vieram.
/// </summary>
public static class ResultadosHttp
{
    public static IResult ParaResultado<T>(this RespostaApi<T> resposta, Func<T, IResult> sucesso) =>
        resposta.Sucesso ? sucesso(resposta.Valor!) : Falha(resposta.StatusCode, resposta.Problema);

    public static IResult ParaResultado<T>(this RespostaApi<T> resposta) =>
        resposta.ParaResultado(valor => Results.Ok(valor));

    public static IResult ParaResultado(this RespostaRepassada resposta) =>
        resposta.Corpo is null
            ? Results.StatusCode(resposta.StatusCode)
            : Results.Text(resposta.Corpo, resposta.ContentType ?? "application/json", statusCode: resposta.StatusCode);

    private static IResult Falha(int status, JsonElement? problema) =>
        problema is null ? Results.StatusCode(status) : Results.Json(problema, statusCode: status);
}
