using System.Text.Json;
using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Endpoints;

public sealed class ResultadosHttpTests
{
    public sealed class QuandoOServicoTeveSucesso
    {
        [Fact]
        public void Entao_responde_200_com_o_valor()
        {
            // Case / When
            var resultado = RespostaApi<string>.Ok("saldo").ParaResultado();

            // Then
            resultado.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(200);
            resultado.Should().BeAssignableTo<IValueHttpResult>().Which.Value.Should().Be("saldo");
        }

        [Fact]
        public void Entao_usa_o_resultado_que_o_endpoint_pedir()
        {
            // Case / When
            var resultado = RespostaApi<string>.Ok("x").ParaResultado(v => Results.Created($"/api/{v}", v));

            // Then
            resultado.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(201);
        }
    }

    public sealed class QuandoAWebApiRecusou
    {
        [Fact]
        public void Entao_repassa_status_e_problem_details()
        {
            // Case
            var problema = JsonDocument.Parse("""{"title":"Dados invalidos."}""").RootElement;

            // When
            var resultado = RespostaApi<string>.Falha(400, problema).ParaResultado();

            // Then
            resultado.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(400);
            resultado.Should().BeAssignableTo<IValueHttpResult>().Which.Value.Should().Be(problema);
        }

        [Fact]
        public void Entao_sem_corpo_responde_so_o_status()
        {
            // Case / When
            var resultado = RespostaApi<string>.Falha(403, null).ParaResultado();

            // Then
            resultado.Should().BeOfType<StatusCodeHttpResult>().Which.StatusCode.Should().Be(403);
        }
    }

    public sealed class QuandoORepasseVolta
    {
        [Fact]
        public void Entao_o_corpo_segue_como_veio()
        {
            // Case / When
            var resultado = new RespostaRepassada(401, """{"title":"Sessao expirada."}""", "application/problem+json")
                .ParaResultado();

            // Then
            var conteudo = resultado.Should().BeOfType<ContentHttpResult>().Subject;
            conteudo.StatusCode.Should().Be(401);
            conteudo.ResponseContent.Should().Be("""{"title":"Sessao expirada."}""");
            conteudo.ContentType.Should().Be("application/problem+json");
        }

        [Fact]
        public void Entao_sem_corpo_responde_so_o_status() =>
            new RespostaRepassada(204, null, null).ParaResultado()
                .Should().BeOfType<StatusCodeHttpResult>().Which.StatusCode.Should().Be(204);
    }
}
