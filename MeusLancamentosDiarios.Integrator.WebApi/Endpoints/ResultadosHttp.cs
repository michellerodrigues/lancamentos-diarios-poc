namespace MeusLancamentosDiarios.Integrator.WebApi.Endpoints;

public static class ResultadosHttp
{
    /// <summary>A consulta que pode nao achar nada: null vira 404.</summary>
    public static IResult OkOuNaoEncontrado<T>(T? valor) where T : class =>
        valor is null ? Results.NotFound() : Results.Ok(valor);
}
