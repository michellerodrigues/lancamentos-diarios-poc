namespace MeusLancamentosDiarios.Integrator.Events.Publishing;

public sealed class RelayOptions
{
    public const string Secao = "Relay";

    /// <summary>Intervalo entre ciclos quando roda como worker continuo.</summary>
    public TimeSpan Intervalo { get; set; }

    /// <summary>Quantos lancamentos cada ciclo reserva de uma vez.</summary>
    public int TamanhoLote { get; set; }

    /// <summary>Tentativas de publicacao antes de a linha parar em Erro.</summary>
    public int MaxTentativas { get; set; }

    /// <summary>
    /// Teto da espera entre tentativas. O atraso dobra a cada falha (2s, 4s, 8s...)
    /// ate este limite, para uma indisponibilidade curta do broker nao consumir
    /// todas as tentativas e parar o lancamento em Erro.
    /// </summary>
    public TimeSpan BackoffMaximo { get; set; }

    /// <summary>
    /// Depois de quanto tempo uma linha parada em Lido volta a ser candidata.
    ///
    /// Lido significa "alguem reservou para publicar". Se esse alguem morreu no meio —
    /// processo derrubado, publish que estourou o limite de espera —, sem isto a linha
    /// ficaria invisivel para sempre, porque a varredura so procura Cadastrado.
    /// </summary>
    public TimeSpan ReservaExpiraEm { get; set; }

    /// <summary>
    /// false quando o ciclo e disparado de fora (Cloud Scheduler -> Cloud Run Job).
    /// true para o loop continuo usado no desenvolvimento local.
    /// </summary>
    public bool LoopContinuo { get; set; }

    public bool Valida =>
        Intervalo > TimeSpan.Zero
        && TamanhoLote > 0
        && MaxTentativas > 0
        && BackoffMaximo > TimeSpan.Zero
        && ReservaExpiraEm > TimeSpan.Zero;
}
