using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Events.Data;

/// <summary>Uma linha da tabela LancamentosDiarios.</summary>
public sealed class LancamentoEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Conta a que o lancamento pertence. E a unidade de ordenacao: vira a ordering
    /// key no Pub/Sub, entao lancamentos da mesma conta sao entregues em ordem e um
    /// de cada vez, enquanto contas diferentes seguem em paralelo.
    /// </summary>
    public string ContaId { get; set; } = string.Empty;

    /// <summary>
    /// Numero sequencial DENTRO da conta, comecando em 1. E o que define a ordem
    /// correta e o que permite detectar buraco: se a sequencia 2 esta pronta para
    /// publicar mas a 1 ainda nao saiu, a 2 espera.
    /// </summary>
    public long Sequencia { get; set; }

    public TipoLancamentoEnum Tipo { get; set; }

    /// <summary>Valor absoluto em centavos. O sinal vem do <see cref="Tipo"/>.</summary>
    public long ValorCentavos { get; set; }

    public DateOnly DataLancamento { get; set; }

    public DateTimeOffset DataRegistro { get; set; }

    public string? Observacao { get; set; }

    public StageLancamentoEnum Stage { get; set; }

    public DateTimeOffset StageAtualizadoEm { get; set; }

    /// <summary>Tentativas de publicacao no Pub/Sub, contadas pelo Events.</summary>
    public int TentativasEnvio { get; set; }

    /// <summary>Tentativas de consolidacao, contadas pelo Consumer.</summary>
    public int TentativasProcessamento { get; set; }

    /// <summary>
    /// Momento a partir do qual vale tentar publicar de novo. Null quando nunca
    /// falhou. E o que evita queimar todas as tentativas numa indisponibilidade
    /// curta do broker.
    /// </summary>
    public DateTimeOffset? ProximaTentativaEm { get; set; }

    public string? MensagemId { get; set; }

    public string? Erro { get; set; }

    /// <summary>Quanto esta linha soma ao saldo: positivo para credito, negativo para debito.</summary>
    public long DeltaCentavos => Tipo == TipoLancamentoEnum.Debito ? -ValorCentavos : ValorCentavos;
}
