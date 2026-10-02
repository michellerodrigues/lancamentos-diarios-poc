using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Queries.ObterUsuarioAtual;

public sealed class ObterUsuarioAtualHandler(IUsuarioRepository repositorio)
    : IQueryHandler<ObterUsuarioAtualQuery, UsuarioResponse?>
{
    public async Task<UsuarioResponse?> HandleAsync(
        ObterUsuarioAtualQuery consulta, CancellationToken cancellationToken)
    {
        var usuario = await repositorio.ObterPorIdAsync(consulta.UsuarioId, cancellationToken);

        return usuario is null ? null : usuario.ToResponse();
    }
}
