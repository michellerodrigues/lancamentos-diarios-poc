using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;

/// <summary>De-para do usuario para o contrato. Fica na WebApi: os contratos nao conhecem a tabela.</summary>
public static class UsuarioResponseExtensions
{
    public static UsuarioResponse ToResponse(this UsuarioEntity usuario) => new()
    {
        Id = usuario.Id,
        Nome = usuario.Nome,
        Email = usuario.Email,
        ContaId = usuario.ContaId,
        Role = usuario.Role
    };
}
