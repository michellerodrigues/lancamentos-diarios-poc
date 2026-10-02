using System.Security.Claims;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Queries.ObterUsuarioAtual;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder rotas)
    {
        // Publico: e por aqui que se consegue o token.
        var grupo = rotas.MapGroup("/auth").WithTags("Autenticacao").AllowAnonymous();

        grupo.MapPost("/registrar", async (
                RegistrarUsuarioCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                Results.Created("/auth/eu", await dispatcher.EnviarAsync(comando, cancellationToken)))
            .WithName("Registrar")
            .WithSummary("Cadastro com e-mail e senha. Cria a conta do usuario e ja devolve a sessao.")
            .Produces<SessaoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/login", async (
                LoginCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                Results.Ok(await dispatcher.EnviarAsync(comando, cancellationToken)))
            .WithName("Login")
            .WithSummary("Entra com e-mail e senha.")
            .Produces<SessaoResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/google", async (
                LoginGoogleCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                Results.Ok(await dispatcher.EnviarAsync(comando, cancellationToken)))
            .WithName("LoginGoogle")
            .WithSummary("Troca o ID token do Google pela sessao. Cadastra no primeiro acesso.")
            .Produces<SessaoResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/renovar", async (
                RenovarSessaoCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                Results.Ok(await dispatcher.EnviarAsync(comando, cancellationToken)))
            .WithName("RenovarSessao")
            .WithSummary("Troca o token de renovacao por um par novo. O antigo deixa de valer.")
            .Produces<SessaoResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/sair", async (
                EncerrarSessaoCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                await dispatcher.EnviarAsync(comando, cancellationToken);
                return Results.NoContent();
            })
            .WithName("Sair")
            .WithSummary("Logout: revoga o token de renovacao.");

        grupo.MapPost("/esqueci-senha", async (
                SolicitarRedefinicaoSenhaCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                await dispatcher.EnviarAsync(comando, cancellationToken);

                // Mesma resposta com ou sem cadastro: nao conta quais e-mails existem.
                return Results.Accepted();
            })
            .WithName("EsqueciSenha")
            .WithSummary("Manda o link de redefinicao, se o e-mail tiver cadastro. Local: veja no Mailpit (:8025).")
            .ProducesValidationProblem();

        grupo.MapPost("/redefinir-senha", async (
                RedefinirSenhaCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                await dispatcher.EnviarAsync(comando, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RedefinirSenha")
            .WithSummary("Define a senha nova com o token do e-mail. Encerra todas as sessoes abertas.")
            .ProducesValidationProblem();

        grupo.MapGet("/config", (IOptions<AuthOptions> options) =>
                Results.Ok(new ConfiguracaoAuthResponse { GoogleClientId = options.Value.Google.ClientId }))
            .WithName("ConfiguracaoAuth")
            .WithSummary("O que a tela de login precisa saber: o Client ID do Google, vazio se desligado.")
            .Produces<ConfiguracaoAuthResponse>();

        // Fora do grupo: o AllowAnonymous do grupo venceria a policy.
        rotas.MapGet("/auth/eu", async (
                ClaimsPrincipal usuario, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                var atual = await dispatcher.ConsultarAsync(
                    new ObterUsuarioAtualQuery { UsuarioId = usuario.UsuarioId() }, cancellationToken);

                return atual is null ? Results.Unauthorized() : Results.Ok(atual);
            })
            .WithTags("Autenticacao")
            .WithName("UsuarioAtual")
            .WithSummary("O dono do token, com a conta e a role.")
            .RequireAuthorization(Politicas.ClienteOuAdmin)
            .Produces<UsuarioResponse>();

        return rotas;
    }
}
