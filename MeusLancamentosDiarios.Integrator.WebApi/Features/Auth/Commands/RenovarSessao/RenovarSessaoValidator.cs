using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.RenovarSessao;

public sealed class RenovarSessaoValidator : AbstractValidator<RenovarSessaoCommand>
{
    public RenovarSessaoValidator()
    {
        RuleFor(x => x.TokenRenovacao).NotEmpty().WithMessage("Token de renovacao ausente.");
    }
}
