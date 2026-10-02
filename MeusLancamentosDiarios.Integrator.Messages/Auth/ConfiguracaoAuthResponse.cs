namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

/// <summary>O que a tela de login precisa saber antes de existir sessao.</summary>
public sealed record ConfiguracaoAuthResponse
{
    /// <summary>Client ID OAuth do Google. Vazio quando o login com Google esta desligado.</summary>
    public required string GoogleClientId { get; init; }
}
