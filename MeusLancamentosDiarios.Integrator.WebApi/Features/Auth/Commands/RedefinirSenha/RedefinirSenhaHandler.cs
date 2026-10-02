using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using Microsoft.AspNetCore.Identity;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.RedefinirSenha;

public sealed class RedefinirSenhaHandler(IUsuarioRepository repositorio, IPasswordHasher<UsuarioEntity> hasher)
    : ICommandHandler<RedefinirSenhaCommand, bool>
{
    public const string Recusa = "Link invalido ou expirado. Peca um novo.";

    public async Task<bool> HandleAsync(RedefinirSenhaCommand comando, CancellationToken cancellationToken)
    {
        var consumo = await repositorio.ConsumirTokenAsync(
            SegredoOpaco.Hashear(comando.Token), FinalidadeTokenEnum.RedefinicaoSenha, cancellationToken);

        if (consumo.Situacao != SituacaoTokenEnum.Valido)
        {
            throw ProblemaException.Invalido(Recusa);
        }

        var usuario = await repositorio.ObterPorIdAsync(consumo.UsuarioId!.Value, cancellationToken)
            ?? throw ProblemaException.Invalido(Recusa);

        await repositorio.AtualizarSenhaAsync(
            usuario.Id, hasher.HashPassword(usuario, comando.NovaSenha), cancellationToken);

        // Quem troca a senha pode estar tirando alguem de uma sessao aberta: todas caem.
        await repositorio.RevogarTokensAsync(usuario.Id, FinalidadeTokenEnum.Renovacao, cancellationToken);

        return true;
    }
}
