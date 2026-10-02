using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo.Queries.ListarContas;

/// <summary>Contas que ja receberam algum lancamento.</summary>
public sealed record ListarContasQuery : IQuery<IReadOnlyList<string>>;
