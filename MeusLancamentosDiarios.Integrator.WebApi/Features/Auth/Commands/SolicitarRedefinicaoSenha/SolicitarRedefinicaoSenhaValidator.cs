using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.SolicitarRedefinicaoSenha;

public sealed class SolicitarRedefinicaoSenhaValidator : AbstractValidator<SolicitarRedefinicaoSenhaCommand>
{
    public SolicitarRedefinicaoSenhaValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail.")
            .EmailAddress().WithMessage("Informe um e-mail valido.");
    }
}
