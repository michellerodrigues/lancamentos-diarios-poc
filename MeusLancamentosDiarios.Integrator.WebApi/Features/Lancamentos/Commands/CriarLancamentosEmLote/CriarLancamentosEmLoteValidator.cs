using FluentValidation;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamento;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamentosEmLote;

public sealed class CriarLancamentosEmLoteValidator : AbstractValidator<CriarLancamentosEmLoteCommand>
{
    /// <summary>O lote entra numa transacao so; sem teto, uma lista enorme seguraria a tabela.</summary>
    public const int MaximoItens = 1_000;

    public CriarLancamentosEmLoteValidator(TimeProvider relogio)
    {
        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("Informe ao menos um lancamento.")
            .Must(itens => itens.Count <= MaximoItens)
                .WithMessage($"O lote aceita no maximo {MaximoItens} lancamentos.");

        // As mesmas regras do lancamento avulso. O erro aponta o item: "Itens[3].Valor".
        RuleForEach(x => x.Itens).SetValidator(new CriarLancamentoValidator(relogio));
    }
}
