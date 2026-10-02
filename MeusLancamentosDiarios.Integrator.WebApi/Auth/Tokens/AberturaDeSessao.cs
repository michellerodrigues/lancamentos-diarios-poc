using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;

/// <summary>
/// O par de tokens que todo login devolve: senha, Google, cadastro e renovacao
/// terminam aqui, para os quatro caminhos emitirem a mesma sessao.
/// </summary>
public sealed class AberturaDeSessao(
    IUsuarioRepository repositorio,
    EmissorDeTokens emissor,
    IOptions<AuthOptions> options,
    TimeProvider relogio)
{
    public async Task<SessaoResponse> AbrirAsync(UsuarioEntity usuario, CancellationToken cancellationToken)
    {
        var acesso = emissor.CriarAcesso(usuario);
        var renovacao = SegredoOpaco.Gerar();

        await repositorio.GravarTokenAsync(
            usuario.Id,
            FinalidadeTokenEnum.Renovacao,
            renovacao.Hash,
            relogio.GetUtcNow() + options.Value.Jwt.ValidadeRenovacao,
            cancellationToken);

        return new SessaoResponse
        {
            TokenAcesso = acesso.Token,
            ExpiraEm = acesso.ExpiraEm,
            TokenRenovacao = renovacao.Texto,
            Usuario = usuario.ToResponse()
        };
    }
}
