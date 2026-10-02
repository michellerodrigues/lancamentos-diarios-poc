using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.TestSupport.Construtores;

namespace MeusLancamentosDiarios.Integrator.Events.Tests.Data;

public sealed class LancamentoEntityTests
{
    public sealed class QuandoCalculaODeltaDoSaldo
    {
        /// <summary>
        /// Classes de equivalência do impacto no saldo: crédito soma, débito subtrai.
        /// O valor guardado é sempre positivo; o sinal vem do tipo.
        /// </summary>
        public static TheoryData<TipoLancamentoEnum, long, long> Casos => new()
        {
            // tipo, centavos guardados, delta esperado
            { TipoLancamentoEnum.Credito, 10_000,  10_000 },
            { TipoLancamentoEnum.Debito,  10_000, -10_000 },
            { TipoLancamentoEnum.Credito,      1,       1 },
            { TipoLancamentoEnum.Debito,       1,      -1 },
            { TipoLancamentoEnum.Credito,      0,       0 },
            { TipoLancamentoEnum.Debito,       0,       0 },
        };

        [Theory]
        [MemberData(nameof(Casos))]
        public void Entao_o_sinal_vem_do_tipo(TipoLancamentoEnum tipo, long centavos, long deltaEsperado)
        {
            // Case
            var lancamento = Dado.UmLancamento()
                .DoTipo(tipo)
                .ComValorEmCentavos(centavos)
                .Build();

            // When
            var delta = lancamento.DeltaCentavos;

            // Then
            delta.Should().Be(deltaEsperado);
        }
    }
}
