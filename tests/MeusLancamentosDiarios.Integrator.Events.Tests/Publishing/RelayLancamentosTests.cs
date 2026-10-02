using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing;
using MeusLancamentosDiarios.Integrator.Events.Publishing.Contracts;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.TestSupport.Construtores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MeusLancamentosDiarios.Integrator.Events.Tests.Publishing;

public sealed class RelayLancamentosTests
{
    /// <summary>Base comum: dublês do repositório e do publicador, e o relay sob teste.</summary>
    public abstract class CenarioDoRelay : Cenario
    {
        protected readonly Mock<ILancamentoRepository> Repositorio = new(MockBehavior.Strict);
        protected readonly Mock<IPublicadorEventos> Publicador = new(MockBehavior.Strict);

        protected RelayOptions Opcoes { get; } = new()
        {
            TamanhoLote = 50,
            MaxTentativas = 5,
            BackoffMaximo = TimeSpan.FromMinutes(1),
            ReservaExpiraEm = TimeSpan.FromMinutes(1)
        };

        protected RelayLancamentos Relay => new(
            Repositorio.Object,
            Publicador.Object,
            Options.Create(Opcoes),
            NullLogger<RelayLancamentos>.Instance);
    }

    // ---------------------------------------------------------------- publicação

    public sealed class QuandoPublicaUmLancamentoComSucesso : CenarioDoRelay
    {
        private const string MensagemId = "msg-42";

        private LancamentoEntity _lancamento = null!;
        private bool _resultado;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento()
                .NaConta("ABC1234")
                .ComSequencia(7)
                .NoEstagio(StageLancamentoEnum.Lido)
                .Build();

