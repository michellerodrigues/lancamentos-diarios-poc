namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

/// <summary>O que todo login devolve: cadastro, senha, Google e renovacao.</summary>
public sealed record SessaoResponse
{
    /// <summary>JWT de acesso. Vai no header <c>Authorization: Bearer</c>.</summary>
    public required string TokenAcesso { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    /// <summary>
    /// Opaco e de uso unico: troca por um par novo em /auth/renovar. O logout o revoga.
    /// </summary>
    public required string TokenRenovacao { get; init; }

    public required UsuarioResponse Usuario { get; init; }
}
