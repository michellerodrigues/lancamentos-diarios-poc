# ADR-SOL-04 — Publicação imediata e relay por Job

- **Status:** Aceita, não executada no GCP

## Contexto

O outbox precisa de alguém que publique o que está pendente. No local, o relay roda a cada
2 s. No GCP, a decisão era Cloud Run Job disparado pelo Cloud Scheduler, e o Scheduler não
dispara em menos de 1 minuto: um lançamento poderia esperar até 60 s antes de ser publicado,
com o aviso de consolidação na tela o tempo todo.

## Decisão

As quatro opções avaliadas, do [README](../../../../README.md#o-scheduler-tem-granularidade-mínima-de-1-minuto):

| Opção | Latência | Custo e complexidade |
|---|---|---|
| 1. Aceitar o minuto | até 60 s | nenhum |
| 2. Cloud Tasks se reagendando a cada 5 s | ~5 s | ~17 mil execuções por dia; não escala a zero |
| 3. Relay sempre ligado (`min-instances = 1`) | ~2 s | uma instância parada |
| **4. Híbrido** | **milissegundos** | algumas linhas no handler |

**Escolhida a 4:**

- A WebApi grava o lançamento e, **depois do commit**, publica na mesma requisição, desde que
  nenhum lançamento anterior da conta esteja pendente. Espera no máximo 2 s e responde 201 de
  qualquer jeito.
- O relay vira rede de proteção: **Cloud Run Job a cada minuto**, `LoopContinuo=false`,
  `--tasks 1 --parallelism 1`, `--task-timeout 50s` (menor que o intervalo, para duas
  execuções não se sobreporem) e `--max-retries 3`. Recolhe falha do broker, conta com
  anterior pendente, lote e reserva vencida.
- A WebApi e o relay usam o mesmo `RelayLancamentos.PublicarAsync`, e a reserva
  (`Cadastrado` → `Lido`) impede que os dois publiquem a mesma linha.

## Consequências

**Ganhos**
- Caminho feliz sem esperar o agendador.
- A garantia de entrega continua sendo a do outbox: commit primeiro, publicação depois.
- O piso de 1 minuto só pesa na recuperação.

**Custos**
- Na recuperação, um lançamento espera até ~60 s, mais o backoff.
- O relay publica **um lançamento por conta por execução**: uma conta com 60 pendentes leva
  60 minutos. O relay em lote, medido em [capacidade-do-relay.md](../../../capacidade-do-relay.md),
  resolve e ainda está só na bancada.
- Dois publicadores: a WebApi também precisa de permissão de publicação e de saída para o
  Pub/Sub.
- No Cloud Run com CPU alocada só durante a requisição, uma publicação que passe dos 2 s pode
  congelar depois da resposta; a reserva vence em 1 min e o relay recolhe.

## Onde está

[`CriarLancamentoHandler`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Lancamentos/Commands/CriarLancamento/CriarLancamentoHandler.cs),
[`RelayWorker`](../../../../MeusLancamentosDiarios.Integrator.Events/Publishing/RelayWorker.cs),
[`deploy/events-cloud-run-job.sh`](../../../../deploy/events-cloud-run-job.sh).

## Revisitar quando

A latência de recuperação importar para o negócio: a opção 3 é o código de hoje, sem
mudança, rodando como serviço sempre ligado. Ou quando o relay em lote sair da bancada.