            Publicador
                .Setup(p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(MensagemId);

            Repositorio
                .Setup(r => r.MarcarEnfileiradoAsync(_lancamento.Id, MensagemId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        protected override async Task When() =>
            _resultado = await Relay.PublicarAsync(_lancamento);

        [Fact]
        public void Entao_informa_sucesso() =>
            _resultado.Should().BeTrue();

        [Fact]
        public void Entao_marca_o_lancamento_como_enfileirado() =>
            Repositorio.Verify(
                r => r.MarcarEnfileiradoAsync(_lancamento.Id, MensagemId, It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_publica_o_evento_com_a_conta_e_a_sequencia_da_linha() =>
            Publicador.Verify(
                p => p.PublicarAsync(
                    It.Is<LancamentoRegistradoEvent>(e =>
                        e.LancamentoId == _lancamento.Id &&
                        e.ContaId == "ABC1234" &&
                        e.Sequencia == 7 &&
                        e.ValorCentavos == _lancamento.ValorCentavos),
                    It.IsAny<CancellationToken>()),
                Times.Once);
    }

    public sealed class QuandoAPublicacaoFalha : CenarioDoRelay
    {
        private LancamentoEntity _lancamento = null!;
        private bool _resultado;
        private DateTimeOffset _proximaTentativaRegistrada;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento()
                .NoEstagio(StageLancamentoEnum.Lido)
                .ComTentativasDeEnvio(1)
                .Build();

            Publicador
                .Setup(p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("broker fora do ar"));

            Repositorio
                .Setup(r => r.DevolverParaFilaAsync(
                    _lancamento.Id,
                    It.IsAny<string>(),
                    Opcoes.MaxTentativas,
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Guid, string, int, DateTimeOffset, CancellationToken>(
                    (_, _, _, proxima, _) => _proximaTentativaRegistrada = proxima)
                .Returns(Task.CompletedTask);
        }

        protected override async Task When() =>
            _resultado = await Relay.PublicarAsync(_lancamento);

        [Fact]
        public void Entao_informa_falha() =>
            _resultado.Should().BeFalse();

        [Fact]
        public void Entao_nao_propaga_a_excecao() =>
            // Chegar aqui já prova: o When teria estourado antes de completar.
            _resultado.Should().BeFalse();

        [Fact]
        public void Entao_devolve_a_linha_para_a_fila_com_o_motivo() =>
            Repositorio.Verify(
                r => r.DevolverParaFilaAsync(
                    _lancamento.Id,
                    "broker fora do ar",
                    Opcoes.MaxTentativas,
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_agenda_a_proxima_tentativa_no_futuro() =>
            // Segunda tentativa: 2^2 = 4s. Sem isso, uma queda curta do broker
            // queimaria as cinco tentativas em segundos.
            _proximaTentativaRegistrada.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(2));

        [Fact]
        public void Entao_nao_marca_como_enfileirado() =>
            Repositorio.Verify(
                r => r.MarcarEnfileiradoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    // ---------------------------------------------------------------- backoff

    public sealed class QuandoCalculaOBackoff
    {
        /// <summary>
        /// Classes de equivalência da espera: primeiras tentativas dobram; a partir
        /// do ponto em que 2^n ultrapassa o teto, todas colapsam no teto.
        /// </summary>
        public static TheoryData<int, int> Tentativas => new()
        {
            // tentativa, segundos esperados (teto de 60s)
            { 1, 2 },
            { 2, 4 },
            { 3, 8 },
            { 4, 16 },
            { 5, 32 },
            { 6, 60 },   // 64 passaria do teto
            { 20, 60 },  // muito além do teto
        };

        [Theory]
        [MemberData(nameof(Tentativas))]
        public void Entao_dobra_ate_o_teto(int tentativa, int segundosEsperados)
        {
            // Case
            var relay = new RelayLancamentos(
                Mock.Of<ILancamentoRepository>(),
                Mock.Of<IPublicadorEventos>(),
                Options.Create(new RelayOptions { BackoffMaximo = TimeSpan.FromMinutes(1) }),
                NullLogger<RelayLancamentos>.Instance);

            // When
            var espera = relay.CalcularBackoff(tentativa);

            // Then
            espera.Should().Be(TimeSpan.FromSeconds(segundosEsperados));
        }
    }

    // ---------------------------------------------------------------- ciclo

    public sealed class QuandoNaoHaNadaParaPublicar : CenarioDoRelay
    {
        private int _publicados;

        protected override void Case() =>
            Repositorio
                .Setup(r => r.ReservarParaPublicacaoAsync(
                    Opcoes.TamanhoLote, Opcoes.ReservaExpiraEm, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

        protected override async Task When() =>
            _publicados = await Relay.ExecutarCicloAsync();

        [Fact]
        public void Entao_nao_publica_nada() =>
            _publicados.Should().Be(0);

        [Fact]
        public void Entao_nao_chama_o_broker() =>
            Publicador.Verify(
                p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    public sealed class QuandoUmaContaFalhaNoMeioDoLote : CenarioDoRelay
    {
        private IReadOnlyList<LancamentoEntity> _contaQueFalha = null!;
        private IReadOnlyList<LancamentoEntity> _outraConta = null!;
        private int _publicados;

        protected override void Case()
        {
            // ABC1234 com três lançamentos; XYZ9999 com um. O primeiro da ABC1234
            // falha, então os dois seguintes DELA não podem ser publicados —
            // publicá-los inverteria a ordem de forma irreversível.
            _contaQueFalha = Dado.LancamentosDaConta("ABC1234", 3);
            _outraConta = Dado.LancamentosDaConta("XYZ9999", 1);

            var lote = _contaQueFalha.Concat(_outraConta).ToList();

            Repositorio
                .Setup(r => r.ReservarParaPublicacaoAsync(
                    Opcoes.TamanhoLote, Opcoes.ReservaExpiraEm, It.IsAny<CancellationToken>()))
                .ReturnsAsync(lote);

            // O primeiro da ABC1234 falha.
            Publicador
                .Setup(p => p.PublicarAsync(
                    It.Is<LancamentoRegistradoEvent>(e => e.LancamentoId == _contaQueFalha[0].Id),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("falhou"));

            // A outra conta publica normalmente.
            Publicador
                .Setup(p => p.PublicarAsync(
                    It.Is<LancamentoRegistradoEvent>(e => e.ContaId == "XYZ9999"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("msg-xyz");

            Repositorio
                .Setup(r => r.DevolverParaFilaAsync(
                    _contaQueFalha[0].Id, It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Repositorio
                .Setup(r => r.LiberarReservaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Repositorio
                .Setup(r => r.MarcarEnfileiradoAsync(
                    _outraConta[0].Id, "msg-xyz", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        protected override async Task When() =>
            _publicados = await Relay.ExecutarCicloAsync();

        [Fact]
        public void Entao_publica_apenas_a_conta_saudavel() =>
            _publicados.Should().Be(1);

        [Fact]
        public void Entao_nao_tenta_publicar_os_sucessores_da_conta_travada() =>
            Publicador.Verify(
                p => p.PublicarAsync(
                    It.Is<LancamentoRegistradoEvent>(e =>
                        e.LancamentoId == _contaQueFalha[1].Id || e.LancamentoId == _contaQueFalha[2].Id),
                    It.IsAny<CancellationToken>()),
                Times.Never);

        [Fact]
        public void Entao_libera_a_reserva_dos_sucessores_sem_contar_tentativa()
        {
            // LiberarReserva, e não DevolverParaFila: nada deu errado com eles,
            // só perderam a vez. Contar tentativa os levaria a Erro sem motivo.
            Repositorio.Verify(
                r => r.LiberarReservaAsync(_contaQueFalha[1].Id, It.IsAny<CancellationToken>()),
                Times.Once);

            Repositorio.Verify(
                r => r.LiberarReservaAsync(_contaQueFalha[2].Id, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Entao_a_conta_saudavel_nao_e_afetada() =>
            Repositorio.Verify(
                r => r.LiberarReservaAsync(_outraConta[0].Id, It.IsAny<CancellationToken>()),
                Times.Never);
    }

    public sealed class QuandoTodoOLotePublica : CenarioDoRelay
    {
        private IReadOnlyList<LancamentoEntity> _lote = null!;
        private int _publicados;

        protected override void Case()
        {
            _lote = Dado.LancamentosDaConta("ABC1234", 4);

            Repositorio
                .Setup(r => r.ReservarParaPublicacaoAsync(
                    Opcoes.TamanhoLote, Opcoes.ReservaExpiraEm, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_lote);

            Publicador
                .Setup(p => p.PublicarAsync(It.IsAny<LancamentoRegistradoEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("msg");

            Repositorio
                .Setup(r => r.MarcarEnfileiradoAsync(
                    It.IsAny<Guid>(), "msg", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        protected override async Task When() =>
            _publicados = await Relay.ExecutarCicloAsync();

        [Fact]
        public void Entao_publica_todos() =>
            _publicados.Should().Be(4);

        [Fact]
        public void Entao_publica_na_ordem_da_sequencia()
        {
            var sequenciasPublicadas = Publicador.Invocations
                .Select(i => ((LancamentoRegistradoEvent)i.Arguments[0]).Sequencia)
                .ToList();

            sequenciasPublicadas.Should().Equal(1, 2, 3, 4);
        }
    }
}
