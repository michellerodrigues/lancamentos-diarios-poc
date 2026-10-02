using FluentValidation;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;

/// <summary>A mesma regra no cadastro e na redefinicao.</summary>
public static class RegrasDeSenha
{
    public const int TamanhoMinimo = 8;

    /// <summary>Teto contra senha gigante fazendo o PBKDF2 trabalhar a toa.</summary>
    public const int TamanhoMaximo = 128;

    public static IRuleBuilderOptions<T, string> SenhaForte<T>(this IRuleBuilderInitial<T, string> regra) =>
        regra
            // Para no primeiro erro: "informe a senha" ja explica o resto.
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(TamanhoMinimo)
                .WithMessage($"A senha deve ter ao menos {TamanhoMinimo} caracteres.")
            .MaximumLength(TamanhoMaximo)
                .WithMessage($"A senha aceita no maximo {TamanhoMaximo} caracteres.")
            .Matches("[A-Za-z]").WithMessage("A senha precisa de ao menos uma letra.")
            .Matches("[0-9]").WithMessage("A senha precisa de ao menos um numero.");
}
