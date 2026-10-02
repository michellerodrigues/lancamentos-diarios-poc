using System.Security.Cryptography;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using Microsoft.AspNetCore.Identity;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;

/// <summary>
/// Cria o usuario ja com a conta dele. Usado pelo cadastro e pelo primeiro login com
/// Google: os dois caminhos nascem Cliente, com uma conta nova e so dele.
/// </summary>
public sealed class CadastroDeCliente(
    IUsuarioRepository repositorio,
    IPasswordHasher<UsuarioEntity> hasher,
    TimeProvider relogio)
{
    /// <summary>Colisoes de conta sorteada antes de desistir. Com 175 milhoes de combinacoes, nao chega a 2.</summary>
    private const int TentativasConta = 5;

    private const string Letras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digitos = "0123456789";

    /// <summary>
    /// <paramref name="senha"/> null para quem entra so com Google. O e-mail chega
    /// normalizado.
    /// </summary>
    public async Task<UsuarioEntity> CadastrarAsync(
        string email,
        string nome,
        string? senha,
        string? googleId,
        CancellationToken cancellationToken)
    {
        var usuario = new UsuarioEntity
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            Nome = nome,
            GoogleId = googleId,
            Role = Roles.Cliente,
            CriadoEm = relogio.GetUtcNow()
        };

        if (senha is not null)
        {
            usuario.SenhaHash = hasher.HashPassword(usuario, senha);
        }

        for (var tentativa = 1; tentativa <= TentativasConta; tentativa++)
        {
            usuario.ContaId = SortearConta();

            switch (await repositorio.InserirAsync(usuario, cancellationToken))
            {
                case ResultadoInsercaoUsuarioEnum.Inserido:
                    return usuario;

                case ResultadoInsercaoUsuarioEnum.EmailEmUso:
                    throw ProblemaException.Conflito("Este e-mail ja esta cadastrado.");

                case ResultadoInsercaoUsuarioEnum.GoogleIdEmUso:
                    throw ProblemaException.Conflito("Esta conta Google ja esta vinculada a outro usuario.");

                case ResultadoInsercaoUsuarioEnum.ContaEmUso:
                    continue;
            }
        }

        throw new InvalidOperationException(
            $"Nenhuma conta livre em {TentativasConta} sorteios. Algo esta errado com o gerador.");
    }

    /// <summary>
    /// Mesmo formato das contas que ja existiam (ABC1234): tres letras e quatro digitos.
    /// Passa no validador de conta e e legivel numa apresentacao.
    /// </summary>
    internal static string SortearConta() =>
        RandomNumberGenerator.GetString(Letras, 3) + RandomNumberGenerator.GetString(Digitos, 4);
}
