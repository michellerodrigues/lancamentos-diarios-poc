# ADR-SW-11 — Polling condicional no front, formatação no servidor

- **Status:** Aceita
- **Escopo:** `.FrontEnd`, `.Bff`

## Contexto

Depois de lançar, o usuário quer ver o saldo mudar sem recarregar a tela. A consolidação leva
de milissegundos a alguns segundos no caminho feliz, e até um minuto na recuperação pelo
relay no GCP. Na maior parte do tempo não há nada em trânsito.

## Decisão

- **Polling condicional.** O BFF devolve `intervaloPollingMs = 2000` enquanto há lançamento
  pendente e `0` quando tudo consolidou. O `LancamentosService` reagenda com `setTimeout`
  só quando o intervalo é positivo, e para ao sair da tela.
- **O service é a única porta para o BFF.** Os componentes leem signals (`saldo`,
  `carregando`, `offline`, `conta`) e não sabem de onde o dado vem.
- **Formatação no servidor.** Moeda, datas em Brasília e rótulos chegam prontos
  (`SaldoTela`, `PendenteTela`), feitos por `LancamentoHelper` no `.Common`. O Angular não usa
  pipes de moeda.
- **Falha mantém o último saldo** na tela e liga o aviso de sem conexão.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| SignalR ou WebSocket | Exige um caminho do Consumer até o BFF: uma segunda subscription ou um backplane (Redis), para economizar cerca de 2 s |
| Server-Sent Events | Mesmo problema de origem do aviso, com conexão aberta por tela |
| Polling fixo | Tráfego em repouso, sem nada para mostrar |
| Formatação no Angular | Web e um eventual app nativo poderiam mostrar textos diferentes |

## Consequências

**Ganhos**
- Em repouso, nenhuma requisição.
- Trocar polling por push mexe só no `lancamentos.service.ts`.

**Custos**
- **2 s fixos, sem recuo**, enquanto há pendente. Se um lançamento travar, a tela aberta
  consulta para sempre ([RNF-36](../../../requisitos-nao-funcionais.md#usabilidade)). Cerca de
  100 telas abertas com consolidação travada já somam 50 consultas por segundo.
- Uma tela aberta antes de um lançamento feito em outro lugar não percebe o pendente até
  ser recarregada.
- Um lançamento em `Erro` não conta como pendente; o polling para e a tela não mostra o erro.

## Onde está no código

- [`FrontEnd/src/app/core/lancamentos.service.ts`](../../../../MeusLancamentosDiarios.Integrator.FrontEnd/src/app/core/lancamentos.service.ts)
- [`Bff/Conversores/SaldoTelaConversor.cs`](../../../../MeusLancamentosDiarios.Integrator.Bff/Conversores/SaldoTelaConversor.cs)
- [`Common/Helpers/LancamentoHelper.cs`](../../../../MeusLancamentosDiarios.Integrator.Common/Helpers/LancamentoHelper.cs)

## Revisitar quando

- Antes de produção: recuo de 2 s até 60 s e parada depois de alguns minutos, com aviso de
  atraso (item 6 do plano em [RNF-01](../../../requisitos-nao-funcionais.md#o-que-fazer-por-ordem-de-retorno)).
- Se o negócio pedir atualização instantânea entre dispositivos: push, com o Consumer
  publicando "saldo mudou" num segundo tópico.
