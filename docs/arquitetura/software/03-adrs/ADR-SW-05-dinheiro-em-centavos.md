# ADR-SW-05 — Dinheiro em centavos

- **Status:** Aceita
- **Escopo:** banco, evento, `DinheiroHelper`

## Contexto

O saldo é uma soma de milhares de lançamentos. Ponto flutuante acumularia erro em cima do
saldo. A POC começou em SQLite, que não tem tipo decimal exato.

## Decisão

- **No banco e no evento, inteiro em centavos** (`bigint`, `long`): `ValorCentavos` e
  `SaldoCentavos`.
- **Valor sempre positivo; o sinal vem do `Tipo`.** `LancamentoEntity.DeltaCentavos` devolve
  o valor negativo para débito.
- **Na API, reais em `decimal`**, com até duas casas (`PrecisionScale(18, 2)`), maior que zero
  e até R$ 1.000.000,00, um teto de sanidade contra digitação errada.
- **Conversão num lugar só**: `DinheiroHelper.ParaCentavos` (arredonda com
  `MidpointRounding.AwayFromZero`) e `ParaReais`.
- **Formatação no servidor**: `LancamentoHelper.Moeda` devolve `R$ 1.234,50` em pt-BR.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| `float` ou `real` | Erro acumulado no `SUM()` |
| `decimal(19,2)` no SQL Server | Também seria exato. Manter centavos evita conversão no SQL e deixa o evento com um inteiro, sem depender de como cada linguagem lê número decimal em JSON |
| `money` | Tipo específico do SQL Server, com arredondamento próprio em divisão |

## Consequências

**Ganhos**
- `SUM` e soma no saldo são exatos.
- Com teto de 10⁸ centavos por lançamento, o `bigint` comporta mais de 92 bilhões de
  lançamentos no teto antes de transbordar.

**Custos**
- Duas representações (reais na API, centavos no banco e no evento): quem lê o banco ou a
  mensagem precisa saber a unidade. O nome da coluna (`…Centavos`) diz.
- Moeda única. Outra moeda pediria uma coluna de moeda e o expoente dela.

## Onde está no código

- [`Common/Helpers/DinheiroHelper.cs`](../../../../MeusLancamentosDiarios.Integrator.Common/Helpers/DinheiroHelper.cs), coberto por 21 testes
- [`CriarLancamentoValidator`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Lancamentos/Commands/CriarLancamento/CriarLancamentoValidator.cs): limites e casas decimais

## Revisitar quando

Houver segunda moeda, ou valor com mais de duas casas (câmbio, juros).
