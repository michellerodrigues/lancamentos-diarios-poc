using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.EncerrarSessao;

/// <summary>
/// Revoga o token de renovacao. O JWT de acesso ja emitido nao tem como ser
/// revogado; ele simplesmente vence, e por isso a validade dele e curta.
/// </summary>
public sealed class EncerrarSessaoHandler(IUsuarioRepository repositorio)
    : ICommandHandler<EncerrarSessaoCommand, bool>
{
    public async Task<bool> HandleAsync(EncerrarSessaoCommand comando, CancellationToken cancellationToken)
    {
        // Token ja usado ou inexistente: a sessao ja esta encerrada, nada a fazer.
        var consumo = await repositorio.ConsumirTokenAsync(
            SegredoOpaco.Hashear(comando.TokenRenovacao), FinalidadeTokenEnum.Renovacao, cancellationToken);

        return consumo.Situacao == SituacaoTokenEnum.Valido;
    }
}
