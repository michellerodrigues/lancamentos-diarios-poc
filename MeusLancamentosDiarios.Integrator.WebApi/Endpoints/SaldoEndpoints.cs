using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ListarContas;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ObterSaldo;

namespace MeusLancamentosDiarios.Integrator.WebApi.Endpoints;

public static class SaldoEndpoints
{
    public static IEndpointRouteBuilder MapSaldo(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/saldo/{contaId}", async (
                string contaId,
                IDispatcher dispatcher,
                CancellationToken cancellationToken,
                int? limitePendentes) =>
                Results.Ok(await dispatcher.ConsultarAsync(
                    new ObterSaldoQuery { ContaId = contaId, LimitePendentes = limitePendentes },
                    cancellationToken)))
            .RequireAuthorization(Politicas.ClienteOuAdmin)
            .ExigirPosseDaConta()
            .WithTags("Saldo")
            .WithName("ObterSaldo")
            .WithSummary("Saldo consolidado da conta, pendentes e saldo projetado.")
            .Produces<SaldoResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        rotas.MapGet("/contas", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                Results.Ok(await dispatcher.ConsultarAsync(new ListarContasQuery(), cancellationToken)))
            .RequireAuthorization(Politicas.SomenteAdmin)
            .WithTags("Saldo")
            .WithName("ListarContas")
            .WithSummary("Contas que ja receberam algum lancamento.")
            .Produces<IReadOnlyList<string>>();

        return rotas;
    }
}
