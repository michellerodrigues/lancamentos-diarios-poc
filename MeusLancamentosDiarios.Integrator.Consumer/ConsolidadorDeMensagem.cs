using System.Text.Json;
using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Consumer;

/// <summary>
/// Regra de consolidação de uma mensagem, isolada do transporte.
///
/// Recebe o corpo JSON e devolve o que fazer com a mensagem; não conhece Pub/Sub,
/// gRPC nem <c>SubscriberClient</c>. O worker cuida do transporte e traduz a
/// resposta — assim toda a regra fica exercitável sem subir broker nenhum.
///
/// Transições: Enfileirado → EmProcessamento → Consolidado (ou Erro).
/// A entrega do Pub/Sub é "pelo menos uma vez", então a proteção contra processar
/// duas vezes está no estágio gravado na própria linha, não aqui.
/// </summary>
public sealed class ConsolidadorDeMensagem(
    ILancamentoRepository repositorio,
    IOptions<ConsumerOptions> options,
    ILogger<ConsolidadorDeMensagem> logger)
{
    private readonly ConsumerOptions _opcoes = options.Value;

    public async Task<RespostaAoBrokerEnum> ProcessarAsync(
        string corpo, string mensagemId, CancellationToken cancellationToken)
    {
        LancamentoRegistradoEvent? evento;

        try
        {
            evento = JsonSerializer.Deserialize<LancamentoRegistradoEvent>(corpo, EventosJson.Options);
        }
        catch (JsonException ex)
        {
            // Reentregar não conserta um corpo malformado: descarta e segue.
            logger.LogError(ex, "Mensagem {MensagemId} descartada: corpo invalido.", mensagemId);
            return RespostaAoBrokerEnum.Confirmar;
        }

        if (evento is null)
        {
            logger.LogError("Mensagem {MensagemId} descartada: corpo vazio.", mensagemId);
            return RespostaAoBrokerEnum.Confirmar;
        }

        var lancamento = await repositorio.IniciarProcessamentoAsync(evento.LancamentoId, cancellationToken);

        if (lancamento is null)
        {
            // A linha não estava em Lido nem Enfileirado: já foi consolidada,
            // parou em Erro, ou está sendo processada agora. Nada a fazer.
            logger.LogInformation(
                "LancamentoEntity {ContaId}#{Sequencia} ignorado: entrega duplicada ou ja finalizado.",
                evento.ContaId, evento.Sequencia);

            return RespostaAoBrokerEnum.Confirmar;
        }

        try
        {
            var consolidado = await repositorio.ConsolidarAsync(lancamento, cancellationToken);

            if (!consolidado)
            {
                logger.LogWarning(
                    "LancamentoEntity {ContaId}#{Sequencia} saiu de EmProcessamento antes da consolidacao.",
                    lancamento.ContaId, lancamento.Sequencia);

                return RespostaAoBrokerEnum.Confirmar;
            }

            logger.LogInformation(
                "LancamentoEntity {ContaId}#{Sequencia} consolidado ({Delta} centavos).",
                lancamento.ContaId, lancamento.Sequencia, lancamento.DeltaCentavos);

            return RespostaAoBrokerEnum.Confirmar;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Devolve a linha para Enfileirado: é o estágio em que
            // IniciarProcessamentoAsync a aceita de volta na reentrega. Sem isso
            // ela ficaria presa em EmProcessamento e toda reentrega seria
            // descartada como duplicata.
            await repositorio.DevolverParaProcessamentoAsync(
                lancamento.Id, ex.Message, _opcoes.MaxTentativas, CancellationToken.None);

            logger.LogError(ex,
                "Falha ao consolidar o lancamento {ContaId}#{Sequencia}.",
                lancamento.ContaId, lancamento.Sequencia);

            return RespostaAoBrokerEnum.Devolver;
        }
    }
}
