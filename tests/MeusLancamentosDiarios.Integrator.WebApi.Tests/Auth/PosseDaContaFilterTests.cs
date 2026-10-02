using System.Security.Claims;
using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Auth;

public sealed class PosseDaContaFilterTests
{
    /// <summary>
    /// Base dos cenarios do filtro. O "endpoint" e um delegate que registra se foi
    /// chamado e devolve a resposta que o cenario escolher.
    /// </summary>
    public abstract class CenarioDoFiltro : Cenario
    {
        protected ClaimsPrincipal Usuario = UsuarioDe(Roles.Cliente, "ABC1234");

        protected string? ContaNaRota;

        protected object?[] Argumentos = [];

        protected IResult RespostaDoEndpoint = Results.Ok();

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

        protected static CriarLancamentoCommand ComandoNaConta(string conta) => new()
        {
            ContaId = conta,
            Tipo = TipoLancamentoEnum.Credito,
            Valor = 10m,
            DataLancamento = new DateOnly(2026, 10, 1)
        };

        protected static LancamentoDetalheResponse LancamentoDaConta(string conta) => new()
        {
            Id = Guid.CreateVersion7(),
            ContaId = conta,
            Sequencia = 1,
            Tipo = TipoLancamentoEnum.Credito,
            Valor = 10m,
            DataLancamento = new DateOnly(2026, 10, 1),
            DataRegistro = DateTimeOffset.UtcNow,
            Stage = StageLancamentoEnum.Consolidado,
            StageAtualizadoEm = DateTimeOffset.UtcNow,
            TentativasEnvio = 1,
            TentativasProcessamento = 1
        };
    }

    // --------------------------------------------- antes do handler: rota e corpo

    public sealed class QuandoOClientePedeAPropriaContaNaRota : CenarioDoFiltro
    {
        protected override void Case() => ContaNaRota = "ABC1234";

        [Fact]
        public void Entao_deixa_o_endpoint_executar() => EndpointExecutou.Should().BeTrue();

        [Fact]
        public void Entao_devolve_a_resposta_do_endpoint() => Resultado.Should().BeSameAs(RespostaDoEndpoint);
    }

    public sealed class QuandoOClientePedeAContaDeOutroNaRota : CenarioDoFiltro
    {
        protected override void Case() => ContaNaRota = "XYZ9999";

        [Fact]
        public void Entao_recusa_com_403() => Resultado.Should().BeOfType<ForbidHttpResult>();

        [Fact]
        public void Entao_nem_chega_ao_handler() => EndpointExecutou.Should().BeFalse();
    }

    public sealed class QuandoOComandoApontaAContaDeOutro : CenarioDoFiltro
    {
        protected override void Case() => Argumentos = [ComandoNaConta("XYZ9999")];

        [Fact]
        public void Entao_recusa_antes_de_gravar()
        {
            Resultado.Should().BeOfType<ForbidHttpResult>();
            EndpointExecutou.Should().BeFalse();
        }
    }

    public sealed class QuandoOComandoVemSemConta : CenarioDoFiltro
    {
        protected override void Case() => Argumentos = [ComandoNaConta("")];

        [Fact]
        public void Entao_deixa_o_validador_responder() =>
            // Conta em branco nao e assunto de posse: o validador devolve 400 com a mensagem certa.
            EndpointExecutou.Should().BeTrue();
    }

    public sealed class QuandoOAdminMexeNaContaDeOutro : CenarioDoFiltro
    {
        protected override void Case()
        {
            Usuario = UsuarioDe(Roles.Admin, "ADM0001");
            Argumentos = [ComandoNaConta("XYZ9999")];
        }

        [Fact]
        public void Entao_deixa_passar() => EndpointExecutou.Should().BeTrue();
    }

    // --------------------------------------------- depois do handler: recurso lido

    public sealed class QuandoORecursoLidoEDeOutraConta : CenarioDoFiltro
    {
        protected override void Case() => RespostaDoEndpoint = Results.Ok(LancamentoDaConta("XYZ9999"));

        [Fact]
        public void Entao_troca_a_resposta_por_403() =>
            // A conta so era conhecida depois de ler a linha; o dado nao sai.
            Resultado.Should().BeOfType<ForbidHttpResult>();
    }

    public sealed class QuandoORecursoLidoEDaPropriaConta : CenarioDoFiltro
    {
        protected override void Case() => RespostaDoEndpoint = Results.Ok(LancamentoDaConta("ABC1234"));

        [Fact]
        public void Entao_devolve_o_recurso() => Resultado.Should().BeSameAs(RespostaDoEndpoint);
    }
}
