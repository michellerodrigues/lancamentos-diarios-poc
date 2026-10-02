namespace MeusLancamentosDiarios.Integrator.Common.Auth;

/// <summary>Claims proprios do token. Os registrados (sub, email, name) vem de JwtRegisteredClaimNames.</summary>
public static class ClaimsDoToken
{
    /// <summary>A conta do usuario. Cada usuario tem exatamente uma.</summary>
    public const string Conta = "conta";

    public const string Role = "role";
}
