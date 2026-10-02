namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

public sealed class GoogleOptions
{
    /// <summary>
    /// Client ID OAuth do Google Cloud Console. Vazio desliga o login com Google, e
    /// a tela esconde o botao.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    public bool Habilitado => !string.IsNullOrWhiteSpace(ClientId);
}
