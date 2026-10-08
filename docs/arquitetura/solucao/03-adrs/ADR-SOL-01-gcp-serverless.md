# ADR-SOL-01 — GCP com serviços gerenciados e serverless

- **Status:** Aceita, não executada (nada foi implantado)

## Contexto

O alvo de implantação da POC é o Google Cloud. A equipe vem da AWS, e o README traz a
tradução serviço a serviço. O sistema tem carga baixa com picos, um caminho síncrono curto
(lançar, consultar) e um assíncrono (publicar, consolidar). A operação deve ser mínima: sem
servidor para atualizar, sem cluster para administrar.

## Decisão

| Necessidade | Serviço |
|---|---|
| Front, BFF e WebApi | Cloud Run Services |
| Relay | Cloud Run Job disparado pelo Cloud Scheduler |
| Consumer | Cloud Run worker pool ([ADR-SOL-09](ADR-SOL-09-consumer-em-worker-pool.md)) |
| Banco | Cloud SQL for SQL Server com IP privado ([ADR-SOL-02](ADR-SOL-02-sql-server-como-motor-unico.md)) |
| Mensageria | Pub/Sub ([ADR-SOL-03](ADR-SOL-03-consolidacao-assincrona-pubsub.md)) |
| Entrada HTTPS | Load Balancer externo com certificado gerenciado ([ADR-SOL-08](ADR-SOL-08-load-balancer-como-entrada.md)) |
| Segredos | Secret Manager ([ADR-SOL-10](ADR-SOL-10-segredos-e-imagem-unica.md)) |
| Imagens | Artifact Registry |
| Logs, métricas, traces | Cloud Logging, Cloud Monitoring, Cloud Trace |

Região `southamerica-east1` (São Paulo): usuários no Brasil e dados no país.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| GKE | Controle maior, operação maior. O sistema cabe em Cloud Run |
| Compute Engine | Servidor para administrar |
| Cloud Functions para os workers | Reescrever os projetos no formato de função |
| AWS (ECS Fargate, RDS for SQL Server, SQS FIFO, EventBridge Scheduler) | Fora da premissa; o mapeamento fica no README como rota de saída |

## Consequências

**Ganhos**
- Nenhum servidor nem cluster para manter; TLS e escala gerenciados.
- Front e BFF podem escalar a zero fora do horário, se a latência da partida a frio for
  aceitável.
- O código é portável: são containers comuns, e o broker fica atrás de
  `IPublicadorEventos`.

**Custos**
- Partida a frio do .NET no Cloud Run. Em produção, BFF e WebApi com pelo menos uma instância.
- O Cloud Scheduler não dispara em menos de 1 minuto ([ADR-SOL-04](ADR-SOL-04-publicacao-hibrida.md)).
- Dependência moderada do fornecedor: a semântica de ordenação do Pub/Sub, o modelo de Job
  do Cloud Run e o Load Balancer com NEG serverless não têm equivalente idêntico fora do GCP.
- `southamerica-east1` costuma ter preço maior que as regiões dos EUA; conferir na
  calculadora antes de fechar o orçamento.

## Revisitar quando

A carga deixar de ser intermitente (instâncias mínimas sempre ocupadas podem sair mais caras
que capacidade reservada), ou surgir exigência de outra nuvem.
