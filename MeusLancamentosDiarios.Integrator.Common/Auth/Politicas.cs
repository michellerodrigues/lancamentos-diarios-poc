namespace MeusLancamentosDiarios.Integrator.Common.Auth;

/// <summary>Policies que os endpoints exigem. Cada uma e um conjunto de roles.</summary>
public static class Politicas
{
    public const string ClienteOuAdmin = "ClienteOuAdmin";

    public const string SomenteAdmin = "SomenteAdmin";
}
