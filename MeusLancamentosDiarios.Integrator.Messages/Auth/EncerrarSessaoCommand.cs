using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

/// <summary>Logout. O resultado nao importa ao chamador: o endpoint responde 204 sempre.</summary>
public sealed record EncerrarSessaoCommand : ICommand<bool>
{
    public required string TokenRenovacao { get; init; }
}
