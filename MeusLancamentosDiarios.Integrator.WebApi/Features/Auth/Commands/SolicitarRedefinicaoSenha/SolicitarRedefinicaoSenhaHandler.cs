using System.Net;
using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.SolicitarRedefinicaoSenha;

/// <summary>
/// Gera um link de uso unico e manda por e-mail. Serve tambem para quem so entra
/// com Google definir uma senha.
///
/// Nunca falha para quem chama: e-mail inexistente e falha de SMTP terminam no log,
/// nao na resposta. Uma resposta diferente diria quais e-mails tem cadastro.
/// </summary>
public sealed class SolicitarRedefinicaoSenhaHandler(
    IUsuarioRepository repositorio,
    IEnviadorEmail email,
    IOptions<AuthOptions> options,
    TimeProvider relogio,
    ILogger<SolicitarRedefinicaoSenhaHandler> logger)
    : ICommandHandler<SolicitarRedefinicaoSenhaCommand, bool>
{
    public async Task<bool> HandleAsync(
        SolicitarRedefinicaoSenhaCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await repositorio.ObterPorEmailAsync(
            UsuarioEntity.NormalizarEmail(comando.Email), cancellationToken);

        if (usuario is null)
        {
            logger.LogInformation("Redefinicao de senha pedida para e-mail sem cadastro.");
            return false;
        }

        var opcoes = options.Value.RedefinicaoSenha;

        // So o link mais recente vale: pedir de novo invalida o anterior.
        await repositorio.RevogarTokensAsync(usuario.Id, FinalidadeTokenEnum.RedefinicaoSenha, cancellationToken);

        var segredo = SegredoOpaco.Gerar();

        await repositorio.GravarTokenAsync(
            usuario.Id,
            FinalidadeTokenEnum.RedefinicaoSenha,
            segredo.Hash,
            relogio.GetUtcNow() + opcoes.Validade,
            cancellationToken);

        var link = $"{opcoes.UrlDoFront}?token={Uri.EscapeDataString(segredo.Texto)}";

        try
        {
            await email.EnviarAsync(
                usuario.Email,
                "Redefinição de senha",
                Corpo(usuario, link, opcoes.Validade),
                cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao enviar o e-mail de redefinicao do usuario {UsuarioId}.", usuario.Id);
            return false;
        }
    }

    private static string Corpo(UsuarioEntity usuario, string link, TimeSpan validade) => $"""
        <p>Olá, {WebUtility.HtmlEncode(usuario.Nome)}.</p>
        <p>Recebemos um pedido para redefinir a senha da conta <strong>{usuario.ContaId}</strong>.</p>
        <p><a href="{WebUtility.HtmlEncode(link)}">Redefinir minha senha</a></p>
        <p>O link vale por {(int)validade.TotalMinutes} minutos e só pode ser usado uma vez.
           Se não foi você quem pediu, ignore este e-mail: a senha atual continua valendo.</p>
        """;
}
