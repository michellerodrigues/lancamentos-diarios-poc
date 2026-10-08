# ADR-SW-09 — Ciclo do relay independente do host

- **Status:** Aceita, não executada no GCP
- **Escopo:** `.Events`

## Contexto

No local, o relay roda sempre ligado e varre a tabela a cada 2 s. No GCP, a decisão é um
Cloud Run Job disparado pelo Cloud Scheduler a cada minuto
([ADR-SOL-04](../../solucao/03-adrs/ADR-SOL-04-publicacao-hibrida.md)). O Job espera um
processo que faz o trabalho e termina, com código de saída diferente de zero quando falha.

## Decisão

- O ciclo vive em `RelayLancamentos.ExecutarCicloAsync()`: público, sem dependência de host,
  devolvendo quantos lançamentos publicou.
- `RelayWorker` (um `BackgroundService`) escolhe o modo por `Relay:LoopContinuo`:
  - `true`: `PeriodicTimer` com `Relay:Intervalo`. Falha num ciclo vai para o log e o
    próximo tick tenta de novo.
  - `false`: um ciclo, e então `IHostApplicationLifetime.StopApplication()`. Exceção no ciclo
    deixa `Environment.ExitCode = 1`.
- Antes do ciclo, o worker garante o esquema e, com `PubSub:CriarRecursos`, cria tópico e
  subscription.
- A imagem traz `Relay__LoopContinuo=false`; o compose sobrescreve para `true`.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Cloud Functions de 2ª geração | Exige reescrever o projeto no formato do `Functions.Framework` |
| Um console separado só para o Job | Dois pontos de entrada para o mesmo ciclo |
| Agendador dentro do processo (Hangfire, Quartz) | Precisa de processo sempre ligado e, no caso do Hangfire, de armazenamento próprio |

## Consequências

**Ganhos**
- O mesmo código e a mesma imagem nos dois ambientes.
- O código de saída é o que o Cloud Run Job usa para a política de retentativa
  (`--max-retries 3`).

**Custos**
- Cada rodada no GCP paga a subida do container: medido entre 1,9 s e 3,9 s no local, já
  contando conexão e encerramento.
- Cada rodada roda o script do esquema, o que exige que o usuário do banco enxergue os
  metadados das tabelas.
- O piso do Cloud Scheduler é 1 minuto; o atraso do caminho de recuperação herda esse piso.

## Onde está no código

- [`Events/Publishing/RelayWorker.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/Publishing/RelayWorker.cs)
- [`Events/Publishing/RelayLancamentos.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/Publishing/RelayLancamentos.cs)
- [`Events/Dockerfile`](../../../../MeusLancamentosDiarios.Integrator.Events/Dockerfile) e [`deploy/events-cloud-run-job.sh`](../../../../deploy/events-cloud-run-job.sh)

## Revisitar quando

O relay em lote, medido em [capacidade-do-relay.md](../../../capacidade-do-relay.md), sair da
bancada: o ciclo passa a repetir blocos com orçamento de tempo, e o orçamento precisa ficar
abaixo do `--task-timeout` do Job.
