namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

public sealed record ConsumoDeToken(SituacaoTokenEnum Situacao, Guid? UsuarioId);
