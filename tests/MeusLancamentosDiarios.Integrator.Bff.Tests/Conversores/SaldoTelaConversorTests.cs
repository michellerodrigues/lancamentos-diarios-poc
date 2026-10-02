using FluentAssertions;
using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Tests.Conversores;

public sealed class SaldoTelaConversorTests
{
    /// <summary>
    /// O conversor de pendentes e real: a lista de pendentes faz parte do de-para da
    /// tela, e um dublê ali esconderia justamente o que se quer ver.
    /// </summary>
    private static readonly SaldoTelaConversor Conversor = new(new PendenteTelaConversor());

    /// <summary>A moeda pt-BR separa "R$" do valor com espaço sem quebra.</summary>
    private static string SemEspacoDuro(string texto) => texto.Replace(' ', ' ');

    private static SaldoResponse SaldoDaApi(
        bool consolidacaoEmAndamento,
        IReadOnlyList<LancamentoPendenteResponse>? pendentes = null) => new()
        {
            ContaId = "ABC1234",
            SaldoConsolidado = 500.00m,
            UltimaSequenciaConsolidada = 3,
            AtualizadoEm = new DateTimeOffset(2026, 9, 20, 17, 30, 22, TimeSpan.Zero),
            CreditosPendentes = 100.00m,
            DebitosPendentes = 30.00m,
            SaldoProjetado = 570.00m,
            ConsolidacaoEmAndamento = consolidacaoEmAndamento,
            Pendentes = pendentes ?? []
        };

    private static LancamentoPendenteResponse Pendente(
        long sequencia, TipoLancamentoEnum tipo = TipoLancamentoEnum.Credito, decimal valor = 10m) => new()
        {
            Id = Guid.CreateVersion7(),
            Sequencia = sequencia,
            Tipo = tipo,
            Valor = valor,
            DataLancamento = new DateOnly(2026, 9, 21),
            DataRegistro = DateTimeOffset.UtcNow,
            Observacao = null,
            Stage = StageLancamentoEnum.Enfileirado
        };

    public sealed class QuandoHaLancamentoEmTransito
    {
        [Fact]
        public void Entao_sugere_o_intervalo_de_polling()
        {
            // Case
            var api = SaldoDaApi(consolidacaoEmAndamento: true);

            // When
            var tela = Conversor.Converter(api);

            // Then
            tela.IntervaloPollingMs.Should().Be(SaldoTelaConversor.IntervaloPollingMs);
        }

        [Fact]
        public void Entao_liga_o_aviso_de_consolidacao()
        {
            // Case / When
            var tela = Conversor.Converter(SaldoDaApi(consolidacaoEmAndamento: true));

            // Then
            tela.ConsolidacaoEmAndamento.Should().BeTrue();
        }
    }

    public sealed class QuandoNadaEstaEmTransito
    {
        [Fact]
        public void Entao_zera_o_intervalo_para_o_front_parar_de_consultar()
        {
            // Case
            var api = SaldoDaApi(consolidacaoEmAndamento: false);

            // When
            var tela = Conversor.Converter(api);

            // Then — zero é o sinal de "pode parar". Em repouso o front não faz
            // requisição nenhuma; é o que torna o polling condicional barato.
            tela.IntervaloPollingMs.Should().Be(0);
        }
    }

    public sealed class QuandoMontaOsValoresDaTela
    {
        private readonly SaldoTela _tela = Conversor.Converter(SaldoDaApi(true));

        [Fact]
        public void Entao_traz_os_numeros_crus() =>
            _tela.SaldoConsolidado.Should().Be(500.00m);

        [Fact]
        public void Entao_traz_os_textos_ja_formatados()
        {
            SemEspacoDuro(_tela.SaldoConsolidadoFormatado).Should().Be("R$ 500,00");
            SemEspacoDuro(_tela.SaldoProjetadoFormatado).Should().Be("R$ 570,00");
            SemEspacoDuro(_tela.CreditosPendentesFormatado).Should().Be("R$ 100,00");
            SemEspacoDuro(_tela.DebitosPendentesFormatado).Should().Be("R$ 30,00");
        }

        [Fact]
        public void Entao_converte_a_ultima_atualizacao_para_brasilia() =>
            _tela.UltimaAtualizacaoFormatada.Should().Be("20/09/2026 14:30:22");

        [Fact]
        public void Entao_preserva_a_conta_e_a_sequencia_consolidada()
        {
            _tela.ContaId.Should().Be("ABC1234");
            _tela.UltimaSequenciaConsolidada.Should().Be(3);
        }
    }

    public sealed class QuandoMontaOsPendentes
    {
        /// <summary>
        /// Classes de equivalência do pendente: crédito e débito, cada um com o
        /// rótulo e o sinal que a tela usa.
        /// </summary>
        public static TheoryData<TipoLancamentoEnum, string, string> Tipos => new()
        {
            { TipoLancamentoEnum.Credito, "Crédito", "+" },
            { TipoLancamentoEnum.Debito,  "Débito",  "-" },
        };

        [Theory]
        [MemberData(nameof(Tipos))]
        public void Entao_traduz_tipo_sinal_e_estagio(TipoLancamentoEnum tipo, string rotulo, string sinal)
        {
            // Case
            var pendente = Pendente(sequencia: 4, tipo, valor: 42.00m);

            // When
            var tela = Conversor.Converter(SaldoDaApi(true, [pendente]));

            // Then
            var montado = tela.Pendentes.Single();

            montado.TipoRotulo.Should().Be(rotulo);
            montado.Sinal.Should().Be(sinal);
            montado.StageRotulo.Should().Be("Na fila");
            montado.Sequencia.Should().Be(4);
            SemEspacoDuro(montado.ValorFormatado).Should().Be("R$ 42,00");
            montado.DataLancamentoFormatada.Should().Be("21/09/2026");
        }

        [Fact]
        public void Entao_preserva_a_ordem_recebida_da_api()
        {
            // Case — a WebApi já devolve ordenado; o BFF não reordena.
            var pendentes = new List<LancamentoPendenteResponse> { Pendente(7), Pendente(6), Pendente(5) };

            // When
            var tela = Conversor.Converter(SaldoDaApi(true, pendentes));

            // Then
            tela.Pendentes.Select(p => p.Sequencia).Should().Equal(7, 6, 5);
        }
    }

    public sealed class QuandoAContaNuncaConsolidou
    {
        [Fact]
        public void Entao_a_ultima_atualizacao_vira_nunca()
        {
            // Case — não existe linha no read model para essa conta.
            var api = SaldoDaApi(consolidacaoEmAndamento: false) with
            {
                ContaId = "NOVA1",
                SaldoConsolidado = 0m,
                UltimaSequenciaConsolidada = 0,
                AtualizadoEm = null,
                CreditosPendentes = 0m,
                DebitosPendentes = 0m,
                SaldoProjetado = 0m
            };

            // When
            var tela = Conversor.Converter(api);

            // Then
            tela.UltimaAtualizacaoFormatada.Should().Be("nunca");
            tela.AtualizadoEm.Should().BeNull();
        }
    }
}
