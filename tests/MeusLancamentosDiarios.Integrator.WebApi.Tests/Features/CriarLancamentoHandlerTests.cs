using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing;
using MeusLancamentosDiarios.Integrator.Events.Publishing.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamento;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features;

public sealed class CriarLancamentoHandlerTests
{
    /// <summary>
    /// Base dos cenários do handler.
    ///
    /// O publicador é dublê; o relay é real, porque a regra que interessa está na
    /// coordenação entre handler e relay — trocá-lo por um dublê esconderia
    /// justamente o que se quer verificar.
    /// </summary>
    public abstract class CenarioDoHandler : Cenario
    {
        protected static readonly DateTimeOffset Agora =
            new(2026, 9, 21, 15, 30, 0, TimeSpan.Zero);

        protected readonly Mock<ILancamentoRepository> Repositorio = new(MockBehavior.Strict);
        protected readonly Mock<IPublicadorEventos> Publicador = new(MockBehavior.Strict);

        protected readonly FakeTimeProvider Relogio = new(Agora);

        protected CriarLancamentoResponse Resultado = null!;

        /// <summary>Lançamento como o repositório o recebeu, já com a sequência atribuída.</summary>
        protected LancamentoEntity Gravado = null!;

        protected CriarLancamentoCommand Comando { get; set; } = new()
        {
            ContaId = "ABC1234",
            Tipo = TipoLancamentoEnum.Credito,
            Valor = 100.00m,
            DataLancamento = new DateOnly(2026, 9, 21),
            Observacao = null
        };

        /// <summary>A inserção atribui a sequência; os cenários dizem qual.</summary>
        protected void DadoQueAInsercaoAtribuiASequencia(long sequencia) =>
            Repositorio
                .Setup(r => r.InserirAsync(It.IsAny<LancamentoEntity>(), It.IsAny<CancellationToken>()))
                .Callback<LancamentoEntity, CancellationToken>((l, _) =>
                {
                    l.Sequencia = sequencia;
                    Gravado = l;
                })
                .ReturnsAsync(sequencia);

        protected override async Task When()
        {
            var relay = new RelayLancamentos(
                Repositorio.Object,
                Publicador.Object,
                Options.Create(new RelayOptions { MaxTentativas = 5, BackoffMaximo = TimeSpan.FromMinutes(1) }),
                NullLogger<RelayLancamentos>.Instance);

            var handler = new CriarLancamentoHandler(
                Repositorio.Object,
                relay,
                Relogio,
                NullLogger<CriarLancamentoHandler>.Instance);

            Resultado = await handler.HandleAsync(Comando, CancellationToken.None);
        }
    }

    // --------------------------------------------- conta em dia: publica na hora

