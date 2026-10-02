using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.LoginGoogle;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

public sealed class LoginGoogleHandlerTests
{
    public abstract class CenarioDoGoogle : CenarioDeAutenticacao
    {
        protected const string Credencial = "id-token-do-google";

        protected readonly Mock<IValidadorGoogle> Google = new(MockBehavior.Strict);

        protected IdentidadeGoogle Identidade = new(
            GoogleId: "google-123",
            Email: "Cliente@Lancamentos.local",
            Nome: "Cliente do Google",
            EmailVerificado: true);

        protected SessaoResponse? Resultado;

        protected UsuarioEntity? Inserido;

        protected void DadoQueOGoogleConfirma() =>
            Google.Setup(g => g.ValidarAsync(Credencial, It.IsAny<CancellationToken>())).ReturnsAsync(Identidade);

        protected void DadoQueOGoogleIdRetorna(UsuarioEntity? usuario) =>
            Repositorio
                .Setup(r => r.ObterPorGoogleIdAsync("google-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

        protected void DadoQueOEmailRetorna(UsuarioEntity? usuario) =>
            Repositorio
                .Setup(r => r.ObterPorEmailAsync("cliente@lancamentos.local", It.IsAny<CancellationToken>()))
                .ReturnsAsync(usuario);

        protected override async Task When()
        {
            var handler = new LoginGoogleHandler(
                Google.Object, Repositorio.Object, Cadastro(), Sessao(), Opcoes);

            Resultado = await Capturar(() => handler.HandleAsync(
                new LoginGoogleCommand { Credencial = Credencial }, CancellationToken.None));
        }
    }

    // --------------------------------------------- ja entrou com este Google

    public sealed class QuandoJaEntrouComEsteGoogleAntes : CenarioDoGoogle
    {
        private readonly UsuarioEntity _existente = UmUsuario();

        protected override void Case()
        {
            _existente.GoogleId = "google-123";

            DadoQueOGoogleConfirma();
            DadoQueOGoogleIdRetorna(_existente);
            DadoQueASessaoPodeSerGravada();
        }

        [Fact]
        public void Entao_entra_na_conta_que_ja_existia() =>
            Resultado!.Usuario.ContaId.Should().Be("ABC1234");
    }

    // --------------------------------------------- e-mail ja cadastrado com senha

    public sealed class QuandoOEmailJaTemCadastroComSenha : CenarioDoGoogle
    {
        private readonly UsuarioEntity _existente = UmUsuario();

        protected override void Case()
        {
            DadoQueOGoogleConfirma();
            DadoQueOGoogleIdRetorna(null);
            DadoQueOEmailRetorna(_existente);

            Repositorio
                .Setup(r => r.VincularGoogleAsync(_existente.Id, "google-123", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            DadoQueASessaoPodeSerGravada();
        }

        [Fact]
        public void Entao_vincula_o_google_ao_cadastro_existente() =>
            Repositorio.Verify(
                r => r.VincularGoogleAsync(_existente.Id, "google-123", It.IsAny<CancellationToken>()),
                Times.Once);

        [Fact]
        public void Entao_nao_cria_uma_segunda_conta() =>
            Resultado!.Usuario.ContaId.Should().Be("ABC1234");
    }

    // --------------------------------------------- primeiro acesso

    public sealed class QuandoEOPrimeiroAcesso : CenarioDoGoogle
    {
        protected override void Case()
        {
            DadoQueOGoogleConfirma();
            DadoQueOGoogleIdRetorna(null);
            DadoQueOEmailRetorna(null);

            Repositorio
                .Setup(r => r.InserirAsync(It.IsAny<UsuarioEntity>(), It.IsAny<CancellationToken>()))
                .Callback<UsuarioEntity, CancellationToken>((u, _) => Inserido = u)
                .ReturnsAsync(ResultadoInsercaoUsuarioEnum.Inserido);

            DadoQueASessaoPodeSerGravada();
        }

        [Fact]
        public void Entao_cadastra_como_cliente() =>
            Inserido!.Role.Should().Be(Roles.Cliente);

        [Fact]
        public void Entao_cadastra_sem_senha() =>
            // So entra com Google ate pedir "esqueci a senha" e definir uma.
            Inserido!.SenhaHash.Should().BeNull();

        [Fact]
        public void Entao_guarda_o_email_normalizado() =>
            Inserido!.Email.Should().Be("cliente@lancamentos.local");

        [Fact]
        public void Entao_ganha_uma_conta_propria() =>
            Resultado!.Usuario.ContaId.Should().MatchRegex("^[A-Z]{3}[0-9]{4}$");
    }

    // --------------------------------------------- recusas

    public sealed class QuandoOEmailDoGoogleNaoEVerificado : CenarioDoGoogle
    {
        protected override void Case()
        {
            Identidade = Identidade with { EmailVerificado = false };

            DadoQueOGoogleConfirma();
            DadoQueOGoogleIdRetorna(null);
        }

        [Fact]
        public void Entao_recusa_sem_vincular_a_nenhum_cadastro() =>
            // Quem controla a conta Google pode nao ser o dono do e-mail.
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(401);
    }

    public sealed class QuandoACredencialNaoEValida : CenarioDoGoogle
    {
        protected override void Case() =>
            Google.Setup(g => g.ValidarAsync(Credencial, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IdentidadeGoogle?)null);

        [Fact]
        public void Entao_recusa_com_401() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(401);
    }
}
