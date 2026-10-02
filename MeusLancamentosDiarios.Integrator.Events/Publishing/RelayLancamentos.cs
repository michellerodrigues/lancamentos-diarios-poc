using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Events.Publishing;

/// <summary>
/// Publicação de lançamentos no Pub/Sub, com a transição de estágio correspondente.
///
/// Dois caminhos chegam aqui:
///
/// - <see cref="PublicarAsync"/>: a WebApi publica logo após gravar, quando a conta
///   não tem nenhum lançamento anterior pendente. É o caminho feliz, com latência de
///   milissegundos.
/// - <see cref="ExecutarCicloAsync"/>: a varredura, que recolhe o que ficou para trás.
///   É a rede de proteção, e é ela que garante a ordem dentro da conta.
///
/// O ciclo é público e independente de host de propósito: localmente quem o chama em
/// loop é o <see cref="RelayWorker"/>; no GCP quem chama é o Cloud Scheduler, via
/// Cloud Run Job. O código do relay é o mesmo nos dois casos.
/// </summary>
public sealed class RelayLancamentos(
    ILancamentoRepository repositorio,
    IPublicadorEventos publicador,
    IOptions<RelayOptions> options,
    ILogger<RelayLancamentos> logger)
{
    private readonly RelayOptions _opcoes = options.Value;

    /// <summary>
    /// Publica um lançamento já reservado (estágio Lido) e o move para Enfileirado.
    ///
    /// Falhando, devolve a linha para Cadastrado — ou para Erro ao esgotar as
    /// tentativas — e retorna false. Nunca propaga a exceção: quem chama decide o que
    /// fazer, e na WebApi a resposta ao usuário não pode depender disso.
    /// </summary>
    public async Task<bool> PublicarAsync(LancamentoEntity lancamento, CancellationToken cancellationToken = default)
    {
        var evento = new LancamentoRegistradoEvent
        {
            LancamentoId = lancamento.Id,
            ContaId = lancamento.ContaId,
            Sequencia = lancamento.Sequencia,
            Tipo = lancamento.Tipo,
            ValorCentavos = lancamento.ValorCentavos,
            DataLancamento = lancamento.DataLancamento,
            DataRegistro = lancamento.DataRegistro,
            Observacao = lancamento.Observacao
        };

        try
        {
            var mensagemId = await publicador.PublicarAsync(evento, cancellationToken);
            await repositorio.MarcarEnfileiradoAsync(lancamento.Id, mensagemId, cancellationToken);

            logger.LogInformation(
                "LancamentoEntity {ContaId}#{Sequencia} enfileirado (mensagem {MensagemId}).",
                lancamento.ContaId, lancamento.Sequencia, mensagemId);

            return true;
        }
        catch (Exception ex)
        {
            // CancellationToken.None: o cancelamento pode ser justamente o motivo da
            // falha, e a linha precisa sair de Lido de qualquer jeito — senão ficaria
            // presa num estágio que nenhuma varredura recolhe.
            await repositorio.DevolverParaFilaAsync(
                lancamento.Id,
                ex.Message,
                _opcoes.MaxTentativas,
                DateTimeOffset.UtcNow + CalcularBackoff(lancamento.TentativasEnvio + 1),
                CancellationToken.None);

            logger.LogError(ex,
                "Falha ao publicar o lancamento {ContaId}#{Sequencia}.",
                lancamento.ContaId, lancamento.Sequencia);

            return false;
        }
    }

    /// <summary>
    /// Espera antes da próxima tentativa: dobra a cada falha, com teto configurável.
    /// Uma queda de dez segundos do broker não pode gastar as cinco tentativas.
    /// </summary>
    internal TimeSpan CalcularBackoff(int tentativa)
    {
        var segundos = Math.Pow(2, Math.Clamp(tentativa, 1, 16));
        var espera = TimeSpan.FromSeconds(segundos);

        return espera < _opcoes.BackoffMaximo ? espera : _opcoes.BackoffMaximo;
    }

    /// <summary>
    /// Um ciclo de varredura. Retorna quantos lançamentos foram publicados.
    ///
    /// O lote já vem ordenado por (ContaId, Sequencia) e sem buracos. Falhando um
    /// lançamento, todos os seguintes DAQUELA conta são liberados sem tentativa:
    /// publicá-los inverteria a ordem, e o Pub/Sub não teria como consertar depois.
    /// As outras contas seguem normalmente.
    /// </summary>
    public async Task<int> ExecutarCicloAsync(CancellationToken cancellationToken = default)
    {
        var reservados = await repositorio.ReservarParaPublicacaoAsync(
            _opcoes.TamanhoLote, _opcoes.ReservaExpiraEm, cancellationToken);

        if (reservados.Count == 0) return 0;

        logger.LogInformation("{Quantidade} lancamento(s) marcado(s) como Lido.", reservados.Count);

        var publicados = 0;
        var contasTravadas = new HashSet<string>(StringComparer.Ordinal);

        foreach (var lancamento in reservados)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (contasTravadas.Contains(lancamento.ContaId))
            {
                // Um anterior desta conta falhou neste ciclo. Este não tem problema
                // nenhum — só perdeu a vez. Volta para Cadastrado sem contar tentativa.
                await repositorio.LiberarReservaAsync(lancamento.Id, cancellationToken);

                logger.LogInformation(
                    "LancamentoEntity {ContaId}#{Sequencia} adiado: a conta travou num anterior.",
                    lancamento.ContaId, lancamento.Sequencia);

                continue;
            }

            if (await PublicarAsync(lancamento, cancellationToken))
            {
                publicados++;
            }
            else
            {
                contasTravadas.Add(lancamento.ContaId);
            }
        }

        return publicados;
    }
}
