namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;

public interface IEnviadorEmail
{
    Task EnviarAsync(string para, string assunto, string html, CancellationToken cancellationToken = default);
}
