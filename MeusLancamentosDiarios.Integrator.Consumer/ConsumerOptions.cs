namespace MeusLancamentosDiarios.Integrator.Consumer;

public sealed class ConsumerOptions
{
    public const string Secao = "Consumer";

    /// <summary>Tentativas de consolidacao antes de a linha parar em Erro.</summary>
    public int MaxTentativas { get; set; }

    /// <summary>
    /// Quantas mensagens processar ao mesmo tempo, somando TODAS as contas.
    ///
    /// Nao precisa ser 1: com ordering key ligada o Pub/Sub ja entrega uma mensagem
    /// por vez dentro de cada conta, e so libera a proxima apos o ack. Baixar isto
    /// para 1 serializaria contas independentes entre si, sem ganho de ordem.
    /// </summary>
    public int Concorrencia { get; set; }

    public bool Valida => MaxTentativas > 0 && Concorrencia > 0;
}
