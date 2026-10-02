using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Bff.Contracts;

public sealed record PendenteTela
{
    public required Guid Id { get; init; }

    public required long Sequencia { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    /// <summary>"Credito" ou "Debito", pronto para exibicao.</summary>
    public required string TipoRotulo { get; init; }

    /// <summary>"+" para credito, "-" para debito.</summary>
    public required string Sinal { get; init; }

    public required decimal Valor { get; init; }

    public required string ValorFormatado { get; init; }

    public required DateOnly DataLancamento { get; init; }

    public required string DataLancamentoFormatada { get; init; }

    public string? Observacao { get; init; }

    public required StageLancamentoEnum Stage { get; init; }

    /// <summary>Texto do estagio para a tela de acompanhamento.</summary>
    public required string StageRotulo { get; init; }
}
