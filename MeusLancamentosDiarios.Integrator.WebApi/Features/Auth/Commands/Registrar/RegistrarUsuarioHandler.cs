using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.Registrar;

public sealed class RegistrarUsuarioHandler(CadastroDeCliente cadastro, AberturaDeSessao sessao)
    : ICommandHandler<RegistrarUsuarioCommand, SessaoResponse>
{
    public async Task<SessaoResponse> HandleAsync(
        RegistrarUsuarioCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await cadastro.CadastrarAsync(
            UsuarioEntity.NormalizarEmail(comando.Email),
            comando.Nome.Trim(),
            comando.Senha,
            googleId: null,
            cancellationToken);

        return await sessao.AbrirAsync(usuario, cancellationToken);
    }
}
