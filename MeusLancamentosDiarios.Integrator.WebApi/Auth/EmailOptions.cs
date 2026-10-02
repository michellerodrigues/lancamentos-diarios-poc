namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

public sealed class EmailOptions
{
    /// <summary>Local: o Mailpit do compose, que guarda o e-mail e mostra em :8025.</summary>
    public string Host { get; set; } = string.Empty;

    public int Porta { get; set; }

    public bool UsarSsl { get; set; }

    public string? Usuario { get; set; }

    public string? Senha { get; set; }

    public string Remetente { get; set; } = string.Empty;

    public string NomeRemetente { get; set; } = string.Empty;

    public bool Valida =>
        !string.IsNullOrWhiteSpace(Host) && Porta is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(Remetente);
}
