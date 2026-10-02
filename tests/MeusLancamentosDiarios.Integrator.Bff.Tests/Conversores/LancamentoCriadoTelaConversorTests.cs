using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Conversores;

public sealed class LancamentoCriadoTelaConversorTests
{
    private static CriarLancamentoResponse CriadoNaApi(TipoLancamentoEnum tipo) => new()
    {
        Id = Guid.CreateVersion7(),
        ContaId = "ABC1234",
        Sequencia = 10,
        Tipo = tipo,
        Valor = 1234.50m,
        DataLancamento = new DateOnly(2026, 10, 1),
        DataRegistro = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero),
        Observacao = null,
        Stage = StageLancamentoEnum.Enfileirado
    };

    public sealed class QuandoConverteOLancamentoCriado
    {
        private readonly CriarLancamentoResponse _api = CriadoNaApi(TipoLancamentoEnum.Credito);
        private readonly LancamentoCriadoTela _tela;

        public QuandoConverteOLancamentoCriado() => _tela = new LancamentoCriadoTelaConversor().Converter(_api);

        [Fact]
        public void Entao_preserva_identidade_conta_e_sequencia()
        {
            _tela.Id.Should().Be(_api.Id);
            _tela.ContaId.Should().Be("ABC1234");
            _tela.Sequencia.Should().Be(10);
        }

        [Fact]
        public void Entao_traz_o_valor_cru_e_o_formatado()
        {
            _tela.Valor.Should().Be(1234.50m);
            _tela.ValorFormatado.Replace(' ', ' ').Should().Be("R$ 1.234,50");
        }

        [Fact]
        public void Entao_formata_as_datas_no_horario_de_brasilia()
        {
            _tela.DataLancamentoFormatada.Should().Be("01/10/2026");
            _tela.DataRegistroFormatada.Should().Be("01/10/2026 12:00:00");
        }

        [Fact]
        public void Entao_traduz_o_estagio() =>
            _tela.StageRotulo.Should().Be("Na fila");
    }

    public sealed class QuandoOTipoVaria
    {
        /// <summary>Classes de equivalência do tipo: crédito e débito, com rótulo e sinal.</summary>
        public static TheoryData<TipoLancamentoEnum, string, string> Tipos => new()
        {
            { TipoLancamentoEnum.Credito, "Crédito", "+" },
            { TipoLancamentoEnum.Debito,  "Débito",  "-" },
        };

        [Theory]
        [MemberData(nameof(Tipos))]
        public void Entao_traduz_rotulo_e_sinal(TipoLancamentoEnum tipo, string rotulo, string sinal)
        {
            // Case / When
            var tela = new LancamentoCriadoTelaConversor().Converter(CriadoNaApi(tipo));

            // Then
            tela.TipoRotulo.Should().Be(rotulo);
            tela.Sinal.Should().Be(sinal);
        }
    }
}
