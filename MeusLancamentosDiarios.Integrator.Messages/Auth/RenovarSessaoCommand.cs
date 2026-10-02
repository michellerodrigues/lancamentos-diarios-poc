using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

/// <summary>Troca o token de renovacao por um par novo. O antigo deixa de valer.</summary>
public sealed record RenovarSessaoCommand : ICommand<SessaoResponse>
{
    public required string TokenRenovacao { get; init; }
}
