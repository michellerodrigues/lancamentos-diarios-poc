using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

public sealed record RedefinirSenhaCommand : ICommand<bool>
{
    /// <summary>O token que veio no link do e-mail.</summary>
    public required string Token { get; init; }

    public required string NovaSenha { get; init; }
}
