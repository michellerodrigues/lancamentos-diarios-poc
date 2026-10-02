using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Helpers;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Conversores;

public sealed class PendenteTelaConversor : IConversor<LancamentoPendenteResponse, PendenteTela>
{
    public PendenteTela Converter(LancamentoPendenteResponse pendente) => new()
    {
        Id = pendente.Id,
        Sequencia = pendente.Sequencia,
        Tipo = pendente.Tipo,
        TipoRotulo = LancamentoHelper.TipoRotulo(pendente.Tipo),
        Sinal = LancamentoHelper.Sinal(pendente.Tipo),
        Valor = pendente.Valor,
        ValorFormatado = LancamentoHelper.Moeda(pendente.Valor),
        DataLancamento = pendente.DataLancamento,
        DataLancamentoFormatada = LancamentoHelper.Data(pendente.DataLancamento),
        Observacao = pendente.Observacao,
        Stage = pendente.Stage,
        StageRotulo = LancamentoHelper.StageRotulo(pendente.Stage)
    };
}
