# ADR-SOL-09 — Consumer em worker pool do Cloud Run

- **Status:** Proposta

## Contexto

O Consumer é um Host genérico com *pull subscription*: não abre porta HTTP. Um Cloud Run
Service exige um processo escutando na `PORT`, e o *streaming pull* precisa de CPU o tempo
todo, não só durante uma requisição. O Cloud Scheduler não serve: o Consumer é reativo.

## Decisão

Rodar o Consumer como **Cloud Run worker pool**: sem porta HTTP, CPU sempre alocada, mesma
imagem `runtime:9.0`, com no mínimo uma instância. A disponibilidade do worker pool na região
e o modelo de escala (instâncias fixas ou automáticas) precisam ser confirmados antes da
implantação: o recurso é recente.

## Alternativas consideradas

| Alternativa | Como | Trade-off |
|---|---|---|
| Cloud Run Service com `/health` | Acrescentar Kestrel e um `/health`, imagem `aspnet:9.0`, CPU sempre alocada, `min-instances = 1` | Funciona em qualquer região; um endpoint só para agradar a plataforma |
| **Push subscription** | O Pub/Sub faz `POST` num endpoint; o `BackgroundService` vira uma rota de Minimal API que chama o mesmo `ConsolidadorDeMensagem` | Escala a zero entre mensagens; a ordenação também vale no push. Exige autenticar o push (token OIDC da service account) e trocar a imagem |
| GKE | Deployment comum | Cluster para administrar |

Na AWS, o push equivale ao *event source mapping* de SQS para Lambda.

## Consequências

**Ganhos**
- Nenhuma mudança de código.
- A concorrência continua sendo a do `FlowControlSettings` (20 por instância).

**Custos**
- Instância sempre ligada, mesmo sem mensagens.
- Sem health check: com a assinatura morta, o processo continua no ar sem consumir. A
  observabilidade precisa cobrir isso com a idade da mensagem mais antiga sem ack
  ([plano](../10-observabilidade-e-operacao.md#6-slos-e-alertas)).

## Revisitar quando

O custo da instância ociosa pesar mais que a latência da partida a frio: migrar para push. A
regra já está separada do transporte ([ADR-SW-10](../../software/03-adrs/ADR-SW-10-consumer-com-regra-isolada-do-transporte.md)).
