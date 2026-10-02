using System.Text.Json;
using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.TestSupport.Construtores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MeusLancamentosDiarios.Integrator.Consumer.Tests;

public sealed class ConsolidadorDeMensagemTests
{
    public abstract class CenarioDoConsolidador : Cenario
    {
        protected const string MensagemId = "msg-1";

        protected readonly Mock<ILancamentoRepository> Repositorio = new(MockBehavior.Strict);

        protected ConsumerOptions Opcoes { get; } = new() { MaxTentativas = 5, Concorrencia = 20 };

        protected RespostaAoBrokerEnum Resposta;

        /// <summary>Corpo da mensagem. Os cenários sobrescrevem quando precisam.</summary>
        protected string Corpo { get; set; } = string.Empty;

        protected static string CorpoDe(LancamentoEntity lancamento) =>
            JsonSerializer.Serialize(new LancamentoRegistradoEvent
            {
                LancamentoId = lancamento.Id,
                ContaId = lancamento.ContaId,
                Sequencia = lancamento.Sequencia,
                Tipo = lancamento.Tipo,
                ValorCentavos = lancamento.ValorCentavos,
                DataLancamento = lancamento.DataLancamento,
                DataRegistro = lancamento.DataRegistro,
                Observacao = lancamento.Observacao
            }, EventosJson.Options);

        protected override async Task When()
        {
            var consolidador = new ConsolidadorDeMensagem(
                Repositorio.Object,
                Options.Create(Opcoes),
                NullLogger<ConsolidadorDeMensagem>.Instance);

            Resposta = await consolidador.ProcessarAsync(Corpo, MensagemId, CancellationToken.None);
        }
    }

    // ------------------------------------------------------- caminho feliz

    public sealed class QuandoOLancamentoEstaPronto : CenarioDoConsolidador
    {
        private LancamentoEntity _lancamento = null!;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento()
                .NaConta("ABC1234")
                .ComSequencia(3)
                .DoTipo(TipoLancamentoEnum.Debito)
                .ComValorEmCentavos(3_000)
                .NoEstagio(StageLancamentoEnum.Enfileirado)
                .Build();

            Corpo = CorpoDe(_lancamento);

