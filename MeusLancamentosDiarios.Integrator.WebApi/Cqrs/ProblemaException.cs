namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs;

/// <summary>
/// Recusa que o handler sabe classificar: credencial errada (401), e-mail ja usado
/// (409), link vencido (400). O handler escolhe o status; o endpoint nao precisa
/// conhecer cada motivo.
/// </summary>
public sealed class ProblemaException(int status, string titulo) : Exception(titulo)
{
    public int Status { get; } = status;

    public static ProblemaException NaoAutorizado(string titulo) =>
        new(StatusCodes.Status401Unauthorized, titulo);

    public static ProblemaException Conflito(string titulo) =>
        new(StatusCodes.Status409Conflict, titulo);

    public static ProblemaException Invalido(string titulo) =>
        new(StatusCodes.Status400BadRequest, titulo);
}
