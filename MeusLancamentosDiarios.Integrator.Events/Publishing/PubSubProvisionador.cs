using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Events.Publishing;

/// <summary>
/// Cria topico e subscription quando ainda nao existem. Serve ao emulador, que
/// sobe vazio a cada execucao. Em producao o provisionamento fica no Terraform.
/// </summary>
public sealed class PubSubProvisionador(
    IOptions<PubSubOptions> options,
    ILogger<PubSubProvisionador> logger)
{
    private readonly PubSubOptions _opcoes = options.Value;

    public async Task GarantirRecursosAsync(CancellationToken cancellationToken = default)
    {
        if (!_opcoes.CriarRecursos)
        {
            logger.LogInformation("CriarRecursos=false: topico e subscription vem do provisionamento externo.");
            return;
        }

        var topico = TopicName.FromProjectTopic(_opcoes.ProjectId, _opcoes.Topico);
        var subscription = SubscriptionName.FromProjectSubscription(_opcoes.ProjectId, _opcoes.Subscription);

        var publisherApi = await new PublisherServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync(cancellationToken);

        try
        {
            await publisherApi.CreateTopicAsync(topico, cancellationToken);
            logger.LogInformation("Topico {Topico} criado.", topico.TopicId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            logger.LogDebug("Topico {Topico} ja existia.", topico.TopicId);
        }

        var subscriberApi = await new SubscriberServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.BuildAsync(cancellationToken);

        try
        {
            await subscriberApi.CreateSubscriptionAsync(new Subscription
            {
                SubscriptionName = subscription,
                TopicAsTopicName = topico,

                // Entrega ordenada por ordering key. E propriedade da SUBSCRIPTION:
                // sem ela o publisher ate carimba a chave, mas a entrega ignora a ordem.
                EnableMessageOrdering = true,

                // O padrao de 10s e curto; a biblioteca ainda estende sozinha via
                // modifyAckDeadline enquanto a consolidacao nao termina.
                AckDeadlineSeconds = 60
            });

            logger.LogInformation("Subscription {Subscription} criada.", subscription.SubscriptionId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            logger.LogDebug("Subscription {Subscription} ja existia.", subscription.SubscriptionId);
        }
    }
}
