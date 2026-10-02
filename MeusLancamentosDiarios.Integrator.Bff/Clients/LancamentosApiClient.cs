using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Clients;

public sealed class LancamentosApiClient(HttpClient http, JsonSerializerOptions json) : ILancamentosApiClient
{
    public async Task<RespostaApi<SaldoResponse>> ObterSaldoAsync(
        string contaId, CancellationToken cancellationToken)
    {
        using var resposta = await http.GetAsync(
            $"/saldo/{Uri.EscapeDataString(contaId)}", cancellationToken);

        return await LerAsync<SaldoResponse>(resposta, cancellationToken);
    }

    public async Task<RespostaApi<IReadOnlyList<string>>> ListarContasAsync(
        CancellationToken cancellationToken)
    {
        using var resposta = await http.GetAsync("/contas", cancellationToken);
        return await LerAsync<IReadOnlyList<string>>(resposta, cancellationToken);
    }

    public async Task<RespostaApi<CriarLancamentoResponse>> CriarLancamentoAsync(
        CriarLancamentoCommand comando, CancellationToken cancellationToken)
    {
        using var resposta = await http.PostAsJsonAsync("/lancamentos", comando, json, cancellationToken);
        return await LerAsync<CriarLancamentoResponse>(resposta, cancellationToken);
    }

    public async Task<RespostaRepassada> RepassarAsync(
        HttpMethod metodo, string caminho, object? corpo, CancellationToken cancellationToken)
    {
        using var pedido = new HttpRequestMessage(metodo, caminho);

        if (corpo is not null)
        {
            pedido.Content = JsonContent.Create(corpo, corpo.GetType(), options: json);
        }

        using var resposta = await http.SendAsync(pedido, cancellationToken);

        var texto = await resposta.Content.ReadAsStringAsync(cancellationToken);

        return new RespostaRepassada(
            (int)resposta.StatusCode,
            string.IsNullOrEmpty(texto) ? null : texto,
            resposta.Content.Headers.ContentType?.ToString());
    }

    private async Task<RespostaApi<T>> LerAsync<T>(
        HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        if (resposta.IsSuccessStatusCode)
        {
            var valor = await resposta.Content.ReadFromJsonAsync<T>(json, cancellationToken);

            return valor is null
                ? RespostaApi<T>.Falha((int)HttpStatusCode.BadGateway, null)
                : RespostaApi<T>.Ok(valor);
        }

        // Repassa o ProblemDetails da WebApi intacto: as mensagens do
        // FluentValidation chegam ao usuario sem tradução no meio do caminho.
        JsonElement? problema = null;

        try
        {
            problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(json, cancellationToken);
        }
        catch (JsonException)
        {
            // Corpo nao-JSON (ex.: erro de proxy): so o status importa.
        }

        return RespostaApi<T>.Falha((int)resposta.StatusCode, problema);
    }
}
