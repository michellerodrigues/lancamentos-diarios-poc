# ADR-SW-03 — Estágio na própria linha como outbox

- **Status:** Aceita
- **Escopo:** `LancamentosDiarios`, WebApi, relay, Consumer

## Contexto

Gravar o lançamento e publicar o evento não cabem numa transação só: banco e broker não
compartilham commit. Publicar antes do commit pode anunciar um lançamento que não existe;
publicar depois pode perder o evento se o processo cair entre os dois. E o Pub/Sub entrega
pelo menos uma vez, então o Consumer recebe repetições.

Além disso, cada lançamento precisa mostrar onde está na esteira, para a tela e para quem
investiga.

## Decisão

Não há tabela de outbox separada. A própria linha de `LancamentosDiarios` carrega o estágio
(`Stage`) e as colunas de controle:

| Coluna | Para quê |
|---|---|
| `Stage` | `Cadastrado`, `Lido`, `Enfileirado`, `EmProcessamento`, `Consolidado` ou `Erro` |
| `StageAtualizadoEm` | Quando a linha entrou no estágio. Faz a reserva vencer |
| `TentativasEnvio` | Falhas e sucessos de publicação |
| `TentativasProcessamento` | Tentativas de consolidação |
| `ProximaTentativaEm` | Backoff da publicação |
| `MensagemId` | Id que o broker devolveu |
| `Erro` | Último motivo de falha, truncado em 1.000 caracteres |

Regras:

1. A linha nasce `Cadastrado` no mesmo `INSERT` do lançamento.
2. **Toda transição é um `UPDATE` com o estágio esperado no `WHERE`** (compare-and-set). Se a
   linha já saiu daquele estágio, nada muda, e quem chamou sabe pelo número de linhas.
3. **`Cadastrado`, `Lido` e `Erro` bloqueiam os sucessores da conta na publicação**: o
   lançamento N+1 não é publicado enquanto o N estiver em um deles.
4. O Consumer aceita `Lido` além de `Enfileirado`, porque a mensagem pode chegar antes de o
   publicador gravar `Enfileirado`.

O diagrama de estados completo está em [05 · Modelo de dados](../05-modelo-de-dados.md#2-máquina-de-estados-do-lançamento).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Tabela de outbox separada | Duplica o dado e pede uma junção para saber o estado do lançamento |
| Publicar dentro da transação (dual write) | Sem atomicidade entre banco e broker: evento sem lançamento, ou lançamento sem evento |
| Change Data Capture com Debezium (transaction log tailing) | Infraestrutura a mais (Kafka Connect ou similar) para um fluxo que o polling resolve |
| Tabela de mensagens processadas no Consumer | O estágio da linha já diz se o lançamento foi consolidado |

## Consequências

**Ganhos**
- Um `SELECT` diz onde está cada lançamento, e `GET /lancamentos/{id}` expõe isso.
- Idempotência sem tabela extra: a reentrega encontra a linha adiante e recebe ack.
- A ordem por conta é garantida na publicação, onde o broker não teria como consertar um
  buraco.

**Custos**
- A tabela de negócio carrega colunas técnicas.
- Cada lançamento sofre de quatro a cinco `UPDATE`. Como `Stage` é chave do índice
  `IX_LancamentosDiarios_Stage`, cada transição move a entrada no índice.
- Só o estágio atual é guardado: o histórico das transições se perde.
- `Erro` é terminal e não há ferramenta para reprocessar. Na consolidação, um `Erro` não
  segura os sucessores, que consolidam por cima (lacuna de RF-06).

## Onde está no código

- [`Common/StageLancamentoEnum.cs`](../../../../MeusLancamentosDiarios.Integrator.Common/StageLancamentoEnum.cs)
- [`Events/Data/LancamentoRepository.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/Data/LancamentoRepository.cs): `EstagiosBloqueantes` e um método por transição

## Revisitar quando

- Auditoria pedir o histórico das transições: uma tabela de transições, ou uma tabela
  temporal do SQL Server (`SYSTEM_VERSIONING`), resolvem sem mudar o fluxo.
- O volume fizer o custo das atualizações no índice de estágio aparecer na medição.
