using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Queries.ObterUsuarioAtual;

/// <summary>Quem e o dono do token. Null quando o usuario deixou de existir.</summary>
public sealed record ObterUsuarioAtualQuery : IQuery<UsuarioResponse?>
{
    public required Guid UsuarioId { get; init; }
}
