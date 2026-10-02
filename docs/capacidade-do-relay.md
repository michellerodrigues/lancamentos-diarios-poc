# Capacidade do relay por rodada

Na nuvem, o relay (`.Events`) roda uma vez por minuto: Cloud Scheduler disparando um
Cloud Run Job no GCP, EventBridge Scheduler disparando uma task do ECS na AWS. Este
documento responde quanto cabe numa rodada: quantos lançamentos, de quantas contas,
o relay consegue reservar no banco, publicar na fila e marcar como `Enfileirado`
antes da rodada seguinte.

A publicação imediata da WebApi (passo 4a) continua existindo. O relay só trabalha no
que ela não publicou: falha do broker, conta com anterior pendente, `Erro`. Por isso a
capacidade da rodada importa principalmente na **recuperação**, quando uma queda
deixa uma fila acumulada.

## Termos usados aqui

| Termo | O que é |
|---|---|
| **Rodada** | Uma execução do relay disparada pelo agendador. O container sobe, faz o trabalho e encerra. Na nuvem há uma por minuto. No local não há rodada: o `RelayWorker` fica ligado e roda um **ciclo** a cada 2 s, sem subir nem encerrar. |
| **Bloco** | Uma passada de reservar, publicar e marcar dentro da rodada. Hoje a rodada tem um bloco só, de até 50 lançamentos. Na proposta, ela repete blocos até esvaziar a fila ou até o limite de tempo. |
| **Bancada** | O programa de medição (benchmark) em `tools/RelayBench`. Não faz parte do sistema; só serviu para medir. |

## Resposta curta

- **Hoje cabe no máximo 50 lançamentos por rodada, e só um por conta.** Uma conta com
  60 pendentes precisa de 60 rodadas: uma hora na nuvem.
- **Com o relay em lote, cabe a fila inteira de dezenas de milhares de contas.**
  Medido: 30.000 contas com 1 lançamento cada em 27,6 s; 30.000 lançamentos em
  10.000 contas em 6,7 s.
- **O gargalo é a publicação quando cada conta tem um lançamento só**, porque cada
  conta vira uma chamada própria ao broker. O banco não é gargalo.
- **Depois da rodada, quem limita é o Consumer.** Ele consome cada conta em série, e
  escala entre contas.

## A janela de uma rodada

| | Tempo |
|---|---|
| Intervalo entre rodadas | 60 s |
| Timeout do Job (`--task-timeout` em `deploy/events-cloud-run-job.sh`) | 50 s |
| Subir o container, conectar e encerrar, medido localmente | 1,9 s a 3,9 s |
| Folga para a partida na nuvem e para não encostar no timeout | ~10 s |
| **Janela útil considerada abaixo** | **35 s** |

O timeout menor que o intervalo existe para duas rodadas nunca rodarem juntas. Na
nuvem a partida inclui alocar a máquina e baixar a imagem, então ela tende a ser
maior que a medida aqui. A folga de 10 s cobre isso; vale confirmar no ambiente real.

**Os 35 s são o teto de trabalho, não a duração normal.** A rodada fica no ar só o
tempo de fazer o que encontrou. Com a publicação imediata cobrindo o caminho feliz,
quase sempre não há nada pendente: ela sobe, não encontra nada e encerra em poucos
segundos (de 1,9 s a 3,9 s medidos localmente, já contando a subida). Só quando uma
falha deixou fila ela trabalha mais, e nunca passa do timeout.

```
normal:       |sobe|nada|fim| .............................. |sobe|nada|fim| ...
              0    ~3 s                                       60 s

com fila:     |sobe|bloco|bloco|bloco|...|fim| .............. |sobe|...
              0                           ≤ 40 s              60 s
```

**Duração curta não quer dizer custo curto.** O Fargate cobra no mínimo 1 minuto por
task, então uma task por minuto custa o mesmo que uma task sempre ligada. Na AWS, um
serviço ECS sempre ligado, com o ciclo de 2 s do `RelayWorker`, custaria o mesmo e
teria latência menor. No Cloud Run Job a cobrança é por tempo de instância; vale
conferir na página de preços se há mínimo por execução antes de comparar com um
serviço sempre ligado.

## Como foi medido

A bancada está em [`tools/RelayBench`](../tools/RelayBench) e usa o código real do
`.Events`: repositório, `RelayLancamentos` e o mesmo formato de mensagem do
`PubSubPublicador`.

