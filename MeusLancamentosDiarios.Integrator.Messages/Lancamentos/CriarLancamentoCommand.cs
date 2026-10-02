using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

/// <summary>Referencia de conta: o filtro de posse confere a conta antes do handler.</summary>
public sealed record CriarLancamentoCommand : ICommand<CriarLancamentoResponse>, IReferenciaConta
{
    /// <summary>Conta a que o lancamento pertence. E a unidade de ordenacao do fluxo.</summary>
    public required string ContaId { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    /// <summary>Valor em reais, sempre positivo. O sinal vem do <see cref="Tipo"/>.</summary>
    public required decimal Valor { get; init; }

    /// <summary>Data de competencia informada pelo usuario.</summary>
    public required DateOnly DataLancamento { get; init; }

    public string? Observacao { get; init; }
}
