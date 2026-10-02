using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamento;

public sealed class CriarLancamentoValidator : AbstractValidator<CriarLancamentoCommand>
{
    /// <summary>Teto de sanidade: barra digitacao errada, nao regra de negocio.</summary>
    public const decimal ValorMaximo = 1_000_000m;

    public const int TamanhoMinimoConta = 3;

    public const int TamanhoMaximoConta = 20;

    public const int TamanhoMaximoObservacao = 200;

    /// <summary>Letras, digitos e hifen. Sem espaco: a conta vira ordering key no Pub/Sub.</summary>
    public const string PadraoConta = "^[A-Za-z0-9-]+$";

    private static readonly TimeZoneInfo FusoBrasil =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public CriarLancamentoValidator(TimeProvider relogio)
    {
        RuleFor(x => x.ContaId)
            .NotEmpty().WithMessage("Informe a conta.")
            .MinimumLength(TamanhoMinimoConta)
                .WithMessage($"A conta deve ter ao menos {TamanhoMinimoConta} caracteres.")
            .MaximumLength(TamanhoMaximoConta)
                .WithMessage($"A conta aceita no maximo {TamanhoMaximoConta} caracteres.")
            .Matches(PadraoConta)
                .WithMessage("A conta aceita apenas letras, numeros e hifen.");

        RuleFor(x => x.Tipo)
            .IsInEnum()
            .WithMessage("Informe se o lancamento e credito ou debito.");

        RuleFor(x => x.Valor)
            .GreaterThan(0).WithMessage("O valor deve ser maior que zero.")
            .LessThanOrEqualTo(ValorMaximo)
                .WithMessage($"O valor nao pode passar de {ValorMaximo:N2}.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .WithMessage("O valor aceita no maximo duas casas decimais.");

        RuleFor(x => x.DataLancamento)
            .NotEmpty().WithMessage("Informe a data do lancamento.")
            .Must(data => data <= HojeNoBrasil(relogio))
                .WithMessage("A data do lancamento nao pode ser futura.");

        RuleFor(x => x.Observacao)
            .MaximumLength(TamanhoMaximoObservacao)
            .WithMessage($"A observacao aceita no maximo {TamanhoMaximoObservacao} caracteres.");
    }

    /// <summary>
    /// "Hoje" no fuso de Sao Paulo, nao em UTC: as 21h de Brasilia ja e o dia
    /// seguinte em UTC, e um lancamento do dia seria recusado como futuro.
    /// </summary>
    private static DateOnly HojeNoBrasil(TimeProvider relogio) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), FusoBrasil).DateTime);
}
