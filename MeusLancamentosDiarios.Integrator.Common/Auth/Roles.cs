namespace MeusLancamentosDiarios.Integrator.Common.Auth;

/// <summary>Roles do sistema. Viajam no claim "role" do JWT; as mesmas no BFF e na WebApi.</summary>
public static class Roles
{
    /// <summary>Dono de uma conta: ve e lanca so na propria.</summary>
    public const string Cliente = "Cliente";

    /// <summary>Ve qualquer conta, lista as contas e usa o lancamento em lote.</summary>
    public const string Admin = "Admin";
}
