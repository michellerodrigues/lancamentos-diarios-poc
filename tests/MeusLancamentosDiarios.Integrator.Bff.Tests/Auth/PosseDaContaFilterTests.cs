using System.Security.Claims;
using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Auth;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Auth;

public sealed class PosseDaContaFilterTests
{
    /// <summary>
    /// Base dos cenarios do filtro. O "endpoint" e um delegate que so registra se foi
    /// chamado: o que importa e se o filtro deixou chegar ate ele.
    /// </summary>
    public abstract class CenarioDoFiltro : Cenario
    {
        protected ClaimsPrincipal Usuario = UsuarioDe(Roles.Cliente, "ABC1234");

        protected string? ContaNaRota;

        protected object?[] Argumentos = [];

        protected readonly IResult RespostaDoEndpoint = Results.Ok("saldo");

        protected bool EndpointExecutou;

        protected object? Resultado;

        protected override async Task When()
        {
            var http = new DefaultHttpContext { User = Usuario };

            if (ContaNaRota is not null)
            {
                http.Request.RouteValues[PosseDaContaFilter.ParametroDeRota] = ContaNaRota;
            }

            Resultado = await new PosseDaContaFilter().InvokeAsync(
                new DefaultEndpointFilterInvocationContext(http, Argumentos),
                _ =>
                {
                    EndpointExecutou = true;
                    return ValueTask.FromResult<object?>(RespostaDoEndpoint);
                });
        }

        protected static ClaimsPrincipal UsuarioDe(string role, string conta) =>
            new(new ClaimsIdentity(
                [new Claim(ClaimsDoToken.Role, role), new Claim(ClaimsDoToken.Conta, conta)],
                authenticationType: "Bearer", nameType: "name", roleType: ClaimsDoToken.Role));

        protected static NovoLancamentoRequest PedidoNaConta(string? conta) => new()
        {
            ContaId = conta,
            Tipo = TipoLancamentoEnum.Credito,
            Valor = 10m,
            DataLancamento = new DateOnly(2026, 10, 1)
        };
    }

    // --------------------------------------------- conta na rota

    public sealed class QuandoOClientePedeAPropriaConta : CenarioDoFiltro
    {
        protected override void Case() => ContaNaRota = "abc1234";

        [Fact]
        public void Entao_deixa_o_endpoint_executar() => EndpointExecutou.Should().BeTrue();

        [Fact]
        public void Entao_devolve_a_resposta_do_endpoint() => Resultado.Should().BeSameAs(RespostaDoEndpoint);
    }

    public sealed class QuandoOClientePedeAContaDeOutro : CenarioDoFiltro
    {
        protected override void Case() => ContaNaRota = "XYZ9999";

        [Fact]
        public void Entao_recusa_com_403() => Resultado.Should().BeOfType<ForbidHttpResult>();

        [Fact]
        public void Entao_nem_chega_ao_endpoint() =>
            // Recusa antes de qualquer ida a WebApi.
            EndpointExecutou.Should().BeFalse();
    }

    public sealed class QuandoOAdminPedeAContaDeOutro : CenarioDoFiltro
    {
        protected override void Case()
        {
            Usuario = UsuarioDe(Roles.Admin, "ADM0001");
            ContaNaRota = "XYZ9999";
        }

        [Fact]
        public void Entao_deixa_passar() => EndpointExecutou.Should().BeTrue();
    }

    // --------------------------------------------- conta no corpo

    public sealed class QuandoOCorpoApontaAContaDeOutro : CenarioDoFiltro
    {
        protected override void Case() => Argumentos = [PedidoNaConta("XYZ9999"), Usuario];

        [Fact]
        public void Entao_recusa_com_403() => Resultado.Should().BeOfType<ForbidHttpResult>();
    }

    public sealed class QuandoOCorpoNaoInformaConta : CenarioDoFiltro
    {
        protected override void Case() => Argumentos = [PedidoNaConta(null), Usuario];

        [Fact]
        public void Entao_deixa_o_servico_decidir_a_conta() =>
            // Sem conta, o servico usa a do token: nao ha o que conferir aqui.
            EndpointExecutou.Should().BeTrue();
    }
}
