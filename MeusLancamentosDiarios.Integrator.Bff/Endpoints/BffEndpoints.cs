using System.Security.Claims;
using MeusLancamentosDiarios.Integrator.Bff.Auth;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Auth;

namespace MeusLancamentosDiarios.Integrator.Bff.Endpoints;

/// <summary>
/// Rotas das telas. Cada endpoint so declara: a role vem da policy, a posse da conta
/// do filtro, o trabalho do service e o de-para dos conversores.
/// </summary>
public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBff(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api")
            .WithTags("Telas")
            .RequireAuthorization(Politicas.ClienteOuAdmin);

        grupo.MapGet("/saldo/{contaId}", async (
                string contaId, ISaldoService saldo, CancellationToken cancellationToken) =>
                (await saldo.ObterAsync(contaId, cancellationToken)).ParaResultado())
            .ExigirPosseDaConta()
            .WithName("SaldoDaTela")
            .WithSummary("Tudo que a tela de Saldo Consolidado precisa, num payload so.")
            .Produces<SaldoTela>();

        grupo.MapGet("/contas", async (ISaldoService saldo, CancellationToken cancellationToken) =>
                (await saldo.ListarContasAsync(cancellationToken)).ParaResultado())
            .RequireAuthorization(Politicas.SomenteAdmin)
            .WithName("ListarContas")
            .WithSummary("Contas ja usadas, para o seletor da tela do admin.")
            .Produces<IReadOnlyList<string>>();

        grupo.MapPost("/lancamentos", async (
                NovoLancamentoRequest pedido,
                ClaimsPrincipal usuario,
                ILancamentoService lancamentos,
                CancellationToken cancellationToken) =>
                (await lancamentos.IncluirAsync(pedido, usuario, cancellationToken))
                    .ParaResultado(criado => Results.Created($"/api/lancamentos/{criado.Id}", criado)))
            .ExigirPosseDaConta()
            .WithName("IncluirLancamento")
            .WithSummary("Inclui o lancamento na conta. A consolidacao acontece depois.")
            .Produces<LancamentoCriadoTela>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return rotas;
    }
}
