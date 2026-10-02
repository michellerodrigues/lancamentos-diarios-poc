using MeusLancamentosDiarios.Integrator.Common.Auth;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

/// <summary>Uma linha da tabela Usuarios.</summary>
public sealed class UsuarioEntity
{
    public Guid Id { get; set; }

    /// <summary>Sempre minusculo e sem espaco nas pontas: e a chave de login.</summary>
    public string Email { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    /// <summary>Null para quem so entrou com Google e nunca definiu senha.</summary>
    public string? SenhaHash { get; set; }

    /// <summary>O "sub" do Google. Null para quem nunca entrou com Google.</summary>
    public string? GoogleId { get; set; }

    /// <summary>A unica conta do usuario. Unica na tabela: uma conta, um dono.</summary>
    public string ContaId { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.Cliente;

    public DateTimeOffset CriadoEm { get; set; }

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();
}
