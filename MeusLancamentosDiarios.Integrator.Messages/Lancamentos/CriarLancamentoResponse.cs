using MeusLancamentosDiarios.Integrator.Common;

namespace MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

public sealed record CriarLancamentoResponse
{
    public required Guid Id { get; init; }

    public required string ContaId { get; init; }

    /// <summary>Posicao atribuida dentro da conta, comecando em 1.</summary>
    public required long Sequencia { get; init; }

    public required TipoLancamentoEnum Tipo { get; init; }

    public required decimal Valor { get; init; }

    public required DateOnly DataLancamento { get; init; }

    /// <summary>Carimbo do sistema. Distinto da data de lancamento.</summary>
    public required DateTimeOffset DataRegistro { get; init; }

    public string? Observacao { get; init; }

    /// <summary>
    /// Enfileirado quando a publicacao imediata deu certo; Cadastrado ou Lido quando
    /// ficou para a varredura. Nunca Consolidado: a consolidacao e assincrona.
    /// </summary>
    public required StageLancamentoEnum Stage { get; init; }
}
