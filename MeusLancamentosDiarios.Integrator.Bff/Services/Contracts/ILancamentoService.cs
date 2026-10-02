using System.Security.Claims;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;

namespace MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;

public interface ILancamentoService
{
    /// <summary>
    /// Inclui o lancamento pedido pela tela. Sem conta informada, usa a do token; com
    /// conta informada, a posse ja foi conferida pelo filtro.
    /// </summary>
    Task<RespostaApi<LancamentoCriadoTela>> IncluirAsync(
        NovoLancamentoRequest pedido, ClaimsPrincipal usuario, CancellationToken cancellationToken);
}
