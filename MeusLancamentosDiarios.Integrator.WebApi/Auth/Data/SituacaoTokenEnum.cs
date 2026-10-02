namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

public enum SituacaoTokenEnum
{
    /// <summary>Existia, estava dentro da validade e foi consumido agora.</summary>
    Valido,

    /// <summary>Ja tinha sido usado. Para renovacao, e sinal de token vazado.</summary>
    JaConsumido,

    /// <summary>Nao existe ou venceu.</summary>
    Invalido
}
