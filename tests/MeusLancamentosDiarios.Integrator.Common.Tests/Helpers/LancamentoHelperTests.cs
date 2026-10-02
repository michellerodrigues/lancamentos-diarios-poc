using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Common.Helpers;

namespace MeusLancamentosDiarios.Integrator.Common.Tests.Helpers;

public sealed class LancamentoHelperTests
{
    public sealed class QuandoFormataMoeda
    {
        /// <summary>
        /// Classes de equivalência do valor: zero, centavos, unidade, milhar (separador),
        /// milhão (dois separadores) e negativo.
        /// </summary>
        public static TheoryData<decimal, string> Valores => new()
        {
            { 0m,             "R$ 0,00" },
            { 0.5m,           "R$ 0,50" },
            { 1m,             "R$ 1,00" },
            { 1.99m,          "R$ 1,99" },
            { 1_000m,     "R$ 1.000,00" },
            { 1_234.56m,  "R$ 1.234,56" },
            { 1_000_000m, "R$ 1.000.000,00" },
            { -30m,          "-R$ 30,00" },
        };

        [Theory]
        [MemberData(nameof(Valores))]
        public void Entao_usa_o_padrao_brasileiro(decimal valor, string esperado)
        {
            // Case / When
            var texto = LancamentoHelper.Moeda(valor);

            // Then — ponto para milhar, vírgula para centavos. Formatar no servidor
            // garante que web e um eventual app nativo mostrem o mesmo texto.
            texto.Replace(' ', ' ').Should().Be(esperado);
        }
    }

    public sealed class QuandoFormataData
    {
        public static TheoryData<DateOnly, string> Datas => new()
        {
            { new DateOnly(2026, 9, 21),  "21/09/2026" },
            { new DateOnly(2026, 1, 1),   "01/01/2026" },
            { new DateOnly(2026, 12, 31), "31/12/2026" },
        };

        [Theory]
        [MemberData(nameof(Datas))]
        public void Entao_usa_dia_mes_ano(DateOnly data, string esperado) =>
            LancamentoHelper.Data(data).Should().Be(esperado);
    }

    public sealed class QuandoFormataDataHora
    {
        [Fact]
        public void Entao_converte_de_utc_para_brasilia()
        {
            // Case — 18h59 UTC é 15h59 em Brasília (UTC-3).
            var utc = new DateTimeOffset(2026, 9, 20, 18, 59, 24, TimeSpan.Zero);

            // When
            var texto = LancamentoHelper.DataHora(utc);

            // Then — o banco guarda tudo em UTC; a tela mostra a hora local.
            texto.Should().Be("20/09/2026 15:59:24");
        }

        [Fact]
        public void Entao_um_instante_nulo_vira_nunca()
        {
            // Case — conta que ainda não consolidou nada não tem "última atualização".
            // When
            var texto = LancamentoHelper.DataHora(null);

            // Then
            texto.Should().Be("nunca");
        }
    }

    public sealed class QuandoRotulaOTipo
    {
        /// <summary>Classes de equivalência do tipo: crédito e débito.</summary>
        public static TheoryData<TipoLancamentoEnum, string, string> Tipos => new()
        {
            // tipo, rótulo esperado, sinal esperado
            { TipoLancamentoEnum.Debito,  "Débito",  "-" },
            { TipoLancamentoEnum.Credito, "Crédito", "+" },
        };

        [Theory]
        [MemberData(nameof(Tipos))]
        public void Entao_devolve_rotulo_e_sinal(TipoLancamentoEnum tipo, string rotulo, string sinal)
        {
            // Case / When / Then
            LancamentoHelper.TipoRotulo(tipo).Should().Be(rotulo);
            LancamentoHelper.Sinal(tipo).Should().Be(sinal);
        }
    }

    public sealed class QuandoRotulaOEstagio
    {
        /// <summary>Um rótulo por estágio da esteira, mais o caso desconhecido.</summary>
        public static TheoryData<StageLancamentoEnum, string> Estagios => new()
        {
            { StageLancamentoEnum.Cadastrado,      "Cadastrado" },
            { StageLancamentoEnum.Lido,            "Lido pelo publicador" },
            { StageLancamentoEnum.Enfileirado,     "Na fila" },
            { StageLancamentoEnum.EmProcessamento, "Consolidando" },
            { StageLancamentoEnum.Consolidado,     "Consolidado" },
            { StageLancamentoEnum.Erro,            "Erro" },
            { (StageLancamentoEnum)99,             "99" },  // desconhecido passa direto
        };

        [Theory]
        [MemberData(nameof(Estagios))]
        public void Entao_traduz_para_texto_de_tela(StageLancamentoEnum stage, string esperado) =>
            LancamentoHelper.StageRotulo(stage).Should().Be(esperado);
    }
}
