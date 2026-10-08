# ADR-SW-10 — Consumer com a regra separada do transporte

- **Status:** Aceita
- **Escopo:** `.Consumer`

## Contexto

O Consumer recebe mensagens por *pull subscription* do Pub/Sub e decide, para cada uma, se
consolida, se descarta ou se devolve à fila. Testar essa decisão com o `SubscriberClient`
exigiria um broker. E o transporte pode mudar: uma *push subscription* entregaria a mesma
mensagem por HTTP.

## Decisão

- **`ConsolidacaoWorker`** cuida só do transporte: cria o `SubscriberClient`, aplica o
  controle de fluxo (`Consumer:Concorrencia`, 20 mensagens em andamento), traduz a resposta
  em `Ack` ou `Nack` e para com 10 s de folga no desligamento.
- **`ConsolidadorDeMensagem`** recebe o corpo e o id da mensagem e devolve
  `RespostaAoBrokerEnum`:

| Situação | Resposta | Por quê |
|---|---|---|
| Corpo não é JSON válido ou é `null` | `Confirmar` | Reentregar não conserta o corpo |
| A linha não está em `Lido` nem `Enfileirado` | `Confirmar` | Mensagem repetida, ou lançamento já finalizado |
| Consolidou | `Confirmar` | — |
| A linha saiu de `EmProcessamento` antes de consolidar | `Confirmar` | Reentregar não ajudaria |
| Exceção na consolidação | `Devolver` | A linha volta para `Enfileirado`, ou `Erro` na 5ª tentativa |

- O consolidador usa do corpo só o `lancamentoId`. Conta, sequência e valor vêm da linha no
  banco.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Regra dentro do callback do `SubscriberClient` | Testável só com broker |
| Push subscription com endpoint HTTP | Proposta para escalar a zero; reaproveitaria o `ConsolidadorDeMensagem` sem mudança ([ADR-SOL-09](../../solucao/03-adrs/ADR-SOL-09-consumer-em-worker-pool.md)) |

## Consequências

**Ganhos**
- A regra tem cobertura completa sem broker: 16 testes, 45 de 45 linhas.
- Trocar o transporte não toca na regra.
- Mensagem adulterada não muda valor: o corpo só aponta qual linha consolidar.

**Custos**
- Mensagem descartada some, deixando só o log de erro. Não há *dead-letter topic*.
- O transporte (`ConsolidacaoWorker`) não tem teste: ack, nack, concorrência e desligamento
  só foram exercitados à mão.
- O Consumer não tem health check: com a assinatura morta, o processo continua no ar sem
  consumir ([RNF-04](../../../requisitos-nao-funcionais.md#disponibilidade-e-resiliência)).

## Onde está no código

- [`Consumer/ConsolidacaoWorker.cs`](../../../../MeusLancamentosDiarios.Integrator.Consumer/ConsolidacaoWorker.cs)
- [`Consumer/ConsolidadorDeMensagem.cs`](../../../../MeusLancamentosDiarios.Integrator.Consumer/ConsolidadorDeMensagem.cs)

## Revisitar quando

A subscription ganhar *dead-letter topic* e política de retentativa: o descarte do corpo
inválido pode passar a ser `Devolver`, para a mensagem terminar no tópico de mortos em vez
de sumir.
