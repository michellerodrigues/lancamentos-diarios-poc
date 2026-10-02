using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.Login;

/// <summary>
/// So presenca. A regra de forca da senha nao entra aqui: ela vale para senha nova,
/// e quem tem senha antiga fora da regra ainda precisa conseguir entrar.
/// </summary>
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}
