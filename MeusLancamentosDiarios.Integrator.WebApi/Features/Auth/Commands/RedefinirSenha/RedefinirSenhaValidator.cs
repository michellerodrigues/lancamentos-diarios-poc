using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.RedefinirSenha;

/// <summary>
/// Roda antes do handler: senha fraca e recusada sem queimar o token, e o usuario
/// pode tentar de novo com o mesmo link.
/// </summary>
public sealed class RedefinirSenhaValidator : AbstractValidator<RedefinirSenhaCommand>
{
    public RedefinirSenhaValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Link de redefinicao incompleto.");
        RuleFor(x => x.NovaSenha).SenhaForte();
    }
}
