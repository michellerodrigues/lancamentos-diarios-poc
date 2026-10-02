using System.Text;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MeusLancamentosDiarios.Integrator.Bff.Auth;

/// <summary>
/// O BFF nao emite token: so valida o que a WebApi emitiu, com a mesma chave, emissor
/// e audiencia. A secao de configuracao tem o mesmo nome nos dois projetos.
/// </summary>
public sealed class JwtOptions
{
    public const string Secao = "Auth:Jwt";

    public const int TamanhoMinimoChave = 32;

    public string Emissor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    public string Chave { get; set; } = string.Empty;

    public bool ChaveValida => Encoding.UTF8.GetByteCount(Chave) >= TamanhoMinimoChave;

    public bool Valida =>
        ChaveValida && !string.IsNullOrWhiteSpace(Emissor) && !string.IsNullOrWhiteSpace(Audiencia);

    /// <summary>As mesmas regras da WebApi. Mudando la, mude aqui.</summary>
    public TokenValidationParameters ParametrosDeValidacao() => new()
    {
        ValidIssuer = Emissor,
        ValidAudience = Audiencia,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Chave)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Name,
        RoleClaimType = ClaimsDoToken.Role
    };
}
