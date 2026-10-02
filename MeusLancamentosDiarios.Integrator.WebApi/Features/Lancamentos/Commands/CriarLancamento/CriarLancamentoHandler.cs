using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Helpers;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamento;

/// <summary>
/// Grava o lançamento e, quando é seguro, publica no mesmo request.
///
/// A gravação é a fonte da verdade — não há transação atômica possível entre banco e
/// broker, e é por isso que o estágio vive na própria linha.
///
/// A publicação imediata só acontece se a conta não tiver nenhum lançamento anterior
/// esperando publicação. Publicar por cima de um predecessor pendente inverteria a
/// ordem de forma irreversível: o Pub/Sub ordena o que recebe, e nada lhe diz que
/// faltou algo antes. Havendo predecessor, o lançamento fica para o relay, que publica
/// a conta inteira na ordem certa.
/// </summary>
public sealed class CriarLancamentoHandler(
    ILancamentoRepository repositorio,
    RelayLancamentos relay,
    TimeProvider relogio,
    ILogger<CriarLancamentoHandler> logger)
    : ICommandHandler<CriarLancamentoCommand, CriarLancamentoResponse>
{
    /// <summary>
    /// Teto para a publicação imediata. Passando disso, responde assim mesmo e deixa
    /// para a varredura: o usuário não pode ficar refém da latência do broker.
    /// </summary>
    public static readonly TimeSpan LimitePublicacao = TimeSpan.FromSeconds(2);

    /// <summary>A linha a gravar. Compartilhado com o lote, para as duas portas normalizarem igual.</summary>
    internal static LancamentoEntity Montar(CriarLancamentoCommand comando, DateTimeOffset registro) => new()
    {
        Id = Guid.CreateVersion7(),

        // Normalizado: a conta vira ordering key, e chave é comparação exata.
        // "abc1234" e "ABC1234" seriam duas contas diferentes no Pub/Sub.
        ContaId = comando.ContaId.Trim().ToUpperInvariant(),

        Tipo = comando.Tipo,
        ValorCentavos = DinheiroHelper.ParaCentavos(comando.Valor),
        DataLancamento = comando.DataLancamento,
        DataRegistro = registro,
        Observacao = string.IsNullOrWhiteSpace(comando.Observacao) ? null : comando.Observacao.Trim(),
        Stage = StageLancamentoEnum.Cadastrado
    };

    public async Task<CriarLancamentoResponse> HandleAsync(
        CriarLancamentoCommand comando, CancellationToken cancellationToken)
    {
        var lancamento = Montar(comando, relogio.GetUtcNow());

        // A sequência é atribuída aqui dentro, como MAX + 1 da conta.
        await repositorio.InserirAsync(lancamento, cancellationToken);

        var stage = await TentarPublicarAsync(lancamento, cancellationToken);

        return new CriarLancamentoResponse
        {
            Id = lancamento.Id,
            ContaId = lancamento.ContaId,
            Sequencia = lancamento.Sequencia,
            Tipo = lancamento.Tipo,
            Valor = DinheiroHelper.ParaReais(lancamento.ValorCentavos),
            DataLancamento = lancamento.DataLancamento,
            DataRegistro = lancamento.DataRegistro,
            Observacao = lancamento.Observacao,
            Stage = stage
        };
    }

    /// <summary>
    /// Devolve o estágio em que o lançamento ficou.
    ///
    /// Nunca lança. O lançamento já está gravado, e responder erro faria o usuário
    /// achar que não foi e lançar de novo: duplicata criada por nós.
    /// </summary>
    private async Task<StageLancamentoEnum> TentarPublicarAsync(
        LancamentoEntity lancamento, CancellationToken cancellationToken)
    {
        try
        {
            // Há alguém desta conta na frente ainda não publicado? Então a vez não é
            // desta linha. O relay publica a conta em ordem no próximo ciclo.
            if (!await repositorio.SemPredecessorPendenteAsync(
                    lancamento.ContaId, lancamento.Sequencia, cancellationToken))
            {
                logger.LogInformation(
                    "LancamentoEntity {ContaId}#{Sequencia} aguarda o relay: ha predecessor pendente na conta.",
                    lancamento.ContaId, lancamento.Sequencia);

                return StageLancamentoEnum.Cadastrado;
            }

            // Reservar antes de publicar: se o relay alcançou esta mesma linha, ele
            // leva, e a reserva falha aqui — nenhuma das duas pontas publica duas vezes.
            if (!await repositorio.ReservarUmAsync(lancamento.Id, cancellationToken))
            {
                return StageLancamentoEnum.Lido;
            }

            var publicacao = relay.PublicarAsync(lancamento, cancellationToken);

            // PublisherClient.PublishAsync não aceita CancellationToken: passar um
            // token adiante não limita nada. O único jeito de cortar a espera é
            // parar de esperar.
            using var relogioDeEspera = new CancellationTokenSource();

            if (await Task.WhenAny(publicacao, Task.Delay(LimitePublicacao, relogioDeEspera.Token))
                != publicacao)
            {
                // A publicação segue em segundo plano. Concluindo, marca Enfileirado;
                // falhando, devolve para Cadastrado. Travando de vez, a linha fica em
                // Lido e a varredura a recolhe quando a reserva vencer. Os três
                // desfechos são seguros: no pior caso o evento sai duplicado, e o
                // Consumer descarta a repetição pelo estágio.
                logger.LogWarning(
                    "Publicacao de {ContaId}#{Sequencia} passou de {Limite}. Respondendo sem esperar.",
                    lancamento.ContaId, lancamento.Sequencia, LimitePublicacao);

                return StageLancamentoEnum.Lido;
            }

            relogioDeEspera.Cancel();

            return await publicacao
                ? StageLancamentoEnum.Enfileirado
                : StageLancamentoEnum.Cadastrado;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Publicacao imediata de {ContaId}#{Sequencia} falhou. A varredura do relay recolhe.",
                lancamento.ContaId, lancamento.Sequencia);

            return StageLancamentoEnum.Cadastrado;
        }
    }
}
