namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

/// <summary>
/// Tudo de autenticacao. Os valores vem so da configuracao: o codigo nao guarda padrao
/// de ambiente, e um valor faltando derruba a subida com a mensagem certa.
/// </summary>
public sealed class AuthOptions
{
    public const string Secao = "Auth";

    public JwtOptions Jwt { get; set; } = new();

    public GoogleOptions Google { get; set; } = new();

    public RedefinicaoSenhaOptions RedefinicaoSenha { get; set; } = new();

    public EmailOptions Email { get; set; } = new();

    /// <summary>
    /// Cria na subida o admin e os clientes das contas de demonstracao. So para o
    /// ambiente local: as senhas estao no codigo.
    /// </summary>
    public bool SemearUsuariosDemo { get; set; }
}
