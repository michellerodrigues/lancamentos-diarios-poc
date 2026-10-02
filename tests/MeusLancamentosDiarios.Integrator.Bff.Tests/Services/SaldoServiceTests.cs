using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores;
using MeusLancamentosDiarios.Integrator.Bff.Services;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;
using MeusLancamentosDiarios.Integrator.TestSupport;
using Moq;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Services;

public sealed class SaldoServiceTests
{
    public abstract class CenarioDoService : Cenario
    {
        protected readonly Mock<ILancamentosApiClient> Api = new(MockBehavior.Strict);

        protected RespostaApi<SaldoTela> Resultado = null!;

        protected static SaldoResponse SaldoDaApi(bool consolidacaoEmAndamento) => new()
        {
            ContaId = "ABC1234",
            SaldoConsolidado = 375.00m,
            UltimaSequenciaConsolidada = 9,
            AtualizadoEm = new DateTimeOffset(2026, 10, 1, 18, 2, 18, TimeSpan.Zero),
            CreditosPendentes = 0m,
            DebitosPendentes = 0m,
            SaldoProjetado = 375.00m,
            ConsolidacaoEmAndamento = consolidacaoEmAndamento,
            Pendentes = []
        };

        protected void DadoQueAWebApiResponde(RespostaApi<SaldoResponse> resposta) =>
            Api.Setup(a => a.ObterSaldoAsync("ABC1234", It.IsAny<CancellationToken>())).ReturnsAsync(resposta);

        protected override async Task When()
        {
            var service = new SaldoService(Api.Object, new SaldoTelaConversor(new PendenteTelaConversor()));
            Resultado = await service.ObterAsync("ABC1234", CancellationToken.None);
        }
    }

    public sealed class QuandoAWebApiDevolveOSaldo : CenarioDoService
    {
        protected override void Case() =>
            DadoQueAWebApiResponde(RespostaApi<SaldoResponse>.Ok(SaldoDaApi(consolidacaoEmAndamento: false)));

        [Fact]
        public void Entao_devolve_o_payload_da_tela() =>
            Resultado.Valor!.SaldoConsolidadoFormatado.Replace(' ', ' ').Should().Be("R$ 375,00");

        [Fact]
        public void Entao_desliga_o_polling_quando_nada_esta_em_transito() =>
            Resultado.Valor!.IntervaloPollingMs.Should().Be(0);
    }

    public sealed class QuandoAWebApiRecusa : CenarioDoService
    {
        protected override void Case() =>
            DadoQueAWebApiResponde(RespostaApi<SaldoResponse>.Falha(403, null));

        [Fact]
        public void Entao_repassa_a_recusa_sem_converter_nada()
        {
            Resultado.Sucesso.Should().BeFalse();
            Resultado.StatusCode.Should().Be(403);
            Resultado.Valor.Should().BeNull();
        }
    }
}
