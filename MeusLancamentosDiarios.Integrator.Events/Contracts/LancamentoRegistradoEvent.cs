using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Events.Contracts;

/// <summary>
/// Contrato publicado no Pub/Sub quando um lancamento e registrado.
/// Serializado como JSON no corpo da mensagem.
/// </summary>
public sealed record LancamentoRegistradoEvent
{
    public required Guid LancamentoId { get; init; }

    /// <summary>Vira a ordering key da mensagem.</summary>
    public required string ContaId { get; init; }

    /// <summary>
    /// Posicao dentro da conta. Vai no corpo, nao na ordering key: a chave agrupa
    /// o que deve ser ordenado, e por isso precisa ser igual para todas as mensagens
    /// da conta. Uma chave por mensagem desligaria a ordenacao.
    /// </summary>
    public required long Sequencia { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    /// <summary>Valor em centavos — inteiro, para nao depender de ponto flutuante.</summary>
    public required long ValorCentavos { get; init; }

    /// <summary>Data informada pelo usuario (competencia), no formato yyyy-MM-dd.</summary>
    public required DateOnly DataLancamento { get; init; }

    /// <summary>Carimbo do sistema no momento do cadastro.</summary>
    public required DateTimeOffset DataRegistro { get; init; }

    public string? Observacao { get; init; }
}
