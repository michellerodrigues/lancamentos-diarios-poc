namespace MeusLancamentosDiarios.Integrator.Bff.Clients;

/// <summary>O que a WebApi respondeu, sem interpretar: status, corpo e tipo do corpo.</summary>
public sealed record RespostaRepassada(int StatusCode, string? Corpo, string? ContentType);
