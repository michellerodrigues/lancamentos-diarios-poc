namespace MeusLancamentosDiarios.Integrator.Bff.Contracts;

/// <summary>
/// O payload que a tela de Saldo Consolidado consome. Alem dos numeros crus,
/// traz os textos ja formatados em pt-BR para o Angular nao repetir formatacao.
/// </summary>
public sealed record SaldoTela
{
    public required string ContaId { get; init; }

    public required decimal SaldoConsolidado { get; init; }

    public required string SaldoConsolidadoFormatado { get; init; }

    /// <summary>Null enquanto a conta nao consolidou nada.</summary>
    public required DateTimeOffset? AtualizadoEm { get; init; }

    public required string UltimaAtualizacaoFormatada { get; init; }

    /// <summary>Ate que sequencia da conta o saldo consolidado esta em dia.</summary>
    public required long UltimaSequenciaConsolidada { get; init; }

    public required decimal SaldoProjetado { get; init; }

    public required string SaldoProjetadoFormatado { get; init; }

    public required decimal CreditosPendentes { get; init; }

    public required string CreditosPendentesFormatado { get; init; }

    public required decimal DebitosPendentes { get; init; }

    public required string DebitosPendentesFormatado { get; init; }

    public required bool ConsolidacaoEmAndamento { get; init; }

    /// <summary>
    /// De quanto em quanto tempo o front deve reconsultar enquanto houver pendencia.
    /// Zero significa que nao ha nada em transito e o polling pode parar.
    /// </summary>
    public required int IntervaloPollingMs { get; init; }

    public required IReadOnlyList<PendenteTela> Pendentes { get; init; }
}
