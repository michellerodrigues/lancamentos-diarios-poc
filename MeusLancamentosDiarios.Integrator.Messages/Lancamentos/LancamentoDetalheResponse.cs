using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

/// <summary>
/// O lancamento e em que ponto da esteira ele esta. A conta so e conhecida depois
/// de ler a linha, entao o filtro de posse confere na saida.
/// </summary>
public sealed record LancamentoDetalheResponse : IReferenciaConta
{
    public required Guid Id { get; init; }

    public required string ContaId { get; init; }

    public required long Sequencia { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    public required decimal Valor { get; init; }

    public required DateOnly DataLancamento { get; init; }

    public required DateTimeOffset DataRegistro { get; init; }

    public string? Observacao { get; init; }

    public required StageLancamentoEnum Stage { get; init; }

    public required DateTimeOffset StageAtualizadoEm { get; init; }

    public required int TentativasEnvio { get; init; }

    public required int TentativasProcessamento { get; init; }

    public string? Erro { get; init; }
}
