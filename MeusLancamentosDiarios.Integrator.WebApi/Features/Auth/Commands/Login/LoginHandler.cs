using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using Microsoft.AspNetCore.Identity;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.Login;

/// <summary>
/// E-mail e senha. A recusa e a mesma para e-mail inexistente e senha errada: a
/// resposta nao conta quais e-mails estao cadastrados.
/// </summary>
public sealed class LoginHandler(
    IUsuarioRepository repositorio,
    IPasswordHasher<UsuarioEntity> hasher,
    AberturaDeSessao sessao)
    : ICommandHandler<LoginCommand, SessaoResponse>
{
    public const string Recusa = "E-mail ou senha invalidos.";

    /// <summary>Hash de uma senha qualquer, so para gastar o mesmo tempo quando nao ha usuario.</summary>
    private static readonly string HashFicticio =
        new PasswordHasher<UsuarioEntity>().HashPassword(new UsuarioEntity(), Guid.NewGuid().ToString());

    public async Task<SessaoResponse> HandleAsync(LoginCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await repositorio.ObterPorEmailAsync(
            UsuarioEntity.NormalizarEmail(comando.Email), cancellationToken);

        // Sem usuario, ou usuario que so entra com Google: verifica contra um hash
        // ficticio assim mesmo. O PBKDF2 leva dezenas de milissegundos, e responder
        // na hora denunciaria pelo tempo que o e-mail nao existe.
        if (usuario?.SenhaHash is null)
        {
            hasher.VerifyHashedPassword(new UsuarioEntity(), HashFicticio, comando.Senha);
            throw ProblemaException.NaoAutorizado(Recusa);
        }

        var verificacao = hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, comando.Senha);

        if (verificacao == PasswordVerificationResult.Failed)
        {
            throw ProblemaException.NaoAutorizado(Recusa);
        }

        // Hash de uma versao antiga do algoritmo (menos iteracoes): aproveita que a
        // senha esta em maos e regrava no formato atual.
        if (verificacao == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await repositorio.AtualizarSenhaAsync(
                usuario.Id, hasher.HashPassword(usuario, comando.Senha), cancellationToken);
        }

        return await sessao.AbrirAsync(usuario, cancellationToken);
    }
}
