using System.Security.Claims;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

namespace MeusLancamentosDiarios.Integrator.Bff.Services;

public sealed class LancamentoService(
    ILancamentosApiClient api,
    IConversor<CriarLancamentoResponse, LancamentoCriadoTela> conversor)
    : ILancamentoService
{
    public async Task<RespostaApi<LancamentoCriadoTela>> IncluirAsync(
        NovoLancamentoRequest pedido, ClaimsPrincipal usuario, CancellationToken cancellationToken)
    {
        // O cliente nao escolhe conta: e sempre a do token. O admin pode informar
        // outra; sem informar, lanca na propria.
        var conta = string.IsNullOrWhiteSpace(pedido.ContaId) ? usuario.ContaId() : pedido.ContaId;

        if (conta is null)
        {
            return RespostaApi<LancamentoCriadoTela>.Falha(StatusCodes.Status403Forbidden, null);
        }

        // O pedido da tela vira o comando da WebApi: o contrato de la e o do Messages.
        var comando = new CriarLancamentoCommand
        {
            ContaId = conta,
            Tipo = pedido.Tipo,
            Valor = pedido.Valor,
            DataLancamento = pedido.DataLancamento,
            Observacao = pedido.Observacao
        };

        var resposta = await api.CriarLancamentoAsync(comando, cancellationToken);
        return resposta.Mapear(conversor.Converter);
    }
}
