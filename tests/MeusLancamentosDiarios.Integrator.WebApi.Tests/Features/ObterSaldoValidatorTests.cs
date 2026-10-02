using FluentValidation.TestHelper;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ObterSaldo;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features;

public sealed class ObterSaldoValidatorTests
{
    public sealed class QuandoValidaOLimiteDePendentes
    {
        /// <summary>
        /// Sem limite vale o padrao do appsettings. Informado, precisa ser positivo:
        /// zero ou negativo iria direto para o TOP (@Limite) do SQL.
        /// </summary>
        public static TheoryData<int?, bool> Limites => new()
        {
            // limite, deve passar
            { null, true  },  // usa o padrao
            { 1,    true  },  // menor valido
            { 50,   true  },
            { 0,    false },
            { -1,   false },
        };

        [Theory]
        [MemberData(nameof(Limites))]
        public void Entao_aceita_so_limite_positivo_ou_ausente(int? limite, bool devePassar)
        {
            // Case
            var consulta = new ObterSaldoQuery { ContaId = "ABC1234", LimitePendentes = limite };

            // When
            var resultado = new ObterSaldoValidator().TestValidate(consulta);

            // Then
            if (devePassar) resultado.ShouldNotHaveValidationErrorFor(x => x.LimitePendentes);
            else resultado.ShouldHaveValidationErrorFor(x => x.LimitePendentes);
        }
    }
}
