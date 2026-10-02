namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

public enum ResultadoInsercaoUsuarioEnum
{
    Inserido,
    EmailEmUso,

    /// <summary>A conta sorteada ja tem dono ou ja tem lancamento. Sorteie outra.</summary>
    ContaEmUso,

    GoogleIdEmUso
}
