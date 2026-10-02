using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.TestSupport.Construtores;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ObterSaldo;
using Microsoft.Extensions.Options;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features;

public sealed class ObterSaldoHandlerTests
{
    public abstract class CenarioDoSaldo : Cenario
    {
        protected readonly Mock<ILancamentoRepository> Repositorio = new(MockBehavior.Strict);

        /// <summary>O mesmo valor do appsettings.json: o codigo nao tem padrao para ele.</summary>
        protected const int LimitePadrao = 20;

        protected SaldoResponse Resposta = null!;

        protected ObterSaldoQuery Consulta { get; set; } = new() { ContaId = "ABC1234" };

        protected void DadoQueOSaldoDaContaE(
            string conta,
            long consolidadoCentavos,
            long creditosPendentes = 0,
            long debitosPendentes = 0,
            int quantidadePendentes = 0,
            long ultimaSequencia = 0) =>
            Repositorio
                .Setup(r => r.ObterSaldoAsync(conta, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SaldoAtual
                {
                    ContaId = conta,
                    SaldoConsolidadoCentavos = consolidadoCentavos,
                    UltimaSequenciaConsolidada = ultimaSequencia,
                    AtualizadoEm = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero),
                    CreditosPendentesCentavos = creditosPendentes,
                    DebitosPendentesCentavos = debitosPendentes,
                    QuantidadePendentes = quantidadePendentes
                });

        protected void DadoQueOsPendentesSao(string conta, params LancamentoEntity[] pendentes) =>
            Repositorio
                .Setup(r => r.ListarPendentesAsync(conta, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pendentes);

        protected override async Task When()
        {
            var handler = new ObterSaldoHandler(
                Repositorio.Object,
                Options.Create(new SaldoOptions { LimitePendentesPadrao = LimitePadrao }));
            Resposta = await handler.HandleAsync(Consulta, CancellationToken.None);
        }
    }

    public sealed class QuandoAContaTemSaldoEPendentes : CenarioDoSaldo
    {
        protected override void Case()
        {
            // R$ 500,00 consolidados; R$ 100,00 a crédito e R$ 30,00 a débito em trânsito.
            DadoQueOSaldoDaContaE("ABC1234",
                consolidadoCentavos: 50_000,
                creditosPendentes: 10_000,
                debitosPendentes: 3_000,
                quantidadePendentes: 2,
                ultimaSequencia: 3);

            DadoQueOsPendentesSao("ABC1234",
                Dado.UmLancamento().ComSequencia(4).DoTipo(TipoLancamentoEnum.Credito)
                    .ComValorEmCentavos(10_000).NoEstagio(StageLancamentoEnum.Enfileirado).Build(),
                Dado.UmLancamento().ComSequencia(5).DoTipo(TipoLancamentoEnum.Debito)
                    .ComValorEmCentavos(3_000).NoEstagio(StageLancamentoEnum.Cadastrado).Build());
        }

        [Fact]
        public void Entao_converte_o_consolidado_para_reais() =>
            Resposta.SaldoConsolidado.Should().Be(500.00m);

        [Fact]
        public void Entao_projeta_o_saldo_somando_o_que_esta_em_transito() =>
            // 500 + 100 − 30. É o número que a tela mostra como "saldo projetado".
            Resposta.SaldoProjetado.Should().Be(570.00m);

        [Fact]
        public void Entao_separa_creditos_e_debitos_pendentes()
        {
            Resposta.CreditosPendentes.Should().Be(100.00m);
            Resposta.DebitosPendentes.Should().Be(30.00m);
        }

        [Fact]
        public void Entao_sinaliza_consolidacao_em_andamento() =>
            Resposta.ConsolidacaoEmAndamento.Should().BeTrue();

        [Fact]
        public void Entao_informa_ate_onde_o_saldo_esta_em_dia() =>
            Resposta.UltimaSequenciaConsolidada.Should().Be(3);

