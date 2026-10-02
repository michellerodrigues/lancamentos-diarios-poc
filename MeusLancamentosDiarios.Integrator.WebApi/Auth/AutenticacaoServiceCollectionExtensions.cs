using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

public static class AutenticacaoServiceCollectionExtensions
{
    /// <summary>
    /// Emissao e validacao do JWT, policies por role e o que os handlers de
    /// autenticacao usam (usuarios, tokens, e-mail, Google).
    /// </summary>
    public static IServiceCollection AddAutenticacao(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Sem chave, ou com chave curta, a aplicacao nem sobe: melhor do que subir
        // emitindo token que qualquer um falsifica.
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.Secao))
            .Validate(o => o.Jwt.ChaveValida,
                $"Auth:Jwt:Chave precisa de ao menos {JwtOptions.TamanhoMinimoChave} bytes.")
            .Validate(o => o.Jwt.Valida,
                "Auth:Jwt precisa de Emissor, Audiencia, ValidadeAcesso e ValidadeRenovacao (maior que o acesso).")
            .Validate(o => o.RedefinicaoSenha.Valida,
                "Auth:RedefinicaoSenha precisa de UrlDoFront absoluta e Validade maior que zero.")
            .Validate(o => o.Email.Valida, "Auth:Email precisa de Host, Porta e Remetente.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((bearer, auth) =>
            {
                // Mantem "sub", "role" e "conta" como vieram, sem renomear para as
                // URIs longas do WS-Federation.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = auth.Value.Jwt.ParametrosDeValidacao();
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.ClienteOuAdmin, p => p.RequireRole(Roles.Cliente, Roles.Admin))
            .AddPolicy(Politicas.SomenteAdmin, p => p.RequireRole(Roles.Admin))

            // Fechado por padrao: endpoint novo sem policy exige login. O que e
            // publico (health, OpenAPI, login) diz isso explicitamente.
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        services.AddSingleton<IPasswordHasher<UsuarioEntity>, PasswordHasher<UsuarioEntity>>();
        services.AddSingleton<IUsuarioRepository, UsuarioRepository>();
        services.AddSingleton<AuthBootstrapper>();
        services.AddSingleton<EmissorDeTokens>();
        services.AddSingleton<AberturaDeSessao>();
        services.AddSingleton<CadastroDeCliente>();
        services.AddSingleton<IEnviadorEmail, SmtpEnviadorEmail>();
        services.AddSingleton<IValidadorGoogle, ValidadorGoogle>();

        return services;
    }
}
