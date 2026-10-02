using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Publishing;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Consumer;

/// <summary>
/// Assina a subscription do Pub/Sub e entrega cada mensagem ao
/// <see cref="ConsolidadorDeMensagem"/>.
///
/// Esta classe cuida só do transporte: conexão, ciclo de vida e tradução da
/// resposta para <c>SubscriberClient.Reply</c>. A regra fica no consolidador,
/// que não conhece Pub/Sub e por isso é testável sem broker.
/// </summary>
public sealed class ConsolidacaoWorker(
    ConsolidadorDeMensagem consolidador,
    DatabaseBootstrapper bootstrapper,
    PubSubProvisionador provisionador,
    IOptions<PubSubOptions> pubSubOptions,
    IOptions<ConsumerOptions> consumerOptions,
    ILogger<ConsolidacaoWorker> logger) : BackgroundService
{
    private readonly PubSubOptions _pubSub = pubSubOptions.Value;
    private readonly ConsumerOptions _opcoes = consumerOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bootstrapper.GarantirEsquemaAsync(stoppingToken);
        await provisionador.GarantirRecursosAsync(stoppingToken);

        var subscription = SubscriptionName.FromProjectSubscription(
            _pubSub.ProjectId, _pubSub.Subscription);

        var assinante = await new SubscriberClientBuilder
        {
            SubscriptionName = subscription,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            Settings = new SubscriberClient.Settings
            {
                // Concorrência entre CONTAS. Dentro de cada conta o Pub/Sub já
                // entrega uma mensagem por vez, por conta da ordering key.
                FlowControlSettings = new FlowControlSettings(
                    maxOutstandingElementCount: _opcoes.Concorrencia,
                    maxOutstandingByteCount: null)
            }
        }.BuildAsync(stoppingToken);

        logger.LogInformation("Consumer ouvindo {Subscription}.", subscription.SubscriptionId);

        var escuta = assinante.StartAsync(async (mensagem, cancellationToken) =>
        {
            var resposta = await consolidador.ProcessarAsync(
                mensagem.Data.ToStringUtf8(), mensagem.MessageId, cancellationToken);

            return resposta == RespostaAoBrokerEnum.Confirmar
                ? SubscriberClient.Reply.Ack
                : SubscriberClient.Reply.Nack;
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal do host.
        }

        await assinante.StopAsync(TimeSpan.FromSeconds(10));
        await escuta;
    }
}
