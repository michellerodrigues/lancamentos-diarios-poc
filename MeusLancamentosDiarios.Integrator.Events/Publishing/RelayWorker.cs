using MeusLancamentosDiarios.Integrator.Events.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Events.Publishing;

/// <summary>
/// Host do relay, nos dois modelos de execucao:
///
/// - <c>LoopContinuo = true</c>: processo sempre ligado, chamando o ciclo em intervalo
///   fixo. E o modo de desenvolvimento local, e o equivalente a um servico ECS/Fargate.
///
/// - <c>LoopContinuo = false</c>: roda UM ciclo e encerra o processo. E o modelo
///   serverless — Cloud Scheduler disparando um Cloud Run Job, equivalente ao
///   EventBridge disparando uma Lambda na AWS.
///
/// O ciclo em si vive em <see cref="RelayLancamentos.ExecutarCicloAsync"/> e e o mesmo
/// nos dois casos: o que muda e so quem o chama.
/// </summary>
public sealed class RelayWorker(
    RelayLancamentos relay,
    DatabaseBootstrapper bootstrapper,
    PubSubProvisionador provisionador,
    IHostApplicationLifetime cicloDeVida,
    IOptions<RelayOptions> options,
    ILogger<RelayWorker> logger) : BackgroundService
{
    private readonly RelayOptions _opcoes = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bootstrapper.GarantirEsquemaAsync(stoppingToken);
        await provisionador.GarantirRecursosAsync(stoppingToken);

        if (!_opcoes.LoopContinuo)
        {
            await ExecutarCicloUnicoAsync(stoppingToken);
            return;
        }

        await ExecutarEmLoopAsync(stoppingToken);
    }

    /// <summary>
    /// Modelo serverless: um ciclo e o processo morre.
    ///
    /// Sair de ExecuteAsync nao encerra o host — ele continua esperando sinal de
    /// shutdown. Num Cloud Run Job isso travaria ate o timeout e a execucao contaria
    /// como falha, entao o encerramento e explicito.
    /// </summary>
    private async Task ExecutarCicloUnicoAsync(CancellationToken stoppingToken)
    {
        try
        {
            var publicados = await relay.ExecutarCicloAsync(stoppingToken);
            logger.LogInformation("Ciclo unico concluido: {Publicados} publicado(s).", publicados);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ciclo unico falhou.");

            // Codigo de saida diferente de zero: e assim que o Cloud Run Job
            // identifica a falha e aplica a politica de retentativa.
            Environment.ExitCode = 1;
        }
        finally
        {
            cicloDeVida.StopApplication();
        }
    }

    /// <summary>Modelo sempre ligado: usado no desenvolvimento local.</summary>
    private async Task ExecutarEmLoopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Relay ativo, ciclo a cada {Intervalo}.", _opcoes.Intervalo);

        using var timer = new PeriodicTimer(_opcoes.Intervalo);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await relay.ExecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Uma falha de ciclo nao pode derrubar o worker: o proximo tick tenta de novo.
                logger.LogError(ex, "Ciclo do relay falhou. Tentando no proximo tick.");
            }
        }
    }
}
