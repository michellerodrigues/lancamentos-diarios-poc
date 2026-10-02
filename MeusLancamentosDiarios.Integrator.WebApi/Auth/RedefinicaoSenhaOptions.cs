namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

public sealed class RedefinicaoSenhaOptions
{
    /// <summary>Tela do front que recebe o token. O e-mail leva o link para ela.</summary>
    public string UrlDoFront { get; set; } = string.Empty;

    public TimeSpan Validade { get; set; }

    public bool Valida => Uri.TryCreate(UrlDoFront, UriKind.Absolute, out _) && Validade > TimeSpan.Zero;
}
