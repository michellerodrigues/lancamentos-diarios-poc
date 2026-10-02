using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamento;

namespace MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamentosEmLote;

/// <summary>
/// Grava o lote inteiro numa transacao e deixa a publicacao para o relay.
///
/// Diferente do avulso, nao tenta publicar no mesmo request: seriam ate mil idas
/// ao broker em serie, cada uma com o seu teto de espera, segurando a resposta.
///
/// O preco e o ritmo do relay: hoje ele publica um lancamento por conta por ciclo
/// (docs/capacidade-do-relay.md). Vinte itens numa conta levam vinte ciclos para
/// sair; contas diferentes andam em paralelo.
/// </summary>
public sealed class CriarLancamentosEmLoteHandler(
    ILancamentoRepository repositorio,
    TimeProvider relogio,
    ILogger<CriarLancamentosEmLoteHandler> logger)
    : ICommandHandler<CriarLancamentosEmLoteCommand, CriarLancamentosEmLoteResponse>
{
    public async Task<CriarLancamentosEmLoteResponse> HandleAsync(
        CriarLancamentosEmLoteCommand comando, CancellationToken cancellationToken)
    {
        var registro = relogio.GetUtcNow();

        var lancamentos = comando.Itens
            .Select(item => CriarLancamentoHandler.Montar(item, registro))
            .ToList();

        await repositorio.InserirLoteAsync(lancamentos, cancellationToken);

        logger.LogInformation(
            "Lote de {Quantidade} lancamentos em {Contas} conta(s) gravado. O relay publica.",
            lancamentos.Count, lancamentos.Select(l => l.ContaId).Distinct().Count());

        return new CriarLancamentosEmLoteResponse
        {
            Quantidade = lancamentos.Count,
            Lancamentos = lancamentos.Select(l => new LancamentoDoLoteResponse
            {
                Id = l.Id,
                ContaId = l.ContaId,
                Sequencia = l.Sequencia,
                Stage = StageLancamentoEnum.Cadastrado
            }).ToList()
        };
    }
}
