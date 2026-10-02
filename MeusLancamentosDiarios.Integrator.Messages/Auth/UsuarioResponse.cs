namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

public sealed record UsuarioResponse
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    public required string Email { get; init; }

    /// <summary>A unica conta do usuario.</summary>
    public required string ContaId { get; init; }

    public required string Role { get; init; }
}
