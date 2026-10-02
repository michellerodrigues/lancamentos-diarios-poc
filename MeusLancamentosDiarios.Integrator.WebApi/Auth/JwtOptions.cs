using System.Text;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

public sealed class JwtOptions
{
    /// <summary>HMAC-SHA256 pede chave de pelo menos 256 bits.</summary>
    public const int TamanhoMinimoChave = 32;

    public string Emissor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    /// <summary>
    /// Segredo compartilhado com o BFF, que valida o mesmo token. Nao fica no
    /// appsettings.json: vem do appsettings.Development.json, do compose ou do
    /// Secret Manager.
    /// </summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Curta de proposito: o logout so revoga a renovacao, nao o token ja emitido.</summary>
    public TimeSpan ValidadeAcesso { get; set; }

    public TimeSpan ValidadeRenovacao { get; set; }

    public bool ChaveValida => Encoding.UTF8.GetByteCount(Chave) >= TamanhoMinimoChave;

    public bool Valida =>
        ChaveValida
        && !string.IsNullOrWhiteSpace(Emissor)
        && !string.IsNullOrWhiteSpace(Audiencia)
        && ValidadeAcesso > TimeSpan.Zero
        && ValidadeRenovacao > ValidadeAcesso;

    public SymmetricSecurityKey ChaveDeAssinatura() => new(Encoding.UTF8.GetBytes(Chave));

    /// <summary>As mesmas regras que o BFF aplica. Mudando aqui, mude la.</summary>
    public TokenValidationParameters ParametrosDeValidacao() => new()
    {
        ValidIssuer = Emissor,
        ValidAudience = Audiencia,
        IssuerSigningKey = ChaveDeAssinatura(),
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
