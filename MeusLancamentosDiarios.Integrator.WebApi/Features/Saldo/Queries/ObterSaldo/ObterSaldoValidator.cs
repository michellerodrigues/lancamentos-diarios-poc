using FluentValidation;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ObterSaldo;

public sealed class ObterSaldoValidator : AbstractValidator<ObterSaldoQuery>
{
    public ObterSaldoValidator()
    {
        // Zero ou negativo chegaria ao TOP (@Limite) do SQL e voltaria como 500.
        RuleFor(x => x.LimitePendentes)
            .GreaterThan(0).WithMessage("O limite de pendentes deve ser maior que zero.")
            .When(x => x.LimitePendentes.HasValue);
    }
}
