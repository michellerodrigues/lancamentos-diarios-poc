using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

public sealed record LancamentoDoLoteResponse
{
    public required Guid Id { get; init; }

    public required string ContaId { get; init; }

    public required long Sequencia { get; init; }

    /// <summary>Sempre Cadastrado: quem publica o lote e o relay.</summary>
    public required StageLancamentoEnum Stage { get; init; }
}
