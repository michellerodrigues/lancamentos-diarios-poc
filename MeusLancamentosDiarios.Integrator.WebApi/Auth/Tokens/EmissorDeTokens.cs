using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;

/// <summary>
/// Emite o JWT de acesso. A WebApi e a unica emissora; o BFF so valida, com a mesma
/// chave, emissor e audiencia.
/// </summary>
public sealed class EmissorDeTokens(IOptions<AuthOptions> options, TimeProvider relogio)
{
    private static readonly JsonWebTokenHandler Handler = new();

    public TokenDeAcesso CriarAcesso(UsuarioEntity usuario)
    {
        var jwt = options.Value.Jwt;
        var agora = relogio.GetUtcNow();
        var expiraEm = agora + jwt.ValidadeAcesso;

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Emissor,
            Audience = jwt.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.ChaveDeAssinatura(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString("D"),
                [JwtRegisteredClaimNames.Email] = usuario.Email,
                [JwtRegisteredClaimNames.Name] = usuario.Nome,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),

                // A conta vai no token: quem decide "e a sua conta?" nao precisa ir ao banco.
                [ClaimsDoToken.Conta] = usuario.ContaId,
                [ClaimsDoToken.Role] = usuario.Role
            }
        });

        return new TokenDeAcesso(token, expiraEm);
    }
}
