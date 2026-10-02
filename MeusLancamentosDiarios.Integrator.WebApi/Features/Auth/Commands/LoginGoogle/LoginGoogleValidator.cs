using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.LoginGoogle;

public sealed class LoginGoogleValidator : AbstractValidator<LoginGoogleCommand>
{
    public LoginGoogleValidator()
    {
        RuleFor(x => x.Credencial).NotEmpty().WithMessage("Credencial do Google ausente.");
    }
}
