using System.Security.Claims;
using FluentAssertions;
using FluentValidation.TestHelper;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.Registrar;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

public sealed class RegrasDeAcessoTests
{
    private static ClaimsPrincipal Usuario(string role, string conta) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimsDoToken.Role, role), new Claim(ClaimsDoToken.Conta, conta)],
            authenticationType: "Bearer",
            nameType: "name",
            roleType: ClaimsDoToken.Role));

    public sealed class QuandoConfereAPosseDaConta
    {
        /// <summary>
        /// A role diz o que o usuario pode fazer; a posse diz sobre qual conta. O
        /// cliente so alcanca a dele, o admin alcanca todas.
        /// </summary>
        public static TheoryData<string, string, string, bool> Casos => new()
        {
            // role,          conta do token, conta pedida, alcanca
            { Roles.Cliente, "ABC1234",       "ABC1234",    true  },  // a propria
            { Roles.Cliente, "ABC1234",       " abc1234 ",  true  },  // a propria, digitada diferente
            { Roles.Cliente, "ABC1234",       "XYZ9999",    false },  // a de outro
            { Roles.Admin,   "ADM0001",       "XYZ9999",    true  },  // admin, qualquer uma
        };

        [Theory]
        [MemberData(nameof(Casos))]
        public void Entao_aplica_a_regra(string role, string contaDoToken, string contaPedida, bool alcanca) =>
            Usuario(role, contaDoToken).PodeAcessarConta(contaPedida).Should().Be(alcanca);
    }

    public sealed class QuandoValidaASenhaDoCadastro
    {
        /// <summary>Classes de equivalencia da senha nova, na mesma regra da redefinicao.</summary>
        public static TheoryData<string, bool> Senhas => new()
        {
            // senha,          deve passar
            { "",              false },  // vazia
            { "abc1234",       false },  // 7: abaixo do minimo
            { "abcd1234",      true  },  // 8: no minimo
            { "abcdefgh",      false },  // sem numero
            { "12345678",      false },  // sem letra
            { new string('a', 127) + "1", true  },  // 128: no maximo
            { new string('a', 128) + "1", false },  // 129: acima do maximo
        };

        [Theory]
        [MemberData(nameof(Senhas))]
        public void Entao_aplica_a_regra_de_forca(string senha, bool devePassar)
        {
            // Case
            var comando = new RegistrarUsuarioCommand { Nome = "Nova", Email = "nova@lancamentos.local", Senha = senha };

            // When
            var resultado = new RegistrarUsuarioValidator().TestValidate(comando);

            // Then
            if (devePassar) resultado.ShouldNotHaveValidationErrorFor(x => x.Senha);
            else resultado.ShouldHaveValidationErrorFor(x => x.Senha);
        }

        [Fact]
        public void Entao_a_senha_vazia_traz_uma_mensagem_so() =>
            // Cascade Stop: "informe a senha" ja explica o resto.
            new RegistrarUsuarioValidator()
                .TestValidate(new RegistrarUsuarioCommand { Nome = "Nova", Email = "nova@x.com", Senha = "" })
                .ShouldHaveValidationErrorFor(x => x.Senha)
                .Should().ContainSingle();
    }
}