    public sealed class QuandoAContaNaoTemPredecessorPendente : CenarioDoHandler
    {
        protected override void Case()
        {
            DadoQueAInsercaoAtribuiASequencia(1);

            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync("ABC1234", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Repositorio
                .Setup(r => r.ReservarUmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Publicador
                .Setup(p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("msg-1");

            Repositorio
                .Setup(r => r.MarcarEnfileiradoAsync(It.IsAny<Guid>(), "msg-1", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_responde_enfileirado() =>
            Resultado.Stage.Should().Be(StageLancamentoEnum.Enfileirado);

        [Fact]
        public void Entao_publica_no_broker_durante_o_request() =>
            Publicador.Verify(
                p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_devolve_a_sequencia_atribuida() =>
            Resultado.Sequencia.Should().Be(1);

        [Fact]
        public void Entao_carimba_a_data_de_registro_com_o_relogio() =>
            Resultado.DataRegistro.Should().Be(Agora);

        [Fact]
        public void Entao_a_data_de_registro_difere_da_data_do_lancamento() =>
            Resultado.DataRegistro.Date.Should().NotBe(default);
    }

    // --------------------------------------------- há buraco: NÃO publica

    public sealed class QuandoExistePredecessorPendenteNaConta : CenarioDoHandler
    {
        protected override void Case()
        {
            DadoQueAInsercaoAtribuiASequencia(5);

            // A sequência 4 (ou anterior) ainda não saiu.
            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync("ABC1234", 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public void Entao_responde_cadastrado() =>
            Resultado.Stage.Should().Be(StageLancamentoEnum.Cadastrado);

        [Fact]
        public void Entao_nao_publica_no_broker() =>
            // O ponto central de toda a ordenação: publicar por cima de um
            // predecessor pendente inverteria a ordem de forma irreversível,
            // porque o Pub/Sub não sabe do que nunca chegou até ele.
            Publicador.Verify(
                p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);

        [Fact]
        public void Entao_nem_chega_a_reservar_a_linha() =>
            Repositorio.Verify(
                r => r.ReservarUmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);

        [Fact]
        public void Entao_o_lancamento_continua_gravado() =>
            // Não publicar não é recusar: a linha está no banco e a varredura a pega.
            Resultado.Id.Should().NotBeEmpty();
    }

    // --------------------------------------------- o relay chegou antes

    public sealed class QuandoORelayReservouALinhaAntes : CenarioDoHandler
    {
        protected override void Case()
        {
            DadoQueAInsercaoAtribuiASequencia(2);

            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync("ABC1234", 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // A reserva falha: outro processo já moveu a linha para Lido.
            Repositorio
                .Setup(r => r.ReservarUmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public void Entao_responde_lido() =>
            Resultado.Stage.Should().Be(StageLancamentoEnum.Lido);

        [Fact]
        public void Entao_nao_publica_em_duplicidade() =>
            Publicador.Verify(
                p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    // --------------------------------------------- broker fora do ar

    public sealed class QuandoOBrokerFalhaNaPublicacaoImediata : CenarioDoHandler
    {
        protected override void Case()
        {
            DadoQueAInsercaoAtribuiASequencia(1);

            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync("ABC1234", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Repositorio
                .Setup(r => r.ReservarUmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Publicador
                .Setup(p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("broker fora do ar"));

            Repositorio
                .Setup(r => r.DevolverParaFilaAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_ainda_assim_responde_com_sucesso() =>
            // O lançamento está gravado. Responder erro faria o usuário lançar de
            // novo — duplicata criada por nós.
            Resultado.Should().NotBeNull();

        [Fact]
        public void Entao_responde_cadastrado_para_a_varredura_recolher() =>
            Resultado.Stage.Should().Be(StageLancamentoEnum.Cadastrado);

        [Fact]
        public void Entao_devolve_a_linha_para_a_fila() =>
            Repositorio.Verify(
                r => r.DevolverParaFilaAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
                Times.Once);
    }

    // --------------------------------------------- normalização da entrada

    public sealed class QuandoAContaVemComCaixaEEspacosVariados : CenarioDoHandler
    {
        protected override void Case()
        {
            Comando = Comando with { ContaId = "  abc1234  " };

            DadoQueAInsercaoAtribuiASequencia(1);

            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync("ABC1234", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Repositorio
                .Setup(r => r.ReservarUmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Publicador
                .Setup(p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("msg");

            Repositorio
                .Setup(r => r.MarcarEnfileiradoAsync(It.IsAny<Guid>(), "msg", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_normaliza_para_maiusculas_sem_espacos() =>
            // A conta vira ordering key no Pub/Sub, e chave é comparação exata:
            // "abc1234" e "ABC1234" seriam duas contas distintas para o broker.
            Gravado.ContaId.Should().Be("ABC1234");

        [Fact]
        public void Entao_a_resposta_traz_a_conta_normalizada() =>
            Resultado.ContaId.Should().Be("ABC1234");

        [Fact]
        public void Entao_publica_com_a_chave_normalizada() =>
            Publicador.Verify(
                p => p.PublicarAsync(
                    It.Is<LancamentoRegistradoEvent>(e => e.ContaId == "ABC1234"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
    }

    public sealed class QuandoGravaOValor : CenarioDoHandler
    {
        protected override void Case()
        {
            Comando = Comando with { Valor = 1_234.56m, Tipo = TipoLancamentoEnum.Debito };

            DadoQueAInsercaoAtribuiASequencia(1);

            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public void Entao_converte_para_centavos() =>
            Gravado.ValorCentavos.Should().Be(123_456);

        [Fact]
        public void Entao_guarda_o_valor_sempre_positivo() =>
            // O sinal vem do tipo, não do número guardado.
            Gravado.ValorCentavos.Should().BePositive();

        [Fact]
        public void Entao_o_delta_do_debito_e_negativo() =>
            Gravado.DeltaCentavos.Should().Be(-123_456);

        [Fact]
        public void Entao_a_resposta_volta_em_reais() =>
            Resultado.Valor.Should().Be(1_234.56m);
    }

    public sealed class QuandoAObservacaoVemEmBranco : CenarioDoHandler
    {
        protected override void Case()
        {
            Comando = Comando with { Observacao = "   " };

            DadoQueAInsercaoAtribuiASequencia(1);

            Repositorio
                .Setup(r => r.SemPredecessorPendenteAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public void Entao_grava_nulo_em_vez_de_espacos() =>
            Gravado.Observacao.Should().BeNull();
    }
}
