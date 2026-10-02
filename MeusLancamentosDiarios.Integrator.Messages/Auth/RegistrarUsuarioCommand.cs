using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Auth;

/// <summary>Cadastro com e-mail e senha. Ja devolve a sessao aberta: cadastrou, entrou.</summary>
public sealed record RegistrarUsuarioCommand : ICommand<SessaoResponse>
{
    public required string Nome { get; init; }

    public required string Email { get; init; }

    public required string Senha { get; init; }
}
