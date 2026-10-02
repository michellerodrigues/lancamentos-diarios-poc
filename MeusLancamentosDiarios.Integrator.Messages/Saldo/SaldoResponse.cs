namespace MeusLancamentosDiarios.Integrator.Messages.Saldo;

public sealed record SaldoResponse
{
    public required string ContaId { get; init; }

    /// <summary>O que o Consumer ja aplicou ao read model desta conta.</summary>
    public required decimal SaldoConsolidado { get; init; }

    /// <summary>
    /// Ate que sequencia o saldo esta em dia. Como a conta consolida em ordem,
    /// este numero e suficiente para saber o que ja entrou.
    /// </summary>
    public required long UltimaSequenciaConsolidada { get; init; }

    /// <summary>Null enquanto a conta nao consolidou nada.</summary>
    public required DateTimeOffset? AtualizadoEm { get; init; }

    public required decimal CreditosPendentes { get; init; }

    public required decimal DebitosPendentes { get; init; }

    /// <summary>Consolidado mais o que ainda esta em transito.</summary>
    public required decimal SaldoProjetado { get; init; }

    /// <summary>
    /// Liga o aviso na tela e diz ao front que vale continuar consultando.
    /// Falso significa que nao ha nada em transito e o polling pode parar.
    /// </summary>
    public required bool ConsolidacaoEmAndamento { get; init; }

    public required IReadOnlyList<LancamentoPendenteResponse> Pendentes { get; init; }
}