- Notebook com 12 núcleos, SQL Server 2022 e emulador do Pub/Sub no Docker Desktop.
- Banco (`LancamentosBench`) e tópico (`bench-lancamentos`) separados dos da demo.
- Mensagem de **367 bytes**: corpo JSON do evento mais os quatro atributos.
- Cada cenário parte da tabela vazia, semeada com todas as linhas em `Cadastrado`.

## Hoje: um lançamento por conta por rodada

A consulta de reserva não traz um lançamento que tenha anterior pendente na mesma
conta. Enquanto o #1 está em `Cadastrado`, o #2 fica de fora do lote, e só entra na
rodada seguinte. O `TamanhoLote = 50` vale para a rodada toda, não por conta.

Depois da reserva, o `ExecutarCicloAsync` publica e marca um lançamento de cada vez,
esperando cada ida ao broker e ao banco.

| Medida | Resultado |
|---|---|
| Rodada cheia: 50 contas, 1 lançamento cada | 2,0 s, ou 40 ms por lançamento |
| Publicar esperando cada mensagem | 17,4 ms por mensagem |
| Marcar `Enfileirado` linha a linha | 11,7 ms por linha |
| Uma conta com 60 pendentes | 60 rodadas |

## Em lote: a conta inteira na mesma rodada

A proposta muda três coisas no relay. A publicação imediata da WebApi não muda.

1. **Reserva de tudo que pode sair, num comando só.** Um anterior pendente que também
   pode sair agora deixa de bloquear. Continua bloqueando só o que não pode sair nesta
   rodada: `Erro`, `Lido` de outra instância e `Cadastrado` esperando o backoff. Assim
   cada conta sai inteira, na ordem, até o primeiro bloqueio.
2. **Publicação sem esperar uma mensagem pela outra.** O cliente do Pub/Sub mantém a
   ordem de chamada dentro da mesma ordering key, então a conta vai em sequência e as
   contas vão em paralelo.
3. **Marcação em lote.** Um `UPDATE` com `OPENJSON` leva até 2.000 linhas de
   `Lido` para `Enfileirado` de uma vez.

| Contas | Por conta | Lançamentos | Reserva | Publicação | Marcação | Total | Por segundo |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1.000 | 1 | 1.000 | 0,13 s | 0,62 s | 0,09 s | 0,84 s | 1.194 |
| 10.000 | 1 | 10.000 | 0,26 s | 6,93 s | 0,59 s | 7,77 s | 1.287 |
| 30.000 | 1 | 30.000 | 2,79 s | 22,47 s | 2,37 s | 27,63 s | 1.086 |
| 10.000 | 3 | 30.000 | 0,83 s | 4,24 s | 1,62 s | 6,69 s | 4.484 |
| 5.000 | 4 | 20.000 | 0,42 s | 1,60 s | 0,81 s | 2,84 s | 7.052 |
| 1.000 | 50 | 50.000 | 0,98 s | 3,91 s | 3,23 s | 8,12 s | 6.160 |
| 1 | 5.000 | 5.000 | 0,15 s | 1,82 s | 0,19 s | 2,16 s | 2.313 |

Em todos os cenários, todas as linhas terminaram em `Enfileirado`.

**A publicação domina quando cada conta tem um lançamento só.** Com ordering key, o
cliente agrupa mensagens por chave: 30.000 contas com 1 lançamento viram 30.000
chamadas ao broker. Com 3 ou 4 por conta, cada chamada leva as mensagens da conta
juntas, as chamadas caem na mesma proporção, e a vazão sobe de ~1.100 para 4.500 a
7.000 por segundo.

## Quanto cabe numa rodada

Com a janela útil de 35 s:

| Perfil da fila | Vazão medida | Numa rodada |
|---|---:|---|
| 1 lançamento por conta, o caso de uma conta por usuário | ~1.100/s | ~38 mil contas (30 mil medidas) |
| 3 por conta | ~4.500/s | ~150 mil lançamentos, ~50 mil contas |
| 4 por conta | ~7.000/s | ~240 mil lançamentos, ~60 mil contas |
| 50 por conta | ~6.200/s | ~215 mil lançamentos, ~4 mil contas |
| Uma conta só | ~2.300/s | ~80 mil lançamentos dessa conta |

