# ADR-SOL-03 — Consolidação assíncrona por Pub/Sub, com ordering key = conta

- **Status:** Aceita

## Contexto

"O serviço de controle de lançamento não deve ficar indisponível se o sistema de
consolidado diário cair." Lançar e consolidar precisam ser desacoplados. Dentro de uma conta
a ordem importa ("consolidado até o lançamento nº N"); contas diferentes são independentes e
devem andar em paralelo.

## Decisão

- **Um tópico**, `lancamentos-registrados`, e **uma subscription de pull**,
  `lancamentos-consolidacao`, que funciona como a fila do Consumer.
- **Ordering key = `ContaId`.** A sequência vai no corpo; uma chave por mensagem desligaria a
  ordenação sem aviso.
- **Ordenação ligada nos dois lados**: `EnableMessageOrdering` no publicador e na subscription.
- **Publicador** chama `ResumePublish(conta)` quando uma publicação falha: sem isso o cliente
  deixa a chave em estado de erro e a conta nunca mais publica.
- **Consumer** com controle de fluxo de 20 mensagens, somando as contas; o Pub/Sub entrega uma
  por vez dentro de cada conta.
- **Entrega pelo menos uma vez**, com o Consumer idempotente pelo estágio da linha.
- **Prazo de ack de 60 s**, estendido pela biblioteca enquanto a consolidação roda.
- **Provisionamento:** pela aplicação no local, onde o emulador sobe vazio
  (`PubSub:CriarRecursos=true`); por Terraform no GCP (`false`).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Consolidar no próprio request | Viola o requisito |
| Consumer lendo direto da tabela | Funcionaria, já que a linha é o outbox; mas cada novo interessado no evento (saldo diário, notificação) teria de varrer a tabela. Um tópico aceita novas subscriptions sem tocar no publicador |
| Cloud Tasks | Não ordena por chave |
| Kafka gerenciado | Partição por chave ordena; operação e custo maiores para este volume |
| Eventarc | Roteia eventos; não é fila com ordem |

Na AWS, o equivalente é SQS FIFO com `MessageGroupId` = conta.

## Consequências

**Ganhos**
- Lançar não depende do Consumer nem do broker (medido com o Consumer e o relay parados e,
  noutro teste, com o broker pausado).
- Ordem por conta, paralelismo entre contas.
- Fan-out: outra subscription no mesmo tópico recebe tudo, sem mudar a WebApi nem o relay.

**Custos**
- O consumidor tem de ser idempotente (é).
- A ordem só vale para mensagens com a mesma chave publicadas na mesma região. Os
  publicadores ficam em `southamerica-east1`; fixar o endpoint regional do Pub/Sub no cliente
  evita que um roteamento diferente quebre a ordem ([integração](../04-integracao-e-contratos.md#4-mensageria)).
- Hoje a subscription não tem política de retentativa nem *dead-letter topic*: uma falha
  passageira esgota as 5 tentativas em segundos.
- O emulador guarda tudo em memória; reiniciar o container apaga tópico e subscription.

## Revisitar quando

Surgir o saldo por dia (RF-08): é o primeiro candidato a uma segunda subscription no mesmo
tópico, com o seu próprio read model.
