using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Bff.Contracts;

/// <summary>A confirmacao do lancamento recem-incluido, com os textos ja formatados.</summary>
public sealed record LancamentoCriadoTela
{
    public required Guid Id { get; init; }

    public required string ContaId { get; init; }

    public required long Sequencia { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    public required string TipoRotulo { get; init; }

    public required decimal Valor { get; init; }

    public required string ValorFormatado { get; init; }

    public required string Sinal { get; init; }

    public required DateOnly DataLancamento { get; init; }

    public required string DataLancamentoFormatada { get; init; }

    public required DateTimeOffset DataRegistro { get; init; }

    public required string DataRegistroFormatada { get; init; }

    public string? Observacao { get; init; }

    public required StageLancamentoEnum Stage { get; init; }

    public required string StageRotulo { get; init; }
}
