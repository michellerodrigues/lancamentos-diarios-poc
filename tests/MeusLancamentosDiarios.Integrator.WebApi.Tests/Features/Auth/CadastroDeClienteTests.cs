using FluentAssertions;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

public sealed class CadastroDeClienteTests
{
    public abstract class CenarioDoCadastro : CenarioDeAutenticacao
    {
        protected readonly List<string> ContasTentadas = [];

        protected UsuarioEntity? Resultado;

        protected void DadoQueAsInsercoesRespondem(params ResultadoInsercaoUsuarioEnum[] respostas)
        {
            var fila = new Queue<ResultadoInsercaoUsuarioEnum>(respostas);

            Repositorio
                .Setup(r => r.InserirAsync(It.IsAny<UsuarioEntity>(), It.IsAny<CancellationToken>()))
                .Callback<UsuarioEntity, CancellationToken>((u, _) => ContasTentadas.Add(u.ContaId))
                .ReturnsAsync(() => fila.Dequeue());
        }

        protected override async Task When() =>
            Resultado = await Capturar(() => Cadastro().CadastrarAsync(
                "nova@lancamentos.local", "Nova", SenhaCorreta, googleId: null, CancellationToken.None));
    }

    public sealed class QuandoAContaSorteadaJaEstaEmUso : CenarioDoCadastro
    {
        protected override void Case() =>
            // Ja tem dono, ou ja tem lancamento de antes do login existir.
            DadoQueAsInsercoesRespondem(ResultadoInsercaoUsuarioEnum.ContaEmUso, ResultadoInsercaoUsuarioEnum.Inserido);

        [Fact]
        public void Entao_sorteia_outra_conta() =>
            ContasTentadas.Should().HaveCount(2).And.OnlyHaveUniqueItems();

        [Fact]
        public void Entao_o_usuario_fica_com_a_conta_que_entrou() =>
            Resultado!.ContaId.Should().Be(ContasTentadas[^1]);
    }

    public sealed class QuandoOEmailJaEstaCadastrado : CenarioDoCadastro
    {
        protected override void Case() => DadoQueAsInsercoesRespondem(ResultadoInsercaoUsuarioEnum.EmailEmUso);

        [Fact]
        public void Entao_recusa_com_409() =>
            Falha.Should().BeOfType<ProblemaException>().Which.Status.Should().Be(409);

        [Fact]
        public void Entao_nao_insiste_com_outra_conta() =>
            ContasTentadas.Should().ContainSingle();
    }

    public sealed class QuandoSorteiaUmaConta
    {
        [Fact]
        public void Entao_segue_o_formato_das_contas_existentes() =>
            // Tres letras e quatro digitos, como ABC1234: passa no validador de conta.
            Enumerable.Range(0, 200)
                .Select(_ => CadastroDeCliente.SortearConta())
                .Should().AllSatisfy(conta => conta.Should().MatchRegex("^[A-Z]{3}[0-9]{4}$"));
    }
}
