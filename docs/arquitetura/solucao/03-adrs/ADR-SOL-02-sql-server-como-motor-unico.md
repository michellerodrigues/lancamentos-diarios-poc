# ADR-SOL-02 — SQL Server como motor único

- **Status:** Aceita

## Contexto

A POC começou com SQLite. Três problemas apareceram: sem decimal exato, *type handlers* do
Dapper para `Guid`, `DateOnly` e `DateTimeOffset` (cada um produziu um bug antes de acertar),
e nenhum caminho gerenciado na nuvem com o mesmo motor. As garantias do fluxo dependem de
dicas de trava do SQL ([ADR-SW-04](../../software/03-adrs/ADR-SW-04-concorrencia-no-banco.md)),
e escrever o mesmo SQL em dois dialetos dobraria o risco.

## Decisão

- **Local:** SQL Server 2022 (edição Developer) em container, com volume persistente.
- **GCP:** Cloud SQL for SQL Server, com IP privado e porta 1433.
- **Conexão:** não existe Cloud SQL Connector para .NET, e o socket Unix do Cloud SQL atende
  só MySQL e PostgreSQL. A aplicação conecta direto no IP privado, com Direct VPC egress. A
  connection string inteira vem do Secret Manager.
- **Um dialeto, um caminho de código.**

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Continuar no SQLite | Os problemas acima; sem serviço gerenciado |
| Cloud SQL for PostgreSQL | Sem custo de licença. Tem os equivalentes (`FOR UPDATE SKIP LOCKED`, `RETURNING`, `ON CONFLICT`), mas exige reescrever o repositório e os bootstrappers. Não foi a escolha registrada; é a primeira alternativa se o custo pesar |
| Spanner, AlloyDB | Dimensionados para outra escala |
| Firestore | Sem as transações e travas em que o fluxo se apoia |

## Consequências

**Ganhos**
- O que roda no local é o que roda na nuvem, inclusive o plano de execução.
- Sem *type handlers*; `datetimeoffset`, `date` e `uniqueidentifier` mapeiam direto.

**Custos**
- **Licença.** No Cloud SQL, a licença do SQL Server é cobrada por vCPU e tende a ser o maior
  custo fixo da solução ([NFRs de solução](../08-nfrs-de-solucao.md#7-custo)). Para um piloto
  pequeno, a edição Express não tem licença, mas limita cada banco a 10 GB; conferir os
  limites de recursos e de alta disponibilidade dessa edição no Cloud SQL.
- **TLS sem conector.** Com `Encrypt=True` e `TrustServerCertificate=False`, o cliente valida o
  certificado do servidor, que não é de uma CA pública: a imagem precisa confiar na CA do
  Cloud SQL, e o nome no certificado precisa casar com o configurado
  ([implantação](../07-implantacao-e-infraestrutura.md#5-componentes-no-gcp)).
- Réplica de leitura e réplica em outra região têm requisitos de edição no SQL Server;
  conferir antes de contar com elas para leitura ou DR.

## Revisitar quando

O custo de licença dominar o orçamento, ou a necessidade de réplica de leitura esbarrar na
edição. A troca para PostgreSQL é contida: o SQL está todo em dois repositórios e dois
bootstrappers.