            Repositorio
                .Setup(r => r.IniciarProcessamentoAsync(_lancamento.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_lancamento);

            Repositorio
                .Setup(r => r.ConsolidarAsync(_lancamento, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        [Fact]
        public void Entao_confirma_a_mensagem() =>
            Resposta.Should().Be(RespostaAoBrokerEnum.Confirmar);

        [Fact]
        public void Entao_move_a_linha_para_em_processamento_antes_de_consolidar() =>
            Repositorio.Verify(
                r => r.IniciarProcessamentoAsync(_lancamento.Id, It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_aplica_o_lancamento_ao_saldo() =>
            Repositorio.Verify(
                r => r.ConsolidarAsync(_lancamento, It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_o_debito_entra_negativo_no_saldo() =>
            _lancamento.DeltaCentavos.Should().Be(-3_000);
    }

    // ------------------------------------------------------- entrega duplicada

    public sealed class QuandoAMensagemEDuplicada : CenarioDoConsolidador
    {
        private LancamentoEntity _lancamento = null!;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento().NoEstagio(StageLancamentoEnum.Consolidado).Build();

            Corpo = CorpoDe(_lancamento);

            // A linha já saiu do fluxo: IniciarProcessamentoAsync devolve null.
            Repositorio
                .Setup(r => r.IniciarProcessamentoAsync(_lancamento.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LancamentoEntity?)null);
        }

        [Fact]
        public void Entao_confirma_sem_reprocessar() =>
            // O Pub/Sub entrega "pelo menos uma vez". Sem este descarte o saldo
            // seria somado duas vezes a cada reentrega.
            Resposta.Should().Be(RespostaAoBrokerEnum.Confirmar);

        [Fact]
        public void Entao_nao_toca_no_saldo() =>
            Repositorio.Verify(
                r => r.ConsolidarAsync(It.IsAny<LancamentoEntity>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    public sealed class QuandoALinhaSaiDeEmProcessamentoNoMeio : CenarioDoConsolidador
    {
        private LancamentoEntity _lancamento = null!;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento().NoEstagio(StageLancamentoEnum.Enfileirado).Build();

            Corpo = CorpoDe(_lancamento);

            Repositorio
                .Setup(r => r.IniciarProcessamentoAsync(_lancamento.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_lancamento);

            // A consolidação não afetou nenhuma linha: alguém a moveu no meio.
            Repositorio
                .Setup(r => r.ConsolidarAsync(_lancamento, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public void Entao_confirma_sem_devolver_a_fila() =>
            // Reentregar não ajudaria: a linha já não está no estágio esperado.
            Resposta.Should().Be(RespostaAoBrokerEnum.Confirmar);
    }

    // ------------------------------------------------------- corpo inválido

    public sealed class QuandoOCorpoNaoEJsonValido : CenarioDoConsolidador
    {
        protected override void Case() => Corpo = "{ isto nao e json";

        [Fact]
        public void Entao_descarta_a_mensagem() =>
            // Reentregar um corpo malformado só repetiria a falha para sempre.
            Resposta.Should().Be(RespostaAoBrokerEnum.Confirmar);

        [Fact]
        public void Entao_nem_consulta_o_banco() =>
            Repositorio.Verify(
                r => r.IniciarProcessamentoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    public sealed class QuandoOCorpoEONuloLiteral : CenarioDoConsolidador
    {
        protected override void Case() => Corpo = "null";

        [Fact]
        public void Entao_descarta_a_mensagem() =>
            Resposta.Should().Be(RespostaAoBrokerEnum.Confirmar);
    }

    // ------------------------------------------------------- falha na consolidação

    public sealed class QuandoAConsolidacaoFalha : CenarioDoConsolidador
    {
        private LancamentoEntity _lancamento = null!;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento().NoEstagio(StageLancamentoEnum.Enfileirado).Build();

            Corpo = CorpoDe(_lancamento);

            Repositorio
                .Setup(r => r.IniciarProcessamentoAsync(_lancamento.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_lancamento);

            Repositorio
                .Setup(r => r.ConsolidarAsync(_lancamento, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("banco indisponivel"));

            Repositorio
                .Setup(r => r.DevolverParaProcessamentoAsync(
                    _lancamento.Id, It.IsAny<string>(), Opcoes.MaxTentativas, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_devolve_a_mensagem_para_a_fila() =>
            Resposta.Should().Be(RespostaAoBrokerEnum.Devolver);

        [Fact]
        public void Entao_devolve_a_linha_para_enfileirado() =>
            // Precisa voltar a um estágio que IniciarProcessamentoAsync aceite,
            // senão a reentrega seria descartada como duplicata e a linha ficaria
            // presa em EmProcessamento para sempre.
            Repositorio.Verify(
                r => r.DevolverParaProcessamentoAsync(
                    _lancamento.Id, "banco indisponivel", Opcoes.MaxTentativas, It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_nao_propaga_a_excecao() =>
            Resposta.Should().Be(RespostaAoBrokerEnum.Devolver);
    }

    // ------------------------------------------------------- contrato do evento

    public sealed class QuandoDesserializaOEvento : CenarioDoConsolidador
    {
        private LancamentoEntity _lancamento = null!;
        private Guid _idRecebido;

        protected override void Case()
        {
            _lancamento = Dado.UmLancamento()
                .NaConta("XYZ9999")
                .ComSequencia(12)
                .ComObservacao("pagamento")
                .Build();

            Corpo = CorpoDe(_lancamento);

            Repositorio
                .Setup(r => r.IniciarProcessamentoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, CancellationToken>((id, _) => _idRecebido = id)
                .ReturnsAsync((LancamentoEntity?)null);
        }

        [Fact]
        public void Entao_le_o_id_do_lancamento_do_corpo() =>
            _idRecebido.Should().Be(_lancamento.Id);

        [Fact]
        public void Entao_o_contrato_sobrevive_a_ida_e_volta()
        {
            // O publicador e o consumidor compartilham EventosJson.Options; se as
            // opções divergirem, o contrato quebra em silêncio.
            var evento = JsonSerializer.Deserialize<LancamentoRegistradoEvent>(Corpo, EventosJson.Options);

            evento.Should().NotBeNull();
            evento!.ContaId.Should().Be("XYZ9999");
            evento.Sequencia.Should().Be(12);
            evento.Observacao.Should().Be("pagamento");
            evento.DataLancamento.Should().Be(_lancamento.DataLancamento);
            evento.ValorCentavos.Should().Be(_lancamento.ValorCentavos);
        }

        [Fact]
        public void Entao_o_tipo_viaja_como_texto() =>
            // JsonStringEnumConverter: "Credito" em vez de 1, para o contrato
            // continuar legível se alguém inspecionar a mensagem na fila.
            Corpo.Should().Contain("\"tipo\":\"Credito\"");
    }
}