Acima de 30 a 50 mil lançamentos os números são extrapolação linear. Para não
depender dela, a rodada trabalha em blocos, descritos abaixo.

## Blocos: N lançamentos por conta, X contas por bloco

A rodada não reserva tudo de uma vez. Ela repete blocos (reserva, publica, marca)
enquanto houver fila e sobrarem mais de 10 s para o timeout. O que não couber fica
para a rodada seguinte. Cada bloco tem dois limites:

- **X contas por bloco.** Entram primeiro as contas que esperam há mais tempo, pela
  cabeça da fila de cada uma.
- **N lançamentos por conta.** Cada conta leva só o começo da sua fila, na ordem da
  sequência. No bloco seguinte, ela continua de onde parou.

O exemplo A com 60, B com 30 e C com 1, com 10 contas e 10 por conta:

| Bloco | A | B | C |
|---|---|---|---|
| 1 | #1 a #10 | #1 a #10 | #1 |
| 2 | #11 a #20 | #11 a #20 | |
| 3 | #21 a #30 | #21 a #30 | |
| 4 a 6 | #31 a #60, 10 por bloco | | |

Seis blocos, todos na mesma rodada. No modelo de hoje, o mesmo exemplo leva 60 rodadas.

Quanto cabe em 35 s, medido:

| Bloco | Fila | Blocos | Publicados | Tempo | Por bloco |
|---|---|---:|---:|---:|---:|
| 10 contas × 10 | 30.000 contas × 1 | 283 | 2.830 de 30.000 | 35 s | 124 ms |
| 100 contas × 10 | 30.000 contas × 1 | 270 | 27.000 de 30.000 | 37 s | 137 ms |
| 500 contas × 10 | 30.000 contas × 1 | 60 | 30.000 | 21 s | 353 ms |
| 10 contas × 10 | 1.000 contas × 10 | 100 | 10.000 | 10,6 s | 106 ms |
| 500 contas × 10 | 1.000 contas × 10 | 2 | 10.000 | 1,5 s | 774 ms |

**Cada bloco tem um custo fixo de uns 110 ms**, e com blocos pequenos é ele que manda.
Medindo por fase, num bloco de 10 contas:

| Fase | Fila de 30 mil linhas | Fila de 10 mil linhas |
|---|---:|---:|
| Reserva | 87 ms | 49 ms |
| Publicação | 21 ms | 21 ms |
| Marcação | 10 ms | 46 ms |

A reserva é a maior parte, e cresce com a fila, não com o bloco: ela numera a fila
elegível inteira antes de escolher as contas. A publicação é quase toda a espera de
agrupamento do cliente do Pub/Sub. A marcação varia com o commit no disco do Docker.
Na nuvem cada bloco ainda paga a latência de rede, o que pesa mais a favor de blocos
grandes. Se a fila um dia passar de centenas de milhares de linhas, dá para baratear
a reserva escolhendo as contas antes de numerar.

**Recomendação: N = 10 e X = 500, até 5.000 lançamentos por bloco.**

- **N = 10** limita quanto uma conta muito carregada ocupa de cada bloco, sem atrasar
  a própria conta: A com 60 sai em 6 blocos de ~0,35 s. Na AWS, 10 é também o máximo
  do `SendMessageBatch`, então cada conta vira uma chamada por bloco.
- **X = 500** dilui o custo fixo. Com ele, 30.000 contas com um lançamento cada saíram
  em 21 s, mais rápido que reservar tudo de uma vez (27,6 s).
- **10 contas por bloco não serve como padrão.** Com a fila de 30.000 contas, só 2.830
  sairiam por rodada, e a recuperação de uma queda levaria mais de dez rodadas.

## Limites dos brokers

Documentação oficial, consultada em setembro de 2026.

| | Google Pub/Sub | Amazon SQS FIFO |
|---|---|---|
| Mensagens por chamada | 1.000, até 10 MB | 10 |
| Por conta (ordering key / message group) | 1 MB/s, cerca de 2.700 mensagens de 367 bytes | 3.000 msg/s com lote, porque o grupo vive numa partição só |
| Por fila ou região | 200 MB/s em regiões menores, 4 GB/s nas maiores | 3.000 msg/s por fila no modo padrão; 45.000 msg/s em São Paulo no modo de alta vazão |
| Mensagens em trânsito | — | 120.000 por fila |

