namespace MeusLancamentosDiarios.Integrator.Events.Data;

/// <summary>
/// Leitura completa do saldo de uma conta: o valor ja consolidado pelo Consumer e o
/// que ainda esta em transito na esteira de estagios.
/// </summary>
public sealed record SaldoAtual
{
    public required string ContaId { get; init; }

    public required long SaldoConsolidadoCentavos { get; init; }

    /// <summary>
    /// Ultima sequencia aplicada ao saldo. Como a conta consolida em ordem, este
    /// numero diz exatamente ate onde o saldo esta em dia.
    /// </summary>
    public required long UltimaSequenciaConsolidada { get; init; }

    /// <summary>Null enquanto a conta nao consolidou nada — nao existe linha no read model.</summary>
    public required DateTimeOffset? AtualizadoEm { get; init; }

    public required long CreditosPendentesCentavos { get; init; }

    public required long DebitosPendentesCentavos { get; init; }

    public required int QuantidadePendentes { get; init; }

    /// <summary>Consolidado mais o que ainda nao foi consolidado.</summary>
    public long SaldoProjetadoCentavos =>
        SaldoConsolidadoCentavos + CreditosPendentesCentavos - DebitosPendentesCentavos;

    /// <summary>Dispara o aviso de "consolidacao em andamento" nas telas.</summary>
    public bool ConsolidacaoEmAndamento => QuantidadePendentes > 0;
}
