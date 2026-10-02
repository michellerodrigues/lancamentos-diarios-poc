namespace MeusLancamentosDiarios.Integrator.Common.Helpers;

/// <summary>
/// Conversao entre centavos (como o banco guarda, em bigint) e reais (como a API
/// expoe). Dinheiro em inteiro para o SUM() do saldo ser exato: ponto flutuante
/// acumularia erro em cima de saldo.
/// </summary>
public static class DinheiroHelper
{
    public static decimal ParaReais(long centavos) => centavos / 100m;

    public static long ParaCentavos(decimal reais) =>
        (long)decimal.Round(reais * 100m, 0, MidpointRounding.AwayFromZero);
}
