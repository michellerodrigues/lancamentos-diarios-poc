using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Messages.Saldo;

public sealed record LancamentoPendenteResponse
{
    public required Guid Id { get; init; }

    public required long Sequencia { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    public required decimal Valor { get; init; }

    public required DateOnly DataLancamento { get; init; }

    public required DateTimeOffset DataRegistro { get; init; }

    public string? Observacao { get; init; }

    /// <summary>Em que ponto da esteira o lancamento esta.</summary>
    public required StageLancamentoEnum Stage { get; init; }
}
