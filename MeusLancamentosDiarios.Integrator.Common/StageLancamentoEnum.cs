namespace MeusLancamentosDiarios.Integrator.Common;

/// <summary>
/// Estágio de um lançamento dentro do fluxo de consolidação.
/// A própria tabela LancamentosDiarios funciona como outbox: esta coluna
/// é o que garante que nenhum lançamento seja publicado ou consolidado duas vezes.
/// </summary>
public enum StageLancamentoEnum
{
    /// <summary>A WebApi gravou o lançamento. Ainda não foi visto pelo publicador.</summary>
    Cadastrado = 1,

    /// <summary>O projeto de Events leu a linha e a reservou para publicação.</summary>
    Lido = 2,

    /// <summary>O projeto de Events publicou o evento no Pub/Sub.</summary>
    Enfileirado = 3,

    /// <summary>O Consumer recebeu a mensagem da fila e começou a consolidar.</summary>
    EmProcessamento = 4,

    /// <summary>O Consumer aplicou o lançamento ao saldo consolidado.</summary>
    Consolidado = 5,

    /// <summary>Falha em qualquer etapa do fluxo. O campo Erro guarda o motivo.</summary>
    Erro = 6
}
