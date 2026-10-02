using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.LoginGoogle;

/// <summary>
/// Troca a credencial do Google pela nossa sessao. Tres casos:
///
/// - ja entrou com este Google antes: entra;
/// - o e-mail ja tem cadastro com senha: vincula o Google a ele e entra;
/// - e-mail novo: cadastra como Cliente, com conta nova, e entra.
///
/// O nosso JWT continua sendo o unico que BFF e WebApi aceitam. O token do Google
/// so prova a identidade uma vez, aqui.
/// </summary>
public sealed class LoginGoogleHandler(
    IValidadorGoogle google,
    IUsuarioRepository repositorio,
    CadastroDeCliente cadastro,
    AberturaDeSessao sessao,
    IOptions<AuthOptions> options)
    : ICommandHandler<LoginGoogleCommand, SessaoResponse>
{
    public async Task<SessaoResponse> HandleAsync(
        LoginGoogleCommand comando, CancellationToken cancellationToken)
    {
        if (!options.Value.Google.Habilitado)
        {
            throw ProblemaException.Invalido("O login com Google nao esta configurado.");
        }

        var identidade = await google.ValidarAsync(comando.Credencial, cancellationToken)
            ?? throw ProblemaException.NaoAutorizado("Nao foi possivel confirmar a conta Google.");

        var usuario = await repositorio.ObterPorGoogleIdAsync(identidade.GoogleId, cancellationToken)
                      ?? await PrimeiroAcessoAsync(identidade, cancellationToken);

        return await sessao.AbrirAsync(usuario, cancellationToken);
    }

    private async Task<UsuarioEntity> PrimeiroAcessoAsync(IdentidadeGoogle identidade, CancellationToken cancellationToken)
    {
        // Sem e-mail verificado, quem controla a conta Google pode nao ser o dono do
        // e-mail. Vincular entregaria a conta de outra pessoa.
        if (!identidade.EmailVerificado)
        {
            throw ProblemaException.NaoAutorizado("O e-mail desta conta Google nao esta verificado.");
        }

        var email = UsuarioEntity.NormalizarEmail(identidade.Email);
        var existente = await repositorio.ObterPorEmailAsync(email, cancellationToken);

        if (existente is null)
        {
            return await cadastro.CadastrarAsync(
                email, identidade.Nome, senha: null, identidade.GoogleId, cancellationToken);
        }

        await repositorio.VincularGoogleAsync(existente.Id, identidade.GoogleId, cancellationToken);
        existente.GoogleId = identidade.GoogleId;

        return existente;
    }
}
