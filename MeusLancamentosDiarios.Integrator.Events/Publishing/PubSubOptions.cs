namespace MeusLancamentosDiarios.Integrator.Events.Publishing;

public sealed class PubSubOptions
{
    public const string Secao = "PubSub";

    /// <summary>Id do projeto no GCP. Com o emulador, qualquer valor serve.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>O mesmo nome no publicador (Events, WebApi) e no consumidor.</summary>
    public string Topico { get; set; } = string.Empty;

    public string Subscription { get; set; } = string.Empty;

    /// <summary>
    /// Cria topico e subscription na subida. Util com o emulador, que sobe vazio.
    /// Em producao o provisionamento e feito por Terraform, entao deixe false.
    /// </summary>
    public bool CriarRecursos { get; set; }

    public bool Valida =>
        !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(Topico)
        && !string.IsNullOrWhiteSpace(Subscription);
}
