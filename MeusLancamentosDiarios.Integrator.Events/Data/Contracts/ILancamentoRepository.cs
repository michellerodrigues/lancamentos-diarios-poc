using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Events.Data.Contracts;

/// <summary>
/// Toda transicao de estagio passa por aqui. Os UPDATEs sempre trazem o estagio
/// esperado no WHERE — e isso que torna o fluxo idempotente diante da entrega
/// "pelo menos uma vez" do Pub/Sub.
///
/// A ordem dentro de uma conta e responsabilidade desta camada: o Pub/Sub ordena o
/// que foi publicado, mas nao sabe de um lancamento que nunca chegou a ser publicado.
/// Quem impede o buraco e a consulta de reserva.
/// </summary>
public interface ILancamentoRepository
{
    /// <summary>
    /// WebApi: grava o lancamento em <see cref="StageLancamentoEnum.Cadastrado"/>,
    /// atribuindo a proxima sequencia da conta. Devolve a sequencia atribuida.
    /// </summary>
    Task<long> InserirAsync(LancamentoEntity lancamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// WebApi: grava varios lancamentos numa transacao so, todos em
    /// <see cref="StageLancamentoEnum.Cadastrado"/>. Dentro de cada conta a sequencia segue
    /// a ordem da lista. Atribui a <see cref="LancamentoEntity.Sequencia"/> de cada item.
    /// </summary>
    Task InserirLoteAsync(IReadOnlyList<LancamentoEntity> lancamentos, CancellationToken cancellationToken = default);

    /// <summary>
    /// WebApi: true quando nao ha nenhum lancamento anterior da mesma conta esperando
    /// publicacao. So nesse caso a publicacao imediata e segura — publicar por cima de
    /// um predecessor pendente inverteria a ordem de forma irreversivel.
    /// </summary>
    Task<bool> SemPredecessorPendenteAsync(
        string contaId, long sequencia, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserva UMA linha para publicacao: Cadastrado -> Lido.
    /// Retorna false quando a linha ja saiu de Cadastrado — e o que impede a WebApi e
    /// o relay de publicarem a mesma linha caso os dois a alcancem ao mesmo tempo.
    /// </summary>
    Task<bool> ReservarUmAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Events: reserva um lote para publicacao, movendo para Lido.
    ///
    /// So traz lancamentos sem predecessor pendente na mesma conta, e devolve tudo
    /// ordenado por (ContaId, Sequencia). Pega tambem o que ficou preso em Lido ha
    /// mais de <paramref name="reservaExpiraEm"/> — reserva de quem nao concluiu.
    /// </summary>
    Task<IReadOnlyList<LancamentoEntity>> ReservarParaPublicacaoAsync(
        int limite, TimeSpan reservaExpiraEm, CancellationToken cancellationToken = default);

    /// <summary>Events: Lido -> Enfileirado, apos o Pub/Sub confirmar o publish.</summary>
    Task MarcarEnfileiradoAsync(Guid id, string mensagemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Events: Lido -> Cadastrado, sem contar tentativa nem registrar erro.
    ///
    /// Usado quando um lancamento reservado nao chega a ser publicado porque um
    /// anterior da mesma conta falhou no mesmo ciclo. Nada deu errado com ele — so
    /// perdeu a vez, e volta a fila para o proximo ciclo.
    /// </summary>
    Task LiberarReservaAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Events: devolve para Cadastrado apos falha transitoria, ou Erro ao estourar as tentativas.</summary>
    Task DevolverParaFilaAsync(
        Guid id,
        string motivo,
        int maxTentativas,
        DateTimeOffset proximaTentativaEm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumer: -> EmProcessamento. Retorna null quando a linha ja saiu do fluxo,
    /// o que identifica uma entrega duplicada e permite dar ack sem reprocessar.
    /// </summary>
    Task<LancamentoEntity?> IniciarProcessamentoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Consumer: aplica o valor ao saldo da conta e marca Consolidado, na mesma transacao.</summary>
    Task<bool> ConsolidarAsync(LancamentoEntity lancamento, CancellationToken cancellationToken = default);

    /// <summary>Consumer: devolve para Enfileirado apos falha, ou Erro ao estourar as tentativas.</summary>
    Task DevolverParaProcessamentoAsync(
        Guid id, string motivo, int maxTentativas, CancellationToken cancellationToken = default);

    /// <summary>Marca Erro com o motivo, sem chance de nova tentativa.</summary>
    Task MarcarErroAsync(Guid id, string motivo, CancellationToken cancellationToken = default);

    /// <summary>Da conta: tudo que ja foi cadastrado e ainda nao consolidou (nem falhou).</summary>
    Task<IReadOnlyList<LancamentoEntity>> ListarPendentesAsync(
        string contaId, int limite, CancellationToken cancellationToken = default);

    /// <summary>Saldo consolidado da conta somado ao que esta em transito.</summary>
    Task<SaldoAtual> ObterSaldoAsync(string contaId, CancellationToken cancellationToken = default);

    /// <summary>Contas que ja receberam algum lancamento. Alimenta o seletor da tela.</summary>
    Task<IReadOnlyList<string>> ListarContasAsync(CancellationToken cancellationToken = default);

    Task<LancamentoEntity?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}
