using System.Security.Claims;

namespace MeusLancamentosDiarios.Integrator.Common.Auth;

public static class UsuarioAutenticadoExtensions
{
    public static string? ContaId(this ClaimsPrincipal usuario) =>
        usuario.FindFirst(ClaimsDoToken.Conta)?.Value;

    public static bool IsAdmin(this ClaimsPrincipal usuario) => usuario.IsInRole(Roles.Admin);

    /// <summary>
    /// A regra de posse: o admin acessa qualquer conta; o cliente, so a dele. A role
    /// diz o que o usuario pode fazer; isto diz sobre qual conta. O BFF confere para
    /// recusar cedo e a WebApi confere de novo, porque e ela quem guarda o dado.
    /// </summary>
    public static bool PodeAcessarConta(this ClaimsPrincipal usuario, string contaId) =>
        usuario.IsAdmin()
        || string.Equals(usuario.ContaId(), contaId.Trim(), StringComparison.OrdinalIgnoreCase);
}
