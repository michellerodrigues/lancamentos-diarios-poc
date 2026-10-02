using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Auth;

namespace MeusLancamentosDiarios.Integrator.Bff.Services;

/// <summary>A tabela de rotas do repasse: /api/auth/x no BFF vira /auth/x na WebApi.</summary>
public sealed class AutenticacaoService(ILancamentosApiClient api) : IAutenticacaoService
{
    public Task<RespostaRepassada> RegistrarAsync(RegistrarUsuarioCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/registrar", comando, cancellationToken);

    public Task<RespostaRepassada> EntrarAsync(LoginCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/login", comando, cancellationToken);

    public Task<RespostaRepassada> EntrarComGoogleAsync(LoginGoogleCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/google", comando, cancellationToken);

    public Task<RespostaRepassada> RenovarAsync(RenovarSessaoCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/renovar", comando, cancellationToken);

    public Task<RespostaRepassada> SairAsync(EncerrarSessaoCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/sair", comando, cancellationToken);

    public Task<RespostaRepassada> EsqueciSenhaAsync(
        SolicitarRedefinicaoSenhaCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/esqueci-senha", comando, cancellationToken);

    public Task<RespostaRepassada> RedefinirSenhaAsync(RedefinirSenhaCommand comando, CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Post, "/auth/redefinir-senha", comando, cancellationToken);

    public Task<RespostaRepassada> ConfiguracaoAsync(CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Get, "/auth/config", null, cancellationToken);

    public Task<RespostaRepassada> UsuarioAtualAsync(CancellationToken cancellationToken) =>
        api.RepassarAsync(HttpMethod.Get, "/auth/eu", null, cancellationToken);
}
