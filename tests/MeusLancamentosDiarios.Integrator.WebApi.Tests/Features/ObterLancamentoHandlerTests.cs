using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.TestSupport.Construtores;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Queries.ObterLancamento;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features;

public sealed class ObterLancamentoHandlerTests
{
    public abstract class CenarioDaConsulta : Cenario
    {
        protected readonly Mock<ILancamentoRepository> Repositorio = new(MockBehavior.Strict);

        protected readonly Guid Id = Guid.CreateVersion7();

        protected LancamentoDetalheResponse? Resultado;

        protected override async Task When() =>
            Resultado = await new ObterLancamentoHandler(Repositorio.Object)
                .HandleAsync(new ObterLancamentoQuery { Id = Id }, CancellationToken.None);
    }

    public sealed class QuandoOLancamentoExiste : CenarioDaConsulta
    {
        protected override void Case() =>
            Repositorio
                .Setup(r => r.ObterPorIdAsync(Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Dado.UmLancamento()
                    .ComId(Id)
                    .NaConta("XYZ9999")
                    .ComSequencia(3)
                    .ComValorEmCentavos(12_345)
                    .NoEstagio(StageLancamentoEnum.Enfileirado)
                    .Build());

        [Fact]
        public void Entao_traz_a_conta_para_o_filtro_de_posse_conferir() =>
            Resultado!.ContaId.Should().Be("XYZ9999");

        [Fact]
        public void Entao_converte_centavos_em_reais() =>
            Resultado!.Valor.Should().Be(123.45m);

        [Fact]
        public void Entao_mostra_em_que_estagio_ele_esta() =>
            Resultado!.Stage.Should().Be(StageLancamentoEnum.Enfileirado);
    }

    public sealed class QuandoOLancamentoNaoExiste : CenarioDaConsulta
    {
        protected override void Case() =>
            Repositorio
                .Setup(r => r.ObterPorIdAsync(Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LancamentoEntity?)null);

        [Fact]
        public void Entao_devolve_null_para_o_endpoint_responder_404() =>
            Resultado.Should().BeNull();
    }
}