        [Fact]
        public void Entao_detalha_cada_pendente_com_sequencia_e_estagio()
        {
            Resposta.Pendentes.Should().HaveCount(2);

            Resposta.Pendentes[0].Sequencia.Should().Be(4);
            Resposta.Pendentes[0].Valor.Should().Be(100.00m);
            Resposta.Pendentes[0].Stage.Should().Be(StageLancamentoEnum.Enfileirado);

            Resposta.Pendentes[1].Sequencia.Should().Be(5);
            Resposta.Pendentes[1].Stage.Should().Be(StageLancamentoEnum.Cadastrado);
        }
    }

    public sealed class QuandoNaoHaNadaEmTransito : CenarioDoSaldo
    {
        protected override void Case()
        {
            DadoQueOSaldoDaContaE("ABC1234", consolidadoCentavos: 50_000, ultimaSequencia: 3);
            DadoQueOsPendentesSao("ABC1234");
        }

        [Fact]
        public void Entao_o_projetado_iguala_o_consolidado() =>
            Resposta.SaldoProjetado.Should().Be(Resposta.SaldoConsolidado);

        [Fact]
        public void Entao_desliga_o_aviso_de_consolidacao() =>
            Resposta.ConsolidacaoEmAndamento.Should().BeFalse();

        [Fact]
        public void Entao_a_lista_de_pendentes_vem_vazia() =>
            Resposta.Pendentes.Should().BeEmpty();
    }

    public sealed class QuandoAChamadaNaoInformaOLimite : CenarioDoSaldo
    {
        protected override void Case()
        {
            DadoQueOSaldoDaContaE("ABC1234", consolidadoCentavos: 0);
            DadoQueOsPendentesSao("ABC1234");
        }

        [Fact]
        public void Entao_detalha_ate_o_limite_padrao_do_appsettings() =>
            Repositorio.Verify(
                r => r.ListarPendentesAsync("ABC1234", LimitePadrao, It.IsAny<CancellationToken>()),
                Times.Once);
    }

    public sealed class QuandoAChamadaInformaOLimite : CenarioDoSaldo
    {
        protected override void Case()
        {
            Consulta = Consulta with { LimitePendentes = 5 };

            DadoQueOSaldoDaContaE("ABC1234", consolidadoCentavos: 0);
            DadoQueOsPendentesSao("ABC1234");
        }

        [Fact]
        public void Entao_o_limite_informado_vence_o_padrao() =>
            Repositorio.Verify(
                r => r.ListarPendentesAsync("ABC1234", 5, It.IsAny<CancellationToken>()),
                Times.Once);
    }

    public sealed class QuandoAContaVemEmMinusculas : CenarioDoSaldo
    {
        protected override void Case()
        {
            Consulta = Consulta with { ContaId = "  abc1234 " };

            // O repositório só é consultado com a forma normalizada.
            DadoQueOSaldoDaContaE("ABC1234", consolidadoCentavos: 0);
            DadoQueOsPendentesSao("ABC1234");
        }

        [Fact]
        public void Entao_normaliza_antes_de_consultar() =>
            Repositorio.Verify(
                r => r.ObterSaldoAsync("ABC1234", It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_a_resposta_traz_a_conta_normalizada() =>
            Resposta.ContaId.Should().Be("ABC1234");
    }

    public sealed class QuandoAContaNuncaTeveLancamento : CenarioDoSaldo
    {
        protected override void Case()
        {
            Repositorio
                .Setup(r => r.ObterSaldoAsync("NOVA1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SaldoAtual
                {
                    ContaId = "NOVA1",
                    SaldoConsolidadoCentavos = 0,
                    UltimaSequenciaConsolidada = 0,
                    AtualizadoEm = null,
                    CreditosPendentesCentavos = 0,
                    DebitosPendentesCentavos = 0,
                    QuantidadePendentes = 0
                });

            DadoQueOsPendentesSao("NOVA1");

            Consulta = Consulta with { ContaId = "NOVA1" };
        }

        [Fact]
        public void Entao_devolve_saldo_zero_em_vez_de_erro() =>
            Resposta.SaldoConsolidado.Should().Be(0m);

        [Fact]
        public void Entao_a_ultima_atualizacao_vem_nula() =>
            // Não há linha no read model: a tela mostra "nunca", não uma data falsa.
            Resposta.AtualizadoEm.Should().BeNull();
    }
}
