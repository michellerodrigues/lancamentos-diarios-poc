using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.Login;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

public sealed class LoginHandlerTests
{
    public abstract class CenarioDoLogin : CenarioDeAutenticacao
    {
        protected UsuarioEntity Cadastrado = UmUsuario();

        protected LoginCommand Comando { get; set; } = new()
        {
            // Maiusculas e espaco de proposito: o e-mail e normalizado antes da busca.
            Email = "  Cliente@Lancamentos.local ",
            Senha = SenhaCorreta
        };

        protected SessaoResponse? Resultado;

        protected void DadoQueOEmailRetorna(UsuarioEntity? usuario) =>
            Repositorio
                .Setup(r => r.ObterPorEmailAsync("cliente@lancamentos.local", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

        protected override async Task When()
        {
            var handler = new LoginHandler(Repositorio.Object, Hasher, Sessao());

            Resultado = await Capturar(() => handler.HandleAsync(Comando, CancellationToken.None));
        }
    }

    // --------------------------------------------- senha certa

    public sealed class QuandoASenhaConfere : CenarioDoLogin
    {
        protected override void Case()
        {
            DadoQueOEmailRetorna(Cadastrado);
            DadoQueASessaoPodeSerGravada();
        }

        [Fact]
        public void Entao_abre_a_sessao_do_usuario() =>
            Resultado!.Usuario.Id.Should().Be(Cadastrado.Id);

        [Fact]
        public void Entao_o_token_de_acesso_leva_a_conta_e_a_role()
        {
            var token = new JsonWebToken(Resultado!.TokenAcesso);

            token.GetClaim("conta").Value.Should().Be("ABC1234");
            token.GetClaim("role").Value.Should().Be("Cliente");
        }

        [Fact]
        public void Entao_o_acesso_vence_em_quinze_minutos() =>
            Resultado!.ExpiraEm.Should().Be(Agora.AddMinutes(15));

        [Fact]
        public void Entao_o_banco_guarda_so_o_hash_da_renovacao() =>
            // O texto vai para o cliente; o que fica no banco nao serve para entrar.
            RenovacaoGravada.Should().NotBeNull().And.HaveCount(32);
    }

    // --------------------------------------------- recusas

    public sealed class QuandoASenhaNaoConfere : CenarioDoLogin
    {
        protected override void Case()
        {
            DadoQueOEmailRetorna(Cadastrado);
            Comando = Comando with { Senha = "OutraSenha1" };
        }

        [Fact]
        public void Entao_recusa_com_401() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(401);

        [Fact]
        public void Entao_nao_abre_sessao() =>
            Repositorio.Verify(
                r => r.GravarTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<FinalidadeTokenEnum>(), It.IsAny<byte[]>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    public sealed class QuandoOEmailNaoTemCadastro : CenarioDoLogin
    {
        protected override void Case() => DadoQueOEmailRetorna(null);

        [Fact]
        public void Entao_recusa_com_a_mesma_mensagem_da_senha_errada() =>
            // Mensagens diferentes contariam quais e-mails existem.
            Falha.Should().BeOfType<ProblemaException>().Which.Message.Should().Be(LoginHandler.Recusa);
    }

    public sealed class QuandoOUsuarioSoEntraComGoogle : CenarioDoLogin
    {
        protected override void Case()
        {
            Cadastrado.SenhaHash = null;
            Cadastrado.GoogleId = "google-123";
            DadoQueOEmailRetorna(Cadastrado);
        }

        [Fact]
        public void Entao_recusa_o_login_por_senha() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(401);
    }
}
