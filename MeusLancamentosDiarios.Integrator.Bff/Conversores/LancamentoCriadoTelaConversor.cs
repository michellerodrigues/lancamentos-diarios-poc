using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;
using MeusLancamentosDiarios.Integrator.Common.Helpers;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

namespace MeusLancamentosDiarios.Integrator.Bff.Conversores;

/// <summary>O lancamento recem-criado, com os textos da tela de confirmacao.</summary>
public sealed class LancamentoCriadoTelaConversor : IConversor<CriarLancamentoResponse, LancamentoCriadoTela>
{
    public LancamentoCriadoTela Converter(CriarLancamentoResponse criado) => new()
    {
        Id = criado.Id,
        ContaId = criado.ContaId,
        Sequencia = criado.Sequencia,
        Tipo = criado.Tipo,
        TipoRotulo = LancamentoHelper.TipoRotulo(criado.Tipo),
        Valor = criado.Valor,
        ValorFormatado = LancamentoHelper.Moeda(criado.Valor),
        Sinal = LancamentoHelper.Sinal(criado.Tipo),
        DataLancamento = criado.DataLancamento,
        DataLancamentoFormatada = LancamentoHelper.Data(criado.DataLancamento),
        DataRegistro = criado.DataRegistro,
        DataRegistroFormatada = LancamentoHelper.DataHora(criado.DataRegistro),
        Observacao = criado.Observacao,
        Stage = criado.Stage,
        StageRotulo = LancamentoHelper.StageRotulo(criado.Stage)
    };
}
