using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

public static class UsuarioIdExtensions
{
    public static Guid UsuarioId(this ClaimsPrincipal usuario) =>
        Guid.Parse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? throw new InvalidOperationException("Token sem o claim sub."));
}
