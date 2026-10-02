using System.Globalization;

namespace MeusLancamentosDiarios.Integrator.Common.Helpers;

/// <summary>
/// Formatacao pt-BR feita no servidor. Deixa o Angular livre de pipes de moeda
/// e garante que web e um eventual app nativo mostrem exatamente o mesmo texto.
/// No Common porque vale para quem quer que exiba um lancamento.
/// </summary>
public static class LancamentoHelper
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly TimeZoneInfo FusoBrasil =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>1234.5m -> "R$ 1.234,50"</summary>
    public static string Moeda(decimal valor) => valor.ToString("C2", Brasil);

    /// <summary>20/09/2026</summary>
    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", Brasil);

    /// <summary>
    /// 20/09/2026 14:30:22, convertido para o horario de Brasilia —
    /// o banco guarda tudo em UTC.
    /// </summary>
    public static string DataHora(DateTimeOffset? instante) =>
        instante is null
            ? "nunca"
            : TimeZoneInfo.ConvertTime(instante.Value, FusoBrasil).ToString("dd/MM/yyyy HH:mm:ss", Brasil);

    public static string TipoRotulo(TipoLancamentoEnum tipo) =>
        tipo == TipoLancamentoEnum.Debito ? "Débito" : "Crédito";

    public static string Sinal(TipoLancamentoEnum tipo) =>
        tipo == TipoLancamentoEnum.Debito ? "-" : "+";

    /// <summary>Texto do estagio para a tela de acompanhamento.</summary>
    public static string StageRotulo(StageLancamentoEnum stage) => stage switch
    {
        StageLancamentoEnum.Cadastrado => "Cadastrado",
        StageLancamentoEnum.Lido => "Lido pelo publicador",
        StageLancamentoEnum.Enfileirado => "Na fila",
        StageLancamentoEnum.EmProcessamento => "Consolidando",
        StageLancamentoEnum.Consolidado => "Consolidado",
        StageLancamentoEnum.Erro => "Erro",
        _ => stage.ToString()
    };
}
