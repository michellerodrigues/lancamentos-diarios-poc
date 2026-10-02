using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.Registrar;

public sealed class RegistrarUsuarioValidator : AbstractValidator<RegistrarUsuarioCommand>
{
    public const int TamanhoMaximoNome = 100;

    public const int TamanhoMaximoEmail = 254;

    public RegistrarUsuarioValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(TamanhoMaximoNome)
                .WithMessage($"O nome aceita no maximo {TamanhoMaximoNome} caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Informe o e-mail.")
            .MaximumLength(TamanhoMaximoEmail)
                .WithMessage($"O e-mail aceita no maximo {TamanhoMaximoEmail} caracteres.")
            .EmailAddress().WithMessage("Informe um e-mail valido.");

        RuleFor(x => x.Senha).SenhaForte();
    }
}
