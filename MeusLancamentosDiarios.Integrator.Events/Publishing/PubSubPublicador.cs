using System.Text.Json;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Events.Publishing;

/// <summary>
/// Publica no Google Cloud Pub/Sub. EmulatorDetection faz o cliente usar o
/// emulador quando PUBSUB_EMULATOR_HOST esta definida, e o Pub/Sub real quando nao.
/// </summary>
public sealed class PubSubPublicador(IOptions<PubSubOptions> options) : IPublicadorEventos, IAsyncDisposable
{
    private readonly PubSubOptions _opcoes = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private PublisherClient? _cliente;

    public async Task<string> PublicarAsync(
        LancamentoRegistradoEvent evento, CancellationToken cancellationToken = default)
    {
        var cliente = await ObterClienteAsync(cancellationToken);
        var corpo = JsonSerializer.Serialize(evento, EventosJson.Options);

        var mensagem = new PubsubMessage
        {
            Data = Google.Protobuf.ByteString.CopyFromUtf8(corpo),

            // A ordering key e a CONTA, nao o lancamento. Ordenacao so existe entre
            // mensagens que compartilham a chave: uma chave por mensagem desligaria a
            // ordenacao sem aviso. Contas diferentes seguem em paralelo.
            OrderingKey = evento.ContaId,

            Attributes =
            {
                // Permite ao consumidor filtrar e rastrear sem abrir o corpo.
                ["lancamentoId"] = evento.LancamentoId.ToString("D"),
                ["contaId"] = evento.ContaId,
                ["sequencia"] = evento.Sequencia.ToString(),
                ["tipo"] = evento.Tipo.ToString()
            }
        };

        try
        {
            return await cliente.PublishAsync(mensagem);
        }
        catch
        {
            // Falhando uma publicacao com ordering key, o cliente coloca A CHAVE INTEIRA
            // em estado de erro e passa a recusar tudo dela com "Cannot publish due to
            // error state in ordering" — inclusive as proximas tentativas deste mesmo
            // lancamento. E deliberado: e assim que ele evita entregar fora de ordem.
            //
            // ResumePublish limpa esse estado. Sem esta chamada a conta nunca mais
            // publica, todas as tentativas se esgotam em segundos e o lancamento para
            // em Erro, travando os sucessores.
            cliente.ResumePublish(evento.ContaId);
            throw;
        }
    }

    private async Task<PublisherClient> ObterClienteAsync(CancellationToken cancellationToken)
    {
        if (_cliente is not null) return _cliente;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cliente ??= await new PublisherClientBuilder
            {
                TopicName = TopicName.FromProjectTopic(_opcoes.ProjectId, _opcoes.Topico),
                EmulatorDetection = EmulatorDetection.EmulatorOrProduction,

                // Sem isto o cliente RECUSA mensagens que tragam OrderingKey.
                Settings = new PublisherClient.Settings { EnableMessageOrdering = true }
            }.BuildAsync(cancellationToken);

            return _cliente;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cliente is not null)
        {
            await _cliente.ShutdownAsync(TimeSpan.FromSeconds(5));
        }

        _lock.Dispose();
    }
}
