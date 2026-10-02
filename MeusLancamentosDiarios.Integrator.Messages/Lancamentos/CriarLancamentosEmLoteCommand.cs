using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

/// <summary>
/// Varios lancamentos de uma vez, em contas quaisquer. Existe para simular carga na
/// apresentacao sem passar pela tela.
/// </summary>
public sealed record CriarLancamentosEmLoteCommand : ICommand<CriarLancamentosEmLoteResponse>
{
    /// <summary>
    /// Cada item tem o mesmo formato e as mesmas regras do lancamento avulso. Dentro
    /// de cada conta, a sequencia segue a ordem desta lista.
    /// </summary>
    public required IReadOnlyList<CriarLancamentoCommand> Itens { get; init; }
}
