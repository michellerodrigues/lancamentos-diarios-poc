using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Contracts;

namespace MeusLancamentosDiarios.Integrator.Bff.Contracts;

public sealed record NovoLancamentoRequest : IReferenciaConta
{
    /// <summary>So o admin informa. Para o cliente, o BFF usa a conta do token.</summary>
    public string? ContaId { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    public required decimal Valor { get; init; }

    public required DateOnly DataLancamento { get; init; }

    public string? Observacao { get; init; }
}
