namespace MeusLancamentosDiarios.Integrator.Bff.Auth;

/// <summary>
/// Leva para a WebApi o mesmo Bearer que chegou do front.
///
/// O BFF nao troca o token por um proprio: a WebApi valida o JWT do usuario e aplica
/// as mesmas roles, entao o BFF nao vira um atalho para quem nao teria acesso direto.
/// </summary>
public sealed class RepassarTokenHandler(IHttpContextAccessor contexto) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var autorizacao = contexto.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrEmpty(autorizacao))
        {
            request.Headers.TryAddWithoutValidation("Authorization", autorizacao);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
