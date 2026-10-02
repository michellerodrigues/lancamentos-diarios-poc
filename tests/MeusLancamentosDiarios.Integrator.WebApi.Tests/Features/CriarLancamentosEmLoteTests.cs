using FluentAssertions;
using FluentValidation.TestHelper;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamentosEmLote;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features;

public sealed class CriarLancamentosEmLoteTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

    private static CriarLancamentoCommand Item(string conta, decimal valor, TipoLancamentoEnum tipo = TipoLancamentoEnum.Credito) => new()
    {
        ContaId = conta,
        Tipo = tipo,
        Valor = valor,
        DataLancamento = new DateOnly(2026, 10, 1)
    };

    // ============================================= handler

    public sealed class QuandoOLoteMisturaContas : Cenario
    {
        private readonly Mock<ILancamentoRepository> _repositorio = new(MockBehavior.Strict);

        private IReadOnlyList<LancamentoEntity> _gravados = [];

        private CriarLancamentosEmLoteResponse _resultado = null!;

        protected override void Case() =>
            // Simula o banco: sequencia por conta, na ordem da lista.
            _repositorio
                .Setup(r => r.InserirLoteAsync(It.IsAny<IReadOnlyList<LancamentoEntity>>(), It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<LancamentoEntity>, CancellationToken>((lancamentos, _) =>
                {
                    _gravados = lancamentos;

                    foreach (var grupo in lancamentos.GroupBy(l => l.ContaId))
                    {
                        var sequencia = 10;
                        foreach (var l in grupo) l.Sequencia = ++sequencia;
                    }
                })
                .Returns(Task.CompletedTask);

        protected override async Task When()
        {
            var handler = new CriarLancamentosEmLoteHandler(
                _repositorio.Object,
                new FakeTimeProvider(Agora),
                NullLogger<CriarLancamentosEmLoteHandler>.Instance);

            _resultado = await handler.HandleAsync(new CriarLancamentosEmLoteCommand
            {
                Itens =
                [
                    Item("abc1234 ", 100m),
                    Item("XYZ9999", 50m, TipoLancamentoEnum.Debito),
                    Item("ABC1234", 25.5m)
                ]
            }, CancellationToken.None);
        }

        [Fact]
        public void Entao_grava_tudo_numa_chamada_so() =>
            // Uma chamada, uma transacao: o lote entra inteiro ou nao entra.
            _repositorio.Verify(
                r => r.InserirLoteAsync(It.IsAny<IReadOnlyList<LancamentoEntity>>(), It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_normaliza_a_conta_como_o_lancamento_avulso() =>
            _gravados.Select(l => l.ContaId).Should().Equal("ABC1234", "XYZ9999", "ABC1234");

        [Fact]
        public void Entao_converte_o_valor_para_centavos() =>
            _gravados.Select(l => l.ValorCentavos).Should().Equal(10_000, 5_000, 2_550);

        [Fact]
        public void Entao_devolve_as_sequencias_na_ordem_dos_itens() =>
            _resultado.Lancamentos.Select(l => (l.ContaId, l.Sequencia))
                .Should().Equal(("ABC1234", 11L), ("XYZ9999", 11L), ("ABC1234", 12L));

        [Fact]
        public void Entao_deixa_a_publicacao_para_o_relay() =>
            // O dublê estrito recusaria qualquer outra chamada ao repositorio — nenhuma
            // reserva, nenhuma publicacao no mesmo request.
            _resultado.Lancamentos.Should().AllSatisfy(l => l.Stage.Should().Be(StageLancamentoEnum.Cadastrado));
    }

    // ============================================= validator

    public sealed class QuandoValidaOLote
    {
        private static CriarLancamentosEmLoteValidator Validator() => new(new FakeTimeProvider(Agora));

        [Fact]
        public void Entao_recusa_lista_vazia() =>
            Validator().TestValidate(new CriarLancamentosEmLoteCommand { Itens = [] })
                .ShouldHaveValidationErrorFor(x => x.Itens);

        [Fact]
        public void Entao_recusa_acima_do_teto()
        {
            var itens = Enumerable.Range(0, CriarLancamentosEmLoteValidator.MaximoItens + 1)
                .Select(_ => Item("ABC1234", 1m))
                .ToList();

            Validator().TestValidate(new CriarLancamentosEmLoteCommand { Itens = itens })
                .ShouldHaveValidationErrorFor(x => x.Itens);
        }

        [Fact]
        public void Entao_aceita_exatamente_o_teto()
        {
            var itens = Enumerable.Range(0, CriarLancamentosEmLoteValidator.MaximoItens)
                .Select(_ => Item("ABC1234", 1m))
                .ToList();

            Validator().TestValidate(new CriarLancamentosEmLoteCommand { Itens = itens })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Entao_o_erro_de_um_item_aponta_qual_item()
        {
            // Mesmas regras do avulso: data futura nao entra, nem em lote.
            var futuro = Item("ABC1234", 10m) with { DataLancamento = new DateOnly(2026, 10, 6) };

            var resultado = Validator().TestValidate(new CriarLancamentosEmLoteCommand
            {
                Itens = [Item("ABC1234", 10m), futuro]
            });

            resultado.ShouldHaveValidationErrorFor("Itens[1].DataLancamento");
        }
    }
}
