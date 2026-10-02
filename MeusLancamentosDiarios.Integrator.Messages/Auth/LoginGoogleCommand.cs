using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

public sealed record LoginGoogleCommand : ICommand<SessaoResponse>
{
    /// <summary>O ID token que o botao do Google entrega ao front (campo "credential").</summary>
    public required string Credencial { get; init; }
}
