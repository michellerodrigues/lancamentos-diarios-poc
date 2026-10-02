using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Services;
using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using Moq;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Services;

public sealed class AutenticacaoServiceTests
{
    public sealed class QuandoRepassaParaAWebApi
    {
        private delegate Task<RespostaRepassada> Chamada(IAutenticacaoService service, object? comando);

        /// <summary>Cada operacao com o comando que o front manda e a chamada ao service.</summary>
        private static readonly Dictionary<string, (object? Comando, Chamada Chamar)> Operacoes = new()
        {
            ["registrar"] = (new RegistrarUsuarioCommand { Nome = "Nova", Email = "nova@x.com", Senha = "Senha1234" },
                (s, c) => s.RegistrarAsync((RegistrarUsuarioCommand)c!, default)),
            ["entrar"] = (new LoginCommand { Email = "a@x.com", Senha = "Senha1234" },
                (s, c) => s.EntrarAsync((LoginCommand)c!, default)),
            ["google"] = (new LoginGoogleCommand { Credencial = "credencial" },
                (s, c) => s.EntrarComGoogleAsync((LoginGoogleCommand)c!, default)),
            ["renovar"] = (new RenovarSessaoCommand { TokenRenovacao = "token" },
                (s, c) => s.RenovarAsync((RenovarSessaoCommand)c!, default)),
            ["sair"] = (new EncerrarSessaoCommand { TokenRenovacao = "token" },
                (s, c) => s.SairAsync((EncerrarSessaoCommand)c!, default)),
            ["esqueci"] = (new SolicitarRedefinicaoSenhaCommand { Email = "a@x.com" },
                (s, c) => s.EsqueciSenhaAsync((SolicitarRedefinicaoSenhaCommand)c!, default)),
            ["redefinir"] = (new RedefinirSenhaCommand { Token = "token", NovaSenha = "Senha1234" },
                (s, c) => s.RedefinirSenhaAsync((RedefinirSenhaCommand)c!, default)),
            ["config"] = (null, (s, _) => s.ConfiguracaoAsync(default)),
            ["eu"] = (null, (s, _) => s.UsuarioAtualAsync(default)),
        };

        /// <summary>A tabela de rotas do repasse: cada operacao do front e uma rota de /auth.</summary>
        public static TheoryData<string, string, string> Rotas => new()
        {
            // operacao,   metodo, rota na WebApi
            { "registrar", "POST", "/auth/registrar" },
            { "entrar",    "POST", "/auth/login" },
            { "google",    "POST", "/auth/google" },
            { "renovar",   "POST", "/auth/renovar" },
            { "sair",      "POST", "/auth/sair" },
            { "esqueci",   "POST", "/auth/esqueci-senha" },
            { "redefinir", "POST", "/auth/redefinir-senha" },
            { "config",    "GET",  "/auth/config" },
            { "eu",        "GET",  "/auth/eu" },
        };

        [Theory]
        [MemberData(nameof(Rotas))]
        public async Task Entao_chama_a_rota_certa_com_o_comando_recebido(string operacao, string metodo, string rota)
        {
            // Case
            var (comando, chamar) = Operacoes[operacao];
            var esperada = new RespostaRepassada(200, """{"ok":true}""", "application/json");
            object? corpoEnviado = null;

            var api = new Mock<ILancamentosApiClient>(MockBehavior.Strict);
            api.Setup(a => a.RepassarAsync(
                    It.Is<HttpMethod>(m => m.Method == metodo), rota, It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .Callback<HttpMethod, string, object?, CancellationToken>((_, _, corpo, _) => corpoEnviado = corpo)
                .ReturnsAsync(esperada);

            // When
            var resposta = await chamar(new AutenticacaoService(api.Object), comando);

            // Then — o BFF so repassa: o comando vai como veio, a resposta volta como veio.
            corpoEnviado.Should().BeSameAs(comando);
            resposta.Should().BeSameAs(esperada);
        }
    }
}
