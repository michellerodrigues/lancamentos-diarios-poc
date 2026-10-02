using System.Net;
using System.Security.Claims;
using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Auth;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using Microsoft.AspNetCore.Http;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests;

public sealed class AutenticacaoTests
{
    public sealed class QuandoOBffChamaAWebApi
    {
        /// <summary>Fim da linha que so registra o que recebeu.</summary>
        private sealed class Destino : HttpMessageHandler
        {
            public HttpRequestMessage? Recebido { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Recebido = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private static async Task<HttpRequestMessage> Enviar(string? autorizacaoDoFront)
        {
            var contexto = new DefaultHttpContext();

            if (autorizacaoDoFront is not null)
            {
                contexto.Request.Headers.Authorization = autorizacaoDoFront;
            }

            var destino = new Destino();
            var repasse = new RepassarTokenHandler(new HttpContextAccessor { HttpContext = contexto })
            {
                InnerHandler = destino
            };

            using var cliente = new HttpClient(repasse) { BaseAddress = new Uri("http://webapi") };
            await cliente.GetAsync("/saldo/ABC1234");

            return destino.Recebido!;
        }

        [Fact]
        public async Task Entao_leva_o_mesmo_bearer_que_chegou_do_front()
        {
            // Case / When
            var recebido = await Enviar("Bearer token-do-usuario");

            // Then — a WebApi decide com o token do usuario, nao com um do BFF.
            recebido.Headers.Authorization!.ToString().Should().Be("Bearer token-do-usuario");
        }

        [Fact]
        public async Task Entao_sem_token_do_front_segue_sem_token()
        {
            // Case / When — rotas publicas, como o login.
            var recebido = await Enviar(null);

            // Then
            recebido.Headers.Authorization.Should().BeNull();
        }
    }

    public sealed class QuandoConfereAPosseDaConta
    {
        private static ClaimsPrincipal Usuario(string role, string conta) =>
            new(new ClaimsIdentity(
                [new Claim(ClaimsDoToken.Role, role), new Claim(ClaimsDoToken.Conta, conta)],
                authenticationType: "Bearer",
                nameType: "name",
                roleType: ClaimsDoToken.Role));

        [Theory]
        [InlineData(Roles.Cliente, "ABC1234", "abc1234", true)]
        [InlineData(Roles.Cliente, "ABC1234", "XYZ9999", false)]
        [InlineData(Roles.Admin, "ADM0001", "XYZ9999", true)]
        public void Entao_segue_a_mesma_regra_da_WebApi(string role, string contaDoToken, string contaPedida, bool alcanca) =>
            Usuario(role, contaDoToken).PodeAcessarConta(contaPedida).Should().Be(alcanca);
    }
}