Nenhum limite por conta chega perto do que uma pessoa gera numa conta corrente. O
que pode pesar é o limite da fila no SQS: no modo padrão, 3.000 msg/s por 35 s dá
cerca de **105 mil mensagens por rodada**. Acima disso, liga-se o modo de alta vazão.

## O que a nuvem muda nesses números

- **Banco: tende a melhorar.** Localmente, uma ida e volta ao SQL Server leva 1,5 ms,
  e um `UPDATE` com commit leva 11,3 ms. A diferença é o disco do Docker Desktop
  gravando o log de transação. Cloud SQL e RDS, em SSD, gravam mais rápido. Reserva
  e marcação em lote já diluem esse custo.
- **Publicação: só medindo.** O emulador está na mesma máquina, sem rede. No Pub/Sub
  real cada chamada paga a latência até o serviço, e no caso de um lançamento por
  conta há uma chamada por conta. A vazão fica perto de chamadas simultâneas
  divididas pela latência, e esses dois números só aparecem no ambiente real. A
  mesma bancada mede isso, veja abaixo.
- **SQS.** O `SendMessageBatch` leva até 10 mensagens, e mensagens de contas
  diferentes podem ir na mesma chamada. Dentro da conta, o lote seguinte só sai
  depois que o anterior voltar.

## Depois do relay: o Consumer

A fila entrega uma mensagem por vez em cada conta. O Consumer escala entre contas,
mas dentro de uma conta é sempre em série.

| Medida | Local |
|---|---|
| Consolidar um lançamento (`IniciarProcessamento` + `Consolidar`) | 26,5 ms |
| Uma conta, em série | ~38 lançamentos/s |
| Uma instância com `Concorrencia = 20`, estimado | até ~750 lançamentos/s |

Consolidar faz dois commits, então os 26,5 ms locais são quase todos disco. Uma rodada
com 30 mil contas levaria perto de 40 s para ser consolidada por uma instância local.
Para ir mais rápido, sobe-se `Concorrencia` ou o número de instâncias. O recurso
compartilhado passa a ser o banco. Para uma conta por usuário, a série dentro da conta
não é problema.

## Para implementar o relay em lote

Hoje o relay em lote existe só na bancada (`ReservarEmLote`, a publicação sem espera e
`MarcarEmLote` em `tools/RelayBench/Program.cs`). No `RelayLancamentos` faltam:

1. **A consulta de reserva** da bancada (`ReservarBloco`), com os dois limites:
   contas por bloco e lançamentos por conta.
2. **O tratamento de falha por conta.** No Pub/Sub, quando uma mensagem falha, todas
   as seguintes da mesma ordering key falham também. O primeiro que falhou vai para
   `DevolverParaFilaAsync`, que conta tentativa e aplica o backoff. Os seguintes da
   conta vão para `LiberarReservaAsync`, sem gastar tentativa. `ResumePublish` é
   chamado uma vez por conta. Os anteriores ao que falhou já estão publicados e vão
   para `Enfileirado`.
3. **O laço de blocos** com orçamento de tempo, no lugar do ciclo único.
4. **A marcação em lote** no repositório.

A bancada não exercita falhas, só o caminho feliz. O tratamento de falha precisa de
teste próprio.

## Como repetir a medição

Com o `docker compose` no ar:

```bash
cd tools/RelayBench
dotnet run -c Release -- atual              # modelo de hoje
dotnet run -c Release -- lote 30000 1       # contas, lançamentos por conta
dotnet run -c Release -- unitario 300       # publicar e marcar um de cada vez
dotnet run -c Release -- consumidor 1000    # consolidar uma conta em série
dotnet run -c Release -- commit             # ida e volta contra commit
dotnet run -c Release -- blocos 500 10 30000 1   # X contas, N por conta, fila, orçamento de 35 s
dotnet run -c Release -- exemplo 10 10      # A com 60, B com 30, C com 1, bloco a bloco
```

Contra o Pub/Sub real, sem a variável do emulador:

```bash
BENCH_PROJECT=meu-projeto \
BENCH_DB="Server=10.20.0.5,1433;Database=LancamentosBench;User Id=...;Password=...;Encrypt=True;" \
dotnet run -c Release -- lote 30000 1
```

A bancada cria o tópico `bench-lancamentos` e o banco `LancamentosBench` se não
existirem, então a credencial precisa dessas permissões. Rodar de dentro da mesma
região e da mesma VPC do Job dá o número que interessa.
