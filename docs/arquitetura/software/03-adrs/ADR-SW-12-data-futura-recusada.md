# ADR-SW-12 — Data futura recusada, não agendada

- **Status:** Aceita
- **Escopo:** `CriarLancamentoValidator`, tela de novo lançamento

## Contexto

A conta consolida em ordem de sequência, e um lançamento pendente segura os sucessores na
publicação ([ADR-SW-03](ADR-SW-03-outbox-na-propria-linha.md)). Um lançamento com data
futura que ficasse esperando a data travaria a conta até lá: um crédito do dia 6 seguraria
tudo o que fosse lançado até o dia 6.

## Decisão

- O validador recusa `DataLancamento` depois de hoje, com "hoje" calculado no fuso
  `America/Sao_Paulo` pelo `TimeProvider`. Às 21h de Brasília já é o dia seguinte em UTC, e
  um lançamento do dia seria recusado como futuro se a conta fosse em UTC.
- O calendário da tela tem `max` em hoje.
- No lote, a mesma regra vale item a item.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Aceitar e segurar até a data | Trava a conta inteira até a data |
| Aceitar e consolidar já | O saldo deixaria de corresponder às datas, o que pesa quando existir o saldo por dia (RF-08) |
| Agendamento | É outra coisa: uma tabela de agendados e um job que, na data, cria um lançamento comum. Fica para quando houver o requisito |

## Consequências

**Ganhos**
- Nenhuma conta presa por data futura.
- A regra é testada com `FakeTimeProvider`, inclusive na virada do dia.

**Custos**
- A tela calcula "hoje" no fuso do aparelho, e o servidor no de São Paulo. Com o aparelho
  adiantado, a tela aceita uma data que o servidor recusa; com o aparelho atrasado, a tela
  bloqueia uma data válida (lacuna 8 de [requisitos-funcionais.md](../../../requisitos-funcionais.md#lacunas)).
- Não há limite inferior: `02/01/0001` é aceito (lacuna 10).
- Lançamento retroativo entra no saldo de hoje, porque o saldo ainda não é por dia.

## Onde está no código

[`CriarLancamentoValidator.cs`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Lancamentos/Commands/CriarLancamento/CriarLancamentoValidator.cs)
e `hojeIso()` em [`core/moeda.ts`](../../../../MeusLancamentosDiarios.Integrator.FrontEnd/src/app/core/moeda.ts).

## Revisitar quando

Existir o saldo diário: a data passa a decidir em que dia o lançamento entra, e um limite
inferior (por exemplo, a data de abertura da conta) passa a ser necessário.
