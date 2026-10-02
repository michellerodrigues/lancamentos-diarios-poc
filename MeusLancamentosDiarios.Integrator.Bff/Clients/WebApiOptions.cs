namespace MeusLancamentosDiarios.Integrator.Bff.Clients;

/// <summary>
/// Onde a WebApi esta. Os valores vem so da configuracao (appsettings.json, compose,
/// Cloud Run): sem padrao no codigo, um ambiente mal configurado falha na subida em
/// vez de apontar para localhost em silencio.
/// </summary>
public sealed class WebApiOptions
{
    public const string Secao = "WebApi";

    public string BaseUrl { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; }

    public bool Valida =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && Timeout > TimeSpan.Zero;
}
