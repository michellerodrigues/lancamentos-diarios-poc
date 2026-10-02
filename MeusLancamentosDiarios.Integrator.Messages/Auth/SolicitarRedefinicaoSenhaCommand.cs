using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

/// <summary>"Esqueci a senha". O endpoint responde 202 exista o e-mail ou nao.</summary>
public sealed record SolicitarRedefinicaoSenhaCommand : ICommand<bool>
{
    public required string Email { get; init; }
}
