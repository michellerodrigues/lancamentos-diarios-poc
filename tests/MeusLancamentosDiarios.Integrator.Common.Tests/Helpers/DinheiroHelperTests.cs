using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common.Helpers;

namespace MeusLancamentosDiarios.Integrator.Common.Tests.Helpers;

public sealed class DinheiroHelperTests
{
    public sealed class QuandoConverteReaisParaCentavos
    {
        /// <summary>
        /// Classes de equivalência do valor: zero, fração mínima, inteiro redondo,
        /// valor com centavos, e os dois lados do arredondamento de meio centavo.
        /// </summary>
        public static TheoryData<decimal, long> Valores => new()
        {
            { 0m,          0 },
            { 0.01m,       1 },
            { 1m,        100 },
            { 1.99m,     199 },
            { 100.00m, 10_000 },
            { 1_000_000m, 100_000_000 },

            // Meio centavo arredonda para longe do zero (AwayFromZero), não
            // para o par mais próximo — é o que a contabilidade espera.
            { 0.005m,      1 },
            { 0.015m,      2 },
        };

        [Theory]
        [MemberData(nameof(Valores))]
        public void Entao_devolve_o_inteiro_em_centavos(decimal reais, long centavosEsperados)
        {
            // Case / When
            var centavos = DinheiroHelper.ParaCentavos(reais);

            // Then
            centavos.Should().Be(centavosEsperados);
        }
    }

    public sealed class QuandoConverteCentavosParaReais
    {
        public static TheoryData<long, decimal> Valores => new()
        {
            { 0,               0m },
            { 1,            0.01m },
            { 199,          1.99m },
            { 10_000,     100.00m },
            { -3_000,     -30.00m },
        };

        [Theory]
        [MemberData(nameof(Valores))]
        public void Entao_devolve_o_decimal_correspondente(long centavos, decimal reaisEsperados)
        {
            // Case / When
            var reais = DinheiroHelper.ParaReais(centavos);

            // Then
            reais.Should().Be(reaisEsperados);
        }
    }

    public sealed class QuandoFazIdaEVolta
    {
        public static TheoryData<decimal> Valores =>
        [
            0m, 0.01m, 0.99m, 1m, 12.34m, 999.99m, 1_000_000m
        ];

        [Theory]
        [MemberData(nameof(Valores))]
        public void Entao_preserva_o_valor(decimal original)
        {
            // Case / When
            var voltou = DinheiroHelper.ParaReais(DinheiroHelper.ParaCentavos(original));

            // Then — o motivo de guardar centavos em inteiro: nada se perde.
            voltou.Should().Be(original);
        }
    }

    public sealed class QuandoSomaMuitosValoresEmCentavos
    {
        [Fact]
        public void Entao_a_soma_e_exata()
        {
            // Case — 0,10 somado dez mil vezes. Em ponto flutuante isso acumula erro.
            var parcelas = Enumerable.Repeat(0.10m, 10_000).ToList();

            // When
            var totalEmCentavos = parcelas.Sum(DinheiroHelper.ParaCentavos);

            // Then
            DinheiroHelper.ParaReais(totalEmCentavos).Should().Be(1_000.00m);
        }
    }
}
