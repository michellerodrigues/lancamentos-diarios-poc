namespace MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

public sealed record CriarLancamentosEmLoteResponse
{
    public required int Quantidade { get; init; }

    /// <summary>Na mesma ordem dos itens recebidos.</summary>
    public required IReadOnlyList<LancamentoDoLoteResponse> Lancamentos { get; init; }
}
