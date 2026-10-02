using FluentAssertions;
using FluentValidation.TestHelper;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Lancamentos.Commands.CriarLancamento;
using Microsoft.Extensions.Time.Testing;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features;

public sealed class CriarLancamentoValidatorTests
{
    /// <summary>
    /// 21/09/2026, 15h30 UTC — 12h30 em Brasília. Fixo, para que "hoje" e "amanhã"
    /// sejam determinísticos nos cenários de data.
    /// </summary>
    private static readonly DateTimeOffset Agora = new(2026, 9, 21, 15, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Hoje = new(2026, 9, 21);

    private static CriarLancamentoValidator Validator() =>
        new(new FakeTimeProvider(Agora));

    private static CriarLancamentoCommand ComandoValido() => new()
    {
        ContaId = "ABC1234",
        Tipo = TipoLancamentoEnum.Credito,
        Valor = 100.00m,
        DataLancamento = Hoje,
        Observacao = null
    };

    public sealed class QuandoOComandoEstaCompleto
    {
        [Fact]
        public void Entao_passa_sem_erros()
        {
            // Case
            var comando = ComandoValido();

            // When
            var resultado = Validator().TestValidate(comando);

            // Then
            resultado.ShouldNotHaveAnyValidationErrors();
        }
    }

    public sealed class QuandoValidaAConta
    {
        /// <summary>
        /// Classes de equivalência da conta: vazia, curta demais, no limite inferior,
        /// no limite superior, longa demais, e com caractere fora do padrão.
        ///
        /// A conta vira ordering key no Pub/Sub — daí a restrição a letras, dígitos
        /// e hífen, sem espaços.
        /// </summary>
        public static TheoryData<string, bool> Contas => new()
        {
            // conta, deve passar
            { "",                        false },  // vazia
            { "  ",                      false },  // só espaços
            { "AB",                      false },  // abaixo do mínimo (3)
            { "ABC",                     true  },  // no mínimo
            { "ABC1234",                 true  },  // caso comum
            { "abc1234",                 true  },  // minúsculas: normaliza depois
            { "ABC-1234",                true  },  // hífen é permitido
            { "12345678901234567890",    true  },  // no máximo (20)
            { "123456789012345678901",   false },  // acima do máximo
            { "ABC 1234",                false },  // espaço no meio
            { "ABC_1234",                false },  // sublinhado não entra
            { "CONTA#1",                 false },  // símbolo
        };

        [Theory]
        [MemberData(nameof(Contas))]
        public void Entao_aceita_apenas_o_formato_de_chave(string conta, bool devePassar)
        {
            // Case
            var comando = ComandoValido() with { ContaId = conta };

            // When
            var resultado = Validator().TestValidate(comando);

            // Then
            if (devePassar)
            {
                resultado.ShouldNotHaveValidationErrorFor(x => x.ContaId);
            }
            else
            {
                resultado.ShouldHaveValidationErrorFor(x => x.ContaId);
            }
        }
    }

    public sealed class QuandoValidaOValor
    {
        /// <summary>
        /// Classes de equivalência do valor: negativo, zero, mínimo aceitável,
        /// casas decimais além de duas, teto e além do teto.
        /// </summary>
        public static TheoryData<decimal, bool> Valores => new()
        {
            // valor, deve passar
            { -1m,            false },
            { 0m,             false },
            { 0.01m,          true  },  // menor valor possível
            { 100m,           true  },
            { 0.001m,         false },  // três casas decimais
            { 1_000_000m,     true  },  // no teto
            { 1_000_000.01m,  false },  // acima do teto
        };

        [Theory]
        [MemberData(nameof(Valores))]
        public void Entao_exige_positivo_com_duas_casas_ate_o_teto(decimal valor, bool devePassar)
        {
            // Case
            var comando = ComandoValido() with { Valor = valor };

            // When
            var resultado = Validator().TestValidate(comando);

            // Then
            if (devePassar)
            {
                resultado.ShouldNotHaveValidationErrorFor(x => x.Valor);
            }
            else
            {
                resultado.ShouldHaveValidationErrorFor(x => x.Valor);
            }
        }
    }

    public sealed class QuandoValidaADataDoLancamento
    {
        /// <summary>
        /// Classes de equivalência da data: passado, hoje e futuro.
        ///
        /// "Hoje" é calculado no fuso de São Paulo. Às 15h30 UTC do dia 21, em
        /// Brasília ainda é dia 21 — então 21 passa e 22 não.
        /// </summary>
        public static TheoryData<DateOnly, bool> Datas => new()
        {
            { new DateOnly(2020, 1, 1),   true  },  // passado distante
            { new DateOnly(2026, 9, 20),  true  },  // ontem
            { new DateOnly(2026, 9, 21),  true  },  // hoje
            { new DateOnly(2026, 9, 22),  false },  // amanhã
            { new DateOnly(2030, 1, 1),   false },  // futuro distante
        };

        [Theory]
        [MemberData(nameof(Datas))]
        public void Entao_recusa_data_futura(DateOnly data, bool devePassar)
        {
            // Case
            var comando = ComandoValido() with { DataLancamento = data };

            // When
            var resultado = Validator().TestValidate(comando);

            // Then
            if (devePassar)
            {
                resultado.ShouldNotHaveValidationErrorFor(x => x.DataLancamento);
            }
            else
            {
                resultado.ShouldHaveValidationErrorFor(x => x.DataLancamento);
            }
        }

        [Fact]
        public void Entao_usa_o_fuso_de_brasilia_e_nao_utc()
        {
            // Case — 21/09 às 02h UTC é ainda 20/09 às 23h em Brasília.
            // Um lançamento datado de 21/09 seria "futuro" para quem lança no Brasil.
            var madrugadaUtc = new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero);
            var validator = new CriarLancamentoValidator(new FakeTimeProvider(madrugadaUtc));

            var comando = ComandoValido() with { DataLancamento = new DateOnly(2026, 9, 21) };

            // When
            var resultado = validator.TestValidate(comando);

            // Then — 21/09 ainda não chegou em Brasília, então é futuro.
            resultado.ShouldHaveValidationErrorFor(x => x.DataLancamento);
        }
    }

