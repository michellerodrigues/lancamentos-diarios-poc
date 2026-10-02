using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;

/// <summary>
/// Autenticacao vista pelo front. Tudo e repasse para /auth da WebApi, que e quem
/// conhece usuario e emite token. Os comandos sao os do Messages, sem copia no BFF.
/// </summary>
public interface IAutenticacaoService
{
    Task<RespostaRepassada> RegistrarAsync(RegistrarUsuarioCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> EntrarAsync(LoginCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> EntrarComGoogleAsync(LoginGoogleCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> RenovarAsync(RenovarSessaoCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> SairAsync(EncerrarSessaoCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> EsqueciSenhaAsync(
        SolicitarRedefinicaoSenhaCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> RedefinirSenhaAsync(RedefinirSenhaCommand comando, CancellationToken cancellationToken);

    Task<RespostaRepassada> ConfiguracaoAsync(CancellationToken cancellationToken);

    Task<RespostaRepassada> UsuarioAtualAsync(CancellationToken cancellationToken);
}
