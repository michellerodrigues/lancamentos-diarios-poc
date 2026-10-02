using MeusLancamentosDiarios.Integrator.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Bff.Auth;

public static class AutenticacaoServiceCollectionExtensions
{
    public static IServiceCollection AddAutenticacao(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Secao))
            .Validate(o => o.Valida,
                $"Auth:Jwt precisa de Emissor, Audiencia e Chave de ao menos {JwtOptions.TamanhoMinimoChave} bytes, " +
                "os mesmos da WebApi.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = jwt.Value.ParametrosDeValidacao();
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.ClienteOuAdmin, p => p.RequireRole(Roles.Cliente, Roles.Admin))
            .AddPolicy(Politicas.SomenteAdmin, p => p.RequireRole(Roles.Admin))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddHttpContextAccessor();
        services.AddTransient<RepassarTokenHandler>();

        return services;
    }
}
