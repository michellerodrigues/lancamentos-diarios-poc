using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.Bff.Endpoints;

/// <summary>
/// Autenticacao vista pelo front. O service repassa para /auth da WebApi; o front
/// continua falando so com o BFF. Os corpos sao os comandos do Messages.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/auth").WithTags("Autenticacao").AllowAnonymous();

        grupo.MapPost("/registrar", async (RegistrarUsuarioCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.RegistrarAsync(pedido, ct)).ParaResultado())
            .WithName("Registrar")
            .WithSummary("Cadastro com e-mail e senha. Ja devolve a sessao.")
            .Produces<SessaoResponse>(StatusCodes.Status201Created);

        grupo.MapPost("/login", async (LoginCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.EntrarAsync(pedido, ct)).ParaResultado())
            .WithName("Login")
            .WithSummary("Entra com e-mail e senha.")
            .Produces<SessaoResponse>();

        grupo.MapPost("/google", async (LoginGoogleCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.EntrarComGoogleAsync(pedido, ct)).ParaResultado())
            .WithName("LoginGoogle")
            .WithSummary("Entra com a credencial do botao do Google.")
            .Produces<SessaoResponse>();

        grupo.MapPost("/renovar", async (RenovarSessaoCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.RenovarAsync(pedido, ct)).ParaResultado())
            .WithName("RenovarSessao")
            .WithSummary("Troca o token de renovacao por um par novo. O antigo deixa de valer.")
            .Produces<SessaoResponse>();

        grupo.MapPost("/sair", async (EncerrarSessaoCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.SairAsync(pedido, ct)).ParaResultado())
            .WithName("Sair")
            .WithSummary("Logout: revoga o token de renovacao.");

        grupo.MapPost("/esqueci-senha", async (SolicitarRedefinicaoSenhaCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.EsqueciSenhaAsync(pedido, ct)).ParaResultado())
            .WithName("EsqueciSenha")
            .WithSummary("Manda o link de redefinicao por e-mail, se houver cadastro.");

        grupo.MapPost("/redefinir-senha", async (RedefinirSenhaCommand pedido, IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.RedefinirSenhaAsync(pedido, ct)).ParaResultado())
            .WithName("RedefinirSenha")
            .WithSummary("Define a senha nova com o token do e-mail.");

        grupo.MapGet("/config", async (IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.ConfiguracaoAsync(ct)).ParaResultado())
            .WithName("ConfiguracaoAuth")
            .WithSummary("Client ID do Google para a tela de login; vazio esconde o botao.")
            .Produces<ConfiguracaoAuthResponse>();

        // Fora do grupo: o AllowAnonymous do grupo venceria a policy.
        rotas.MapGet("/api/auth/eu", async (IAutenticacaoService auth, CancellationToken ct) =>
                (await auth.UsuarioAtualAsync(ct)).ParaResultado())
            .WithTags("Autenticacao")
            .WithName("UsuarioAtual")
            .WithSummary("O dono do token, com a conta e a role.")
            .RequireAuthorization(Politicas.ClienteOuAdmin)
            .Produces<UsuarioResponse>();

        return rotas;
    }
}