    public sealed class QuandoValidaAObservacao
    {
        /// <summary>Classes de equivalência do tamanho: nula, vazia, no limite e acima.</summary>
        public static TheoryData<string?, bool> Observacoes => new()
        {
            { null,                                             true  },
            { "",                                               true  },
            { "Pagamento de fornecedor",                        true  },
            { new string('a', CriarLancamentoValidator.TamanhoMaximoObservacao),     true  },
            { new string('a', CriarLancamentoValidator.TamanhoMaximoObservacao + 1), false },
        };

        [Theory]
        [MemberData(nameof(Observacoes))]
        public void Entao_limita_o_tamanho(string? observacao, bool devePassar)
        {
            // Case
            var comando = ComandoValido() with { Observacao = observacao };

            // When
            var resultado = Validator().TestValidate(comando);

            // Then
            if (devePassar)
            {
                resultado.ShouldNotHaveValidationErrorFor(x => x.Observacao);
            }
            else
            {
                resultado.ShouldHaveValidationErrorFor(x => x.Observacao);
            }
        }
    }

    public sealed class QuandoOTipoNaoExisteNoEnum
    {
        [Fact]
        public void Entao_recusa()
        {
            // Case — valor fora dos definidos, como chegaria de um JSON inválido.
            var comando = ComandoValido() with { Tipo = (TipoLancamentoEnum)99 };

            // When
            var resultado = Validator().TestValidate(comando);

            // Then
            resultado.ShouldHaveValidationErrorFor(x => x.Tipo);
        }
    }

    public sealed class QuandoVariosCamposEstaoInvalidos
    {
        [Fact]
        public void Entao_reporta_todos_de_uma_vez()
        {
            // Case
            var comando = new CriarLancamentoCommand
            {
                ContaId = "",
                Tipo = TipoLancamentoEnum.Credito,
                Valor = 0m,
                DataLancamento = new DateOnly(2030, 1, 1),
                Observacao = new string('a', 500)
            };

            // When
            var resultado = Validator().TestValidate(comando);

            // Then — o usuário corrige tudo numa passada, não um erro por vez.
            resultado.Errors.Select(e => e.PropertyName).Distinct()
                .Should().BeEquivalentTo("ContaId", "Valor", "DataLancamento", "Observacao");
        }
    }
}
