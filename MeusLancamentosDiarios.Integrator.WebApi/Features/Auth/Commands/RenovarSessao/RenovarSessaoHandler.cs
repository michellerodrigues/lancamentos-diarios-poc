using MeusLancamentosDiarios.Integrator.Messages.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Auth.Commands.RenovarSessao;

/// <summary>
/// Rotacao: cada token de renovacao vale uma vez. O usuario e relido do banco, entao
/// uma troca de role passa a valer na proxima renovacao, sem esperar sete dias.
/// </summary>
public sealed class RenovarSessaoHandler(
    IUsuarioRepository repositorio,
    AberturaDeSessao sessao,
    ILogger<RenovarSessaoHandler> logger)
    : ICommandHandler<RenovarSessaoCommand, SessaoResponse>
{
    public const string Recusa = "Sessao expirada. Entre novamente.";

    public async Task<SessaoResponse> HandleAsync(
        RenovarSessaoCommand comando, CancellationToken cancellationToken)
    {
        var consumo = await repositorio.ConsumirTokenAsync(
            SegredoOpaco.Hashear(comando.TokenRenovacao), FinalidadeTokenEnum.Renovacao, cancellationToken);

        if (consumo.Situacao == SituacaoTokenEnum.JaConsumido)
        {
            // Um token ja trocado voltou: alguem guardou uma copia. Nao da para saber
            // quem e o legitimo, entao derruba todas as sessoes do usuario.
            logger.LogWarning(
                "Token de renovacao reutilizado pelo usuario {UsuarioId}. Todas as sessoes revogadas.",
                consumo.UsuarioId);

            await repositorio.RevogarTokensAsync(
                consumo.UsuarioId!.Value, FinalidadeTokenEnum.Renovacao, cancellationToken);

            throw ProblemaException.NaoAutorizado(Recusa);
        }

        if (consumo.Situacao != SituacaoTokenEnum.Valido)
        {
            throw ProblemaException.NaoAutorizado(Recusa);
        }

        var usuario = await repositorio.ObterPorIdAsync(consumo.UsuarioId!.Value, cancellationToken)
            ?? throw ProblemaException.NaoAutorizado(Recusa);

        return await sessao.AbrirAsync(usuario, cancellationToken);
    }
}
