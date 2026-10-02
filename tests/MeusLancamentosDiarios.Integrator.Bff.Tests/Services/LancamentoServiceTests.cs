using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores;
using MeusLancamentosDiarios.Integrator.Bff.Services;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.TestSupport;
using Moq;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Services;

public sealed class LancamentoServiceTests
{
    /// <summary>
    /// Base dos cenarios do service. O cliente da WebApi e dublê estrito; o conversor
    /// e real, porque o que sai do service e justamente o payload convertido.
    /// </summary>
    public abstract class CenarioDoService : Cenario
    {
        protected readonly Mock<ILancamentosApiClient> Api = new(MockBehavior.Strict);

        protected ClaimsPrincipal Usuario = UsuarioDe(Roles.Cliente, "ABC1234");

        protected NovoLancamentoRequest Pedido = new()
        {
            ContaId = null,
            Tipo = TipoLancamentoEnum.Credito,
            Valor = 100m,
            DataLancamento = new DateOnly(2026, 10, 1)
        };

        /// <summary>O comando como chegou a WebApi.</summary>
        protected CriarLancamentoCommand? Enviado;

        protected RespostaApi<LancamentoCriadoTela> Resultado = null!;

        protected void DadoQueAWebApiResponde(RespostaApi<CriarLancamentoResponse> resposta) =>
            Api.Setup(a => a.CriarLancamentoAsync(It.IsAny<CriarLancamentoCommand>(), It.IsAny<CancellationToken>()))
                .Callback<CriarLancamentoCommand, CancellationToken>((comando, _) => Enviado = comando)
                .ReturnsAsync(resposta);

        protected void DadoQueAWebApiCria() =>
            DadoQueAWebApiResponde(RespostaApi<CriarLancamentoResponse>.Ok(new CriarLancamentoResponse
            {
                Id = Guid.CreateVersion7(),
                ContaId = "ABC1234",
                Sequencia = 7,
                Tipo = TipoLancamentoEnum.Credito,
                Valor = 100m,
                DataLancamento = new DateOnly(2026, 10, 1),
                DataRegistro = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero),
                Observacao = null,
                Stage = StageLancamentoEnum.Enfileirado
            }));

        protected override async Task When()
        {
            var service = new LancamentoService(Api.Object, new LancamentoCriadoTelaConversor());
            Resultado = await service.IncluirAsync(Pedido, Usuario, CancellationToken.None);
        }

        protected static ClaimsPrincipal UsuarioDe(string role, string? conta)
        {
            var claims = new List<Claim> { new(ClaimsDoToken.Role, role) };
            if (conta is not null) claims.Add(new Claim(ClaimsDoToken.Conta, conta));

            return new ClaimsPrincipal(new ClaimsIdentity(
                claims, authenticationType: "Bearer", nameType: "name", roleType: ClaimsDoToken.Role));
        }
    }

    // --------------------------------------------- de qual conta

    public sealed class QuandoOClienteNaoInformaConta : CenarioDoService
    {
        protected override void Case() => DadoQueAWebApiCria();

        [Fact]
        public void Entao_lanca_na_conta_do_token() =>
            Enviado!.ContaId.Should().Be("ABC1234");

        [Fact]
        public void Entao_leva_o_pedido_inteiro_para_o_comando()
        {
            Enviado!.Tipo.Should().Be(TipoLancamentoEnum.Credito);
            Enviado.Valor.Should().Be(100m);
            Enviado.DataLancamento.Should().Be(new DateOnly(2026, 10, 1));
        }

        [Fact]
        public void Entao_devolve_o_lancamento_ja_convertido_para_a_tela()
        {
            Resultado.Sucesso.Should().BeTrue();
            Resultado.Valor!.StageRotulo.Should().Be("Na fila");
            Resultado.Valor.ValorFormatado.Replace(' ', ' ').Should().Be("R$ 100,00");
        }
    }

    public sealed class QuandoOAdminInformaOutraConta : CenarioDoService
    {
        protected override void Case()
        {
            // O filtro de posse ja deixou passar: o admin alcanca qualquer conta.
            Usuario = UsuarioDe(Roles.Admin, "ADM0001");
            Pedido = Pedido with { ContaId = "XYZ9999" };
            DadoQueAWebApiCria();
        }

        [Fact]
        public void Entao_lanca_na_conta_informada() =>
            Enviado!.ContaId.Should().Be("XYZ9999");
    }

    public sealed class QuandoOAdminNaoInformaConta : CenarioDoService
    {
        protected override void Case()
        {
            Usuario = UsuarioDe(Roles.Admin, "ADM0001");
            DadoQueAWebApiCria();
        }

        [Fact]
        public void Entao_lanca_na_propria_conta() =>
            Enviado!.ContaId.Should().Be("ADM0001");
    }

    // --------------------------------------------- recusas

    public sealed class QuandoAWebApiRecusa : CenarioDoService
    {
        private static readonly JsonElement Problema =
            JsonDocument.Parse("""{"title":"Dados invalidos.","errors":{"Valor":["O valor deve ser maior que zero."]}}""").RootElement;

        protected override void Case() =>
            DadoQueAWebApiResponde(RespostaApi<CriarLancamentoResponse>.Falha(400, Problema));

        [Fact]
        public void Entao_repassa_o_status() =>
            Resultado.StatusCode.Should().Be(400);

        [Fact]
        public void Entao_repassa_o_problem_details_intacto() =>
            // As mensagens do FluentValidation chegam ao usuario sem traducao no meio.
            Resultado.Problema!.Value.GetProperty("errors").GetProperty("Valor")[0].GetString()
                .Should().Be("O valor deve ser maior que zero.");
    }

    public sealed class QuandoOTokenNaoTemConta : CenarioDoService
    {
        protected override void Case() => Usuario = UsuarioDe(Roles.Cliente, conta: null);

        [Fact]
        public void Entao_recusa_com_403() =>
            Resultado.StatusCode.Should().Be(403);

        [Fact]
        public void Entao_nem_chama_a_WebApi() =>
            Api.Verify(
                a => a.CriarLancamentoAsync(It.IsAny<CriarLancamentoCommand>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }
}
