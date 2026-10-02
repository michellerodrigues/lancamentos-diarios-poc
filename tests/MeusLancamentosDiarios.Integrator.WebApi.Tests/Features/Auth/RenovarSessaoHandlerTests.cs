using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.RenovarSessao;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

public sealed class RenovarSessaoHandlerTests
{
    public abstract class CenarioDaRenovacao : CenarioDeAutenticacao
    {
        protected const string TokenApresentado = "token-de-renovacao-do-cliente";

        protected readonly UsuarioEntity Dono = UmUsuario();

        protected SessaoResponse? Resultado;

        protected void DadoQueOConsumoDoToken(SituacaoTokenEnum situacao, Guid? dono) =>
            Repositorio
                .Setup(r => r.ConsumirTokenAsync(
                    It.Is<byte[]>(h => h.SequenceEqual(SegredoOpaco.Hashear(TokenApresentado))),
                    FinalidadeTokenEnum.Renovacao,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConsumoDeToken(situacao, dono));

        protected override async Task When()
        {
            var handler = new RenovarSessaoHandler(
                Repositorio.Object, Sessao(), NullLogger<RenovarSessaoHandler>.Instance);

            Resultado = await Capturar(() => handler.HandleAsync(
                new RenovarSessaoCommand { TokenRenovacao = TokenApresentado }, CancellationToken.None));
        }
    }

    // --------------------------------------------- token valido: rotaciona

    public sealed class QuandoOTokenEValido : CenarioDaRenovacao
    {
        protected override void Case()
        {
            DadoQueOConsumoDoToken(SituacaoTokenEnum.Valido, Dono.Id);

            // Promovido a admin depois do ultimo login.
            Dono.Role = Roles.Admin;
            Repositorio
                .Setup(r => r.ObterPorIdAsync(Dono.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Dono);

            DadoQueASessaoPodeSerGravada();
        }

        [Fact]
        public void Entao_devolve_um_token_de_renovacao_novo() =>
            Resultado!.TokenRenovacao.Should().NotBe(TokenApresentado);

        [Fact]
        public void Entao_a_role_nova_ja_vale_na_renovacao() =>
            Resultado!.Usuario.Role.Should().Be(Roles.Admin);
    }

    // --------------------------------------------- token reutilizado: vazou

    public sealed class QuandoOTokenJaFoiUsado : CenarioDaRenovacao
    {
        protected override void Case()
        {
            DadoQueOConsumoDoToken(SituacaoTokenEnum.JaConsumido, Dono.Id);

            Repositorio
                .Setup(r => r.RevogarTokensAsync(Dono.Id, FinalidadeTokenEnum.Renovacao, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_recusa_com_401() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(401);

        [Fact]
        public void Entao_derruba_todas_as_sessoes_do_usuario() =>
            // Nao da para saber quem e o legitimo: o dono e o ladrao tem o mesmo token.
            Repositorio.Verify(
                r => r.RevogarTokensAsync(Dono.Id, FinalidadeTokenEnum.Renovacao, It.IsAny<CancellationToken>()),
                Times.Once);
    }

    // --------------------------------------------- token desconhecido ou vencido

    public sealed class QuandoOTokenNaoExisteOuVenceu : CenarioDaRenovacao
    {
        protected override void Case() => DadoQueOConsumoDoToken(SituacaoTokenEnum.Invalido, null);

        [Fact]
        public void Entao_recusa_com_401() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(401);

        [Fact]
        public void Entao_nao_revoga_nada() =>
            Repositorio.Verify(
                r => r.RevogarTokensAsync(It.IsAny<Guid>(), It.IsAny<FinalidadeTokenEnum>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }
}
