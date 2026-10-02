namespace MeusLancamentosDiarios.Integrator.Consumer;

/// <summary>O que o worker deve responder ao broker depois de processar a mensagem.</summary>
public enum RespostaAoBrokerEnum
{
    /// <summary>Mensagem resolvida — consolidada, duplicada ou descartável. Não reentregar.</summary>
    Confirmar,

    /// <summary>Falha passageira. Devolver à fila para nova entrega.</summary>
    Devolver
}
