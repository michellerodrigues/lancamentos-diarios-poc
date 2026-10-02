using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamentosEmLote;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Queries.ObterLancamento;

namespace MeusLancamentosDiarios.Integrator.WebApi.Endpoints;

/// <summary>
/// Cada endpoint so declara: a role vem da policy, a posse da conta do filtro e o
/// trabalho do handler, pelo dispatcher.
/// </summary>
public static class LancamentosEndpoints
{
    public static IEndpointRouteBuilder MapLancamentos(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/lancamentos")
            .WithTags("Lancamentos")
            .RequireAuthorization(Politicas.ClienteOuAdmin);

        grupo.MapPost("/", async (
                CriarLancamentoCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                var resultado = await dispatcher.EnviarAsync(comando, cancellationToken);
                return Results.Created($"/lancamentos/{resultado.Id}", resultado);
            })
            .ExigirPosseDaConta()
            .WithName("CriarLancamento")
            .WithSummary("Registra um lancamento na conta. A consolidacao e assincrona.")
            .Produces<CriarLancamentoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapPost("/lote", async (
                CriarLancamentosEmLoteCommand comando, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                Results.Created((string?)null, await dispatcher.EnviarAsync(comando, cancellationToken)))
            .RequireAuthorization(Politicas.SomenteAdmin)
            .WithName("CriarLancamentosEmLote")
            .WithSummary(
                $"Inclui ate {CriarLancamentosEmLoteValidator.MaximoItens} lancamentos, em contas quaisquer, " +
                "numa transacao so. O relay publica; a tela acompanha a consolidacao.")
            .Produces<CriarLancamentosEmLoteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        grupo.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                ResultadosHttp.OkOuNaoEncontrado(
                    await dispatcher.ConsultarAsync(new ObterLancamentoQuery { Id = id }, cancellationToken)))
            .ExigirPosseDaConta()
            .WithName("ObterLancamento")
            .WithSummary("Consulta um lancamento e em que estagio da esteira ele esta.")
            .Produces<LancamentoDetalheResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return rotas;
    }
}
