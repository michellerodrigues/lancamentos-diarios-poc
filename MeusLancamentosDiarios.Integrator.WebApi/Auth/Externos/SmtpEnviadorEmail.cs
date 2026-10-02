using System.Net;
using System.Net.Mail;
using System.Text;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos;

/// <summary>
/// SMTP simples. Local, fala com o Mailpit, que nao exige autenticacao nem TLS. Em
/// producao, aponte para o relay SMTP do provedor de e-mail por configuracao.
/// </summary>
public sealed class SmtpEnviadorEmail(IOptions<AuthOptions> options) : IEnviadorEmail
{
    public async Task EnviarAsync(
        string para, string assunto, string html, CancellationToken cancellationToken = default)
    {
        var email = options.Value.Email;

        using var cliente = new SmtpClient(email.Host, email.Porta) { EnableSsl = email.UsarSsl };

        if (!string.IsNullOrEmpty(email.Usuario))
        {
            cliente.Credentials = new NetworkCredential(email.Usuario, email.Senha);
        }

        using var mensagem = new MailMessage(
            new MailAddress(email.Remetente, email.NomeRemetente),
            new MailAddress(para))
        {
            Subject = assunto,
            SubjectEncoding = Encoding.UTF8,
            Body = html,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };

        await cliente.SendMailAsync(mensagem, cancellationToken);
    }
}
