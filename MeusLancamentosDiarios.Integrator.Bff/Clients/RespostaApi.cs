using System.Net;
using System.Text.Json;

namespace MeusLancamentosDiarios.Integrator.Bff.Clients;

/// <summary>Resposta da WebApi preservando o status e o corpo de erro para repasse ao front.</summary>
public sealed record RespostaApi<T>(bool Sucesso, T? Valor, int StatusCode, JsonElement? Problema)
{
    public static RespostaApi<T> Ok(T valor) => new(true, valor, (int)HttpStatusCode.OK, null);

    public static RespostaApi<T> Falha(int statusCode, JsonElement? problema) =>
        new(false, default, statusCode, problema);

    /// <summary>Converte o valor do sucesso; a falha passa adiante como veio.</summary>
    public RespostaApi<TDestino> Mapear<TDestino>(Func<T, TDestino> converter) =>
        Sucesso
            ? RespostaApi<TDestino>.Ok(converter(Valor!))
            : RespostaApi<TDestino>.Falha(StatusCode, Problema);
}
