namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo;

public sealed class SaldoOptions
{
    public const string Secao = "Saldo";

    /// <summary>Quantos pendentes o saldo detalha quando a chamada nao informa ?limitePendentes.</summary>
    public int LimitePendentesPadrao { get; set; }

    public bool Valida => LimitePendentesPadrao > 0;
}
