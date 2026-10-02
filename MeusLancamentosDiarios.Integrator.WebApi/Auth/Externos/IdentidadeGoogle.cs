namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos;

/// <summary>Quem o Google diz que o usuario e, ja com a assinatura conferida.</summary>
public sealed record IdentidadeGoogle(string GoogleId, string Email, string Nome, bool EmailVerificado);
