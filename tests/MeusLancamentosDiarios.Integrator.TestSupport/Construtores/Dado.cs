using FizzWare.NBuilder;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Data;

namespace MeusLancamentosDiarios.Integrator.TestSupport.Construtores;

/// <summary>
/// Ponto de entrada dos construtores de cenário. Lido no teste como frase:
/// <c>Dado.UmLancamento().NaConta("ABC1234").ComSequencia(2).Build()</c>
/// </summary>
public static class Dado
{
    public static LancamentoBuilder UmLancamento() => new();

    /// <summary>
    /// Vários lançamentos de uma conta, com sequências 1..n já atribuídas —
    /// o formato em que a varredura do relay os entrega.
    /// </summary>
    public static IReadOnlyList<LancamentoEntity> LancamentosDaConta(string conta, int quantidade) =>
        Builder<LancamentoEntity>.CreateListOfSize(quantidade)
            .All()
            .With(x => x.Id = Guid.CreateVersion7())
            .With(x => x.ContaId = conta)
            .With(x => x.Tipo = TipoLancamentoEnum.Credito)
            .With(x => x.ValorCentavos = 10_000)
            .With(x => x.DataLancamento = LancamentoBuilder.DataPadrao)
            .With(x => x.DataRegistro = LancamentoBuilder.RegistroPadrao)
            .With(x => x.Stage = StageLancamentoEnum.Lido)
            .With(x => x.TentativasEnvio = 0)
            .Do((x, indice) => x.Sequencia = indice + 1)
            .Build()
            .ToList();
}

/// <summary>
/// Construtor de <see cref="LancamentoEntity"/>. Os padrões descrevem o caso comum —
/// crédito de R$ 100,00, conta ABC1234, sequência 1, recém-cadastrado — e cada
/// cenário sobrescreve apenas o que for relevante para ele.
/// </summary>
public sealed class LancamentoBuilder
{
    public const string ContaPadrao = "ABC1234";

    public static readonly DateOnly DataPadrao = new(2026, 9, 21);

    public static readonly DateTimeOffset RegistroPadrao =
        new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly LancamentoEntity _lancamento = Builder<LancamentoEntity>.CreateNew()
        .With(x => x.Id = Guid.CreateVersion7())
        .With(x => x.ContaId = ContaPadrao)
        .With(x => x.Sequencia = 1)
        .With(x => x.Tipo = TipoLancamentoEnum.Credito)
        .With(x => x.ValorCentavos = 10_000)
        .With(x => x.DataLancamento = DataPadrao)
        .With(x => x.DataRegistro = RegistroPadrao)
        .With(x => x.Observacao = null)
        .With(x => x.Stage = StageLancamentoEnum.Cadastrado)
        .With(x => x.StageAtualizadoEm = RegistroPadrao)
        .With(x => x.TentativasEnvio = 0)
        .With(x => x.TentativasProcessamento = 0)
        .With(x => x.ProximaTentativaEm = null)
        .With(x => x.MensagemId = null)
        .With(x => x.Erro = null)
        .Build();

    public LancamentoBuilder ComId(Guid id)
    {
        _lancamento.Id = id;
        return this;
    }

    public LancamentoBuilder NaConta(string conta)
    {
        _lancamento.ContaId = conta;
        return this;
    }

    public LancamentoBuilder ComSequencia(long sequencia)
    {
        _lancamento.Sequencia = sequencia;
        return this;
    }

    public LancamentoBuilder DoTipo(TipoLancamentoEnum tipo)
    {
        _lancamento.Tipo = tipo;
        return this;
    }

    public LancamentoBuilder ComValorEmCentavos(long centavos)
    {
        _lancamento.ValorCentavos = centavos;
        return this;
    }

    public LancamentoBuilder NoEstagio(StageLancamentoEnum stage)
    {
        _lancamento.Stage = stage;
        return this;
    }

    public LancamentoBuilder ComTentativasDeEnvio(int tentativas)
    {
        _lancamento.TentativasEnvio = tentativas;
        return this;
    }

    public LancamentoBuilder ComObservacao(string? observacao)
    {
        _lancamento.Observacao = observacao;
        return this;
    }

    public LancamentoEntity Build() => _lancamento;
}
