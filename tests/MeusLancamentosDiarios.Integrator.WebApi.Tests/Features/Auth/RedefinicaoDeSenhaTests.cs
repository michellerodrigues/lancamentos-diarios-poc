using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.RedefinirSenha;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.SolicitarRedefinicaoSenha;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

public sealed class RedefinicaoDeSenhaTests
{
    // ============================================= "esqueci a senha"

    public abstract class CenarioDaSolicitacao : CenarioDeAutenticacao
    {
        protected readonly Mock<IEnviadorEmail> Email = new(MockBehavior.Strict);

        protected readonly UsuarioEntity Dono = UmUsuario();

        /// <summary>Corpo do e-mail como saiu, quando saiu.</summary>
        protected string? CorpoEnviado;

        /// <summary>Hash do token que foi para o banco.</summary>
        protected byte[]? TokenGravado;

        protected void DadoQueOEmailRetorna(UsuarioEntity? usuario) =>
            Repositorio
                .Setup(r => r.ObterPorEmailAsync("cliente@lancamentos.local", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

        protected void DadoQueOTokenPodeSerGravado()
        {
            Repositorio
                .Setup(r => r.RevogarTokensAsync(Dono.Id, FinalidadeTokenEnum.RedefinicaoSenha, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Repositorio
                .Setup(r => r.GravarTokenAsync(
                    Dono.Id, FinalidadeTokenEnum.RedefinicaoSenha, It.IsAny<byte[]>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, FinalidadeTokenEnum, byte[], DateTimeOffset, CancellationToken>(
                    (_, _, hash, _, _) => TokenGravado = hash)
                .Returns(Task.CompletedTask);
        }

        protected override async Task When()
        {
            var handler = new SolicitarRedefinicaoSenhaHandler(
                Repositorio.Object,
                Email.Object,
                Opcoes,
                Relogio,
                NullLogger<SolicitarRedefinicaoSenhaHandler>.Instance);

            Falha = await Record.ExceptionAsync(() => handler.HandleAsync(
                new SolicitarRedefinicaoSenhaCommand { Email = "cliente@lancamentos.local" },
                CancellationToken.None));
        }
    }

    public sealed class QuandoOEmailTemCadastro : CenarioDaSolicitacao
    {
        protected override void Case()
        {
            DadoQueOEmailRetorna(Dono);
            DadoQueOTokenPodeSerGravado();

            Email
                .Setup(e => e.EnviarAsync(Dono.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, CancellationToken>((_, _, corpo, _) => CorpoEnviado = corpo)
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_o_link_do_email_leva_o_token_que_o_banco_conhece()
        {
            var token = CorpoEnviado!.Split("?token=")[1].Split('"')[0];

            SegredoOpaco.Hashear(Uri.UnescapeDataString(token)).Should().Equal(TokenGravado);
        }

        [Fact]
        public void Entao_o_link_aponta_para_a_tela_do_front() =>
            CorpoEnviado.Should().Contain("http://localhost:4200/redefinir-senha?token=");

        [Fact]
        public void Entao_invalida_o_link_pedido_antes() =>
            Repositorio.Verify(
                r => r.RevogarTokensAsync(Dono.Id, FinalidadeTokenEnum.RedefinicaoSenha, It.IsAny<CancellationToken>()),
                Times.Once);
    }

    public sealed class QuandoOEmailNaoTemCadastro : CenarioDaSolicitacao
    {
        protected override void Case() => DadoQueOEmailRetorna(null);

        [Fact]
        public void Entao_nao_falha_para_quem_chamou() =>
            // Uma falha aqui contaria que o e-mail nao existe.
            Falha.Should().BeNull();

        [Fact]
        public void Entao_nao_envia_email() =>
            Email.Verify(
                e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    public sealed class QuandoOSmtpFalha : CenarioDaSolicitacao
    {
        protected override void Case()
        {
            DadoQueOEmailRetorna(Dono);
            DadoQueOTokenPodeSerGravado();

            Email
                .Setup(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Net.Mail.SmtpException("servidor fora"));
        }

        [Fact]
        public void Entao_a_falha_fica_no_log_e_nao_na_resposta() =>
            Falha.Should().BeNull();
    }

    // ============================================= redefinir com o token do e-mail

    public abstract class CenarioDaRedefinicao : CenarioDeAutenticacao
    {
        protected const string TokenDoLink = "token-que-veio-no-email";

        protected const string SenhaNova = "NovaSenha2026";

        protected readonly UsuarioEntity Dono = UmUsuario();

        protected string? HashGravado;

        protected void DadoQueOConsumoDoToken(SituacaoTokenEnum situacao) =>
            Repositorio
                .Setup(r => r.ConsumirTokenAsync(
                    It.Is<byte[]>(h => h.SequenceEqual(SegredoOpaco.Hashear(TokenDoLink))),
                    FinalidadeTokenEnum.RedefinicaoSenha,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConsumoDeToken(situacao, situacao == SituacaoTokenEnum.Valido ? Dono.Id : null));

        protected override async Task When()
        {
            var handler = new RedefinirSenhaHandler(Repositorio.Object, Hasher);

            Falha = await Record.ExceptionAsync(() => handler.HandleAsync(
                new RedefinirSenhaCommand { Token = TokenDoLink, NovaSenha = SenhaNova },
                CancellationToken.None));
        }
    }

    public sealed class QuandoOLinkEValido : CenarioDaRedefinicao
    {
        protected override void Case()
        {
            DadoQueOConsumoDoToken(SituacaoTokenEnum.Valido);

            Repositorio
                .Setup(r => r.ObterPorIdAsync(Dono.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Dono);

            Repositorio
                .Setup(r => r.AtualizarSenhaAsync(Dono.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, string, CancellationToken>((_, hash, _) => HashGravado = hash)
                .Returns(Task.CompletedTask);

            Repositorio
                .Setup(r => r.RevogarTokensAsync(Dono.Id, FinalidadeTokenEnum.Renovacao, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public void Entao_a_senha_nova_passa_a_valer() =>
            Hasher.VerifyHashedPassword(Dono, HashGravado!, SenhaNova)
                .Should().Be(PasswordVerificationResult.Success);

        [Fact]
        public void Entao_encerra_todas_as_sessoes_abertas() =>
            Repositorio.Verify(
                r => r.RevogarTokensAsync(Dono.Id, FinalidadeTokenEnum.Renovacao, It.IsAny<CancellationToken>()),
                Times.Once);
    }

    public sealed class QuandoOLinkJaFoiUsadoOuVenceu : CenarioDaRedefinicao
    {
        protected override void Case() => DadoQueOConsumoDoToken(SituacaoTokenEnum.JaConsumido);

        [Fact]
        public void Entao_recusa_com_400() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(400);

        [Fact]
        public void Entao_nao_troca_a_senha() =>
            Repositorio.Verify(
                r => r.AtualizarSenhaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }
}
