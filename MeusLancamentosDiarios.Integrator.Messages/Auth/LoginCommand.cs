using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

public sealed record LoginCommand : ICommand<SessaoResponse>
{
    public required string Email { get; init; }

    public required string Senha { get; init; }
}
