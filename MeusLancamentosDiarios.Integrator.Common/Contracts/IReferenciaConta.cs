namespace MeusLancamentosDiarios.Integrator.Common.Contracts;

/// <summary>
/// Algo que aponta para uma conta. Os filtros de posse do BFF e da WebApi conferem
/// se o usuario pode mexer nela.
/// </summary>
public interface IReferenciaConta
{
    /// <summary>Vazio passa pelo filtro: a conta padrao ou a recusa ficam com quem trata.</summary>
    string? ContaId { get; }
}
