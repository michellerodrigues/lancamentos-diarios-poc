# Plano de observabilidade e operação

Como saber que algo vai mal antes do usuário, seguir um lançamento de ponta a ponta, medir os
SLOs e agir quando um alerta toca. Retrato do commit `45bcd17`, em 07/10/2026.

## 1. Objetivos

1. **Detectar antes do usuário** que o lançamento falha, que a consolidação parou ou que um
   saldo não fecha.
2. **Seguir um lançamento** do clique ao saldo pelo `lancamentoId`, em logs e traces.
3. **Medir os SLOs** de [NFRs de solução](08-nfrs-de-solucao.md#22-slos-propostos), com alerta
   pelo consumo do orçamento de erro.
4. **Agir sem improviso**, com um runbook por alerta.

## 2. Estado atual

| Item | Hoje | Lacuna |
|---|---|---|
| Logs | Templates com `{ContaId}#{Sequencia}`, no console, em texto | Sem JSON, sem correlação entre BFF, WebApi, relay e Consumer |
| Rastreio de um lançamento | A própria linha guarda estágio, tentativas e erro; `GET /lancamentos/{id}` expõe | Não registra quem lançou; sem trace |
| Métricas | Nenhuma da aplicação | — |
| Tracing | Nenhum | — |
| Health checks | `/health` fixo na WebApi e no BFF; nada no relay e no Consumer | Não testam dependência; Consumer sem sinal de vida |
| Alertas e runbooks | Nenhum | — |

Fonte: [RNF-25 a RNF-28](../../requisitos-nao-funcionais.md#observabilidade).

## 3. Logs

**Formato.** Uma linha JSON por evento na saída padrão. O Cloud Logging lê do JSON os campos
`severity`, `message` e `logging.googleapis.com/trace`, este no formato
`projects/<projeto>/traces/<trace-id>`, que liga o log ao trace. O formatter do Google
(`Google.Cloud.Logging.Console`) já escreve assim; um `ConsoleFormatter` próprio também serve.

| Campo | Exemplo | Origem |
|---|---|---|
| `severity` | `WARNING` | nível do `ILogger` |
| `message` | `Publicacao de ABC1234#7 passou de 00:00:02…` | template |
| `servico`, `versao`, `ambiente` | `webapi`, `1.2.0+abc123`, `prd` | escopo do processo |
| `logging.googleapis.com/trace`, `…/spanId` | — | atividade corrente |
| `contaId`, `sequencia`, `lancamentoId` | `ABC1234`, `7`, `0199…` | parâmetros do template |
| `usuarioId` | GUID | `sub` do token |
| `stage`, `tentativa`, `mensagemId` | `Lido`, `3`, `1528…` | parâmetros do template |

**Nunca logar:** senha, token (JWT, renovação, redefinição), connection string, e-mail, texto
da observação.

**Níveis.** `Information` para cada transição de estágio (já existe); `Warning` para
retentativa, teto de publicação estourado e reuso de token; `Error` para o que pede ação.
`Microsoft.AspNetCore` e `Grpc` em `Warning`, como hoje.

**Eventos que já existem e viram sinal**

| Mensagem no código | Uso |
|---|---|
| `LancamentoEntity {ContaId}#{Sequencia} aguarda o relay: ha predecessor pendente na conta.` | Taxa de queda para o relay |
| `Publicacao de {ContaId}#{Sequencia} passou de {Limite}. Respondendo sem esperar.` | Broker lento |
| `Falha ao publicar o lancamento {ContaId}#{Sequencia}.` | Broker fora |
| `Falha ao consolidar o lancamento {ContaId}#{Sequencia}.` | Consolidação falhando |
| `Mensagem {MensagemId} descartada: corpo invalido.` | Mensagem envenenada |
| `Token de renovacao reutilizado pelo usuario {UsuarioId}. Todas as sessoes revogadas.` | Alerta de segurança |
| `Ciclo unico falhou.` | Job do relay falhando |
| `Usuarios de demonstracao garantidos (…). Nao ligue Auth:SemearUsuariosDemo em producao.` | Não pode aparecer em produção |

**Retenção.** 30 dias no bucket padrão. Os eventos de auditoria (login, reuso de token, ações
de Admin, reprocesso) vão também, por um *sink*, para um bucket de logs com retenção de um
ano, para investigação e para a LGPD.

## 4. Métricas

### 4.1 Da aplicação

Com `System.Diagnostics.Metrics` e exportação pelo OpenTelemetry. Medidor
`MeusLancamentosDiarios`.

| Métrica | Tipo | Atributos | Onde | Para quê |
|---|---|---|---|---|
| `lancamentos.registrados` | contador | `tipo`, `via` (avulso, lote) | handlers de criação | Volume |
| `lancamentos.publicacoes` | contador | `caminho` (imediato, relay), `resultado` (ok, falha, teto) | `TentarPublicarAsync`, `RelayLancamentos` | Saúde do caminho feliz |
| `lancamentos.publicacao.duracao` | histograma, ms | `caminho` | `PubSubPublicador` | Latência do broker |
| `lancamentos.consolidacoes` | contador | `resultado` (consolidado, repetido, descartado, falha) | `ConsolidadorDeMensagem` | Saúde do Consumer |
| `lancamentos.consolidacao.atraso` | histograma, s | — | `ConsolidadorDeMensagem`: agora − `DataRegistro` | SLO de saldo em dia |
| `lancamentos.pendentes` | gauge observável | `stage` | consulta R4, a cada minuto | Esteira parada |
| `lancamentos.pendente_mais_antigo.idade` | gauge observável, s | `stage` | consulta R4 | Alerta |
| `lancamentos.contas_em_erro` | gauge observável | `etapa` (publicação, consolidação) | consulta R6 | Alerta |
| `saldo.divergencias` | gauge | — | reconciliação R1, diária | SLO de saldo correto |
| `relay.ciclo.publicados`, `relay.ciclo.duracao` | contador, histograma | — | `RelayWorker` | Capacidade da rodada |
| `auth.logins` | contador | `metodo` (senha, google), `resultado` | handlers de login | Força bruta |
| `auth.renovacao.reusos` | contador | — | `RenovarSessaoHandler` | Segurança |
| `http.server.request.duration` | histograma | rota, status | ASP.NET Core, já embutida no .NET 9 | RED por rota |

As consultas de reconciliação (R1 a R6) estão em
[dados macro](05-dados-macro-e-migracao.md#6-reconciliação).

### 4.2 Da plataforma

| Métrica do Cloud Monitoring | Sinal |
|---|---|
| `pubsub.googleapis.com/subscription/oldest_unacked_message_age` | Consumer parado ou lento |
| `pubsub.googleapis.com/subscription/num_undelivered_messages` | Fila acumulando |
| `pubsub.googleapis.com/subscription/dead_letter_message_count` | Mensagem foi para a *dead-letter* |
| `run.googleapis.com/request_count`, `run.googleapis.com/request_latencies` | RED de cada serviço |
| `run.googleapis.com/container/instance_count` | Escala e partida a frio |
| `cloudsql.googleapis.com/database/cpu/utilization`, `…/memory/utilization`, `…/disk/utilization` | Saturação do banco |
| Execuções do Cloud Run Job, por resultado | Relay falhando |
| Logs do Load Balancer (status, latência, regra do Cloud Armor) | SLOs de lançar e consultar; bloqueios |

## 5. Tracing

- **SDK:** OpenTelemetry para .NET, com instrumentação de ASP.NET Core, `HttpClient` e
  `SqlClient`. Exportação por OTLP para o Cloud Trace, pelo endpoint do Google ou por um
  coletor.
- **BFF → WebApi:** a propagação W3C (`traceparent`) é automática com a instrumentação do
  `HttpClient`.
- **Através do Pub/Sub:** o `PubSubPublicador` grava o `traceparent` nos atributos da
  mensagem, e o `ConsolidacaoWorker` abre o span de consumo ligado ao span de publicação.
  Quando quem publica é o relay, o trace original da requisição já terminou: o span do relay
  leva o `lancamentoId`, que liga as duas pontas.
- **`lancamentoId` como atributo** em todo span e todo log do fluxo: buscar por ele mostra a
  vida inteira do lançamento.
- **Amostragem:** 10% das requisições bem-sucedidas em produção; erros aparecem sempre nos
  logs.

## 6. SLOs e alertas

**Alertas por consumo do orçamento de erro**, no modelo de janelas múltiplas do *SRE
Workbook*: consumo 14,4 vezes acima do normal em 1 h (confirmado em 5 min) aciona o plantão;
6 vezes em 6 h (confirmado em 30 min) aciona; 1 vez em 3 dias abre chamado.

| Alerta | Condição | Severidade | Runbook |
|---|---|---|---|
| SLO de lançar queimando | consumo acima de 14,4× em 1 h | SEV2 | [RB-06](#rb-06--banco-lento-ou-bloqueios) e logs de 5xx |
| SLO de consultar queimando | idem | SEV2 | RB-06 |
| Esteira parada | pendente mais antigo há mais de 5 min | SEV2 | [RB-03](#rb-03--fila-acumulando-ou-consumer-parado), [RB-04](#rb-04--relay-não-roda) |
| Fila acumulando | `oldest_unacked_message_age` acima de 5 min | SEV2 | RB-03 |
| Relay falhando | 3 execuções seguidas com falha | SEV2 | RB-04 |
| Conta travada na publicação | `contas_em_erro{etapa=publicacao}` > 0 por 5 min | SEV3; SEV2 acima de 10 contas | [RB-01](#rb-01--conta-travada-na-publicação) |
| Erro na consolidação ou *dead-letter* | `contas_em_erro{etapa=consolidacao}` > 0, ou `dead_letter_message_count` > 0 | SEV3 | [RB-02](#rb-02--lançamento-em-erro-na-consolidação-ou-saldo-que-não-fecha) |
| Saldo divergente | reconciliação R1 ou R3 com linhas | SEV2 | RB-02 |
| Preso depois do broker | reconciliação R5 com linhas | SEV3 | [RB-05](#rb-05--lançamento-preso-em-enfileirado-ou-emprocessamento) |
| Reuso de token | `auth.renovacao.reusos` > 0 | SEV3; SEV2 acima de 10 por hora | [RB-07](#rb-07--ataque-ao-login-ou-reuso-de-token) |
| Ataque ao login | logins recusados acima do normal, ou bloqueios do Cloud Armor em `/api/auth/*` | SEV2 | RB-07 |
| Banco saturado | CPU acima de 80% por 15 min; disco acima de 80% | SEV3; SEV2 se a latência subir | RB-06 |
| Usuários de demonstração em produção | o log de semeadura aparece | SEV2 | Desligar `Auth__SemearUsuariosDemo` e trocar as senhas |

| Severidade | Quando | Resposta |
|---|---|---|
| SEV1 | Lançar fora do ar; saldo errado em muitas contas; dado pessoal exposto | Imediata, a qualquer hora |
| SEV2 | Consolidação parada; SLO queimando rápido; ataque em andamento | Em até 30 min, no horário de plantão |
| SEV3 | Uma conta travada; *dead-letter*; reconciliação com poucos itens | Próximo dia útil |

## 7. Painéis

| Painel | Conteúdo |
|---|---|
| Serviço | Indicadores dos SLOs; requisições, erros e latência por rota; instâncias |
| Esteira | Lançamentos por minuto; publicação imediata × relay; pendentes por estágio; idade do mais antigo; atraso da consolidação (p50, p95, p99); contas em `Erro`; fila e *dead-letter* |
| Relay | Execuções, duração, publicados por execução, falhas |
| Banco | CPU, memória, disco, conexões; consultas mais caras |
| Segurança | Logins por resultado e método; reusos de token; 401 e 403 por rota; bloqueios do Cloud Armor |

Painéis e alertas como código, junto do Terraform.

## 8. Health checks

| Processo | Hoje | Proposto |
|---|---|---|
| WebApi | `/health` devolve `ok` sempre | `/health/live`: só o processo. `/health/ready`: o banco responde a um `SELECT 1` em até 2 s |
| BFF | `/health` fixo | Só `/health/live`. A falha da WebApi aparece nas métricas de erro, não no health: senão a queda da WebApi derrubaria também o BFF, e o front perderia até as mensagens de erro |
| Relay | — | O resultado de cada execução do Job é o sinal |
| Consumer | — | Métrica de última mensagem processada; o sinal que importa é a idade da mensagem mais antiga sem ack |

**O ready da WebApi não inclui o Pub/Sub.** O requisito central é lançar com a consolidação
fora do ar; um ready que dependesse do broker tiraria a WebApi do ar junto com ele.

No Cloud Run, as sondas de partida e de vida apontam para `/health/live`. O `/health/ready`
serve à verificação sintética do Cloud Monitoring, junto de um `GET /api/auth/config`
pelo Load Balancer.

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<BancoHealthCheck>("banco", tags: ["ready"]);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
```

## 9. Operação

| Rotina | Frequência | O quê |
|---|---|---|
| Reconciliação | diária, automática | R1 a R6; resultado vira métrica |
| Revisão da *dead-letter* e das contas em `Erro` | diária | Runbooks RB-01 e RB-02 |
| Custo, capacidade e vulnerabilidades | semanal | Orçamento, instâncias, banco, varredura das imagens |
| Revisão dos SLOs | mensal | Orçamento de erro consumido; decide entre prioridade de confiabilidade e de funcionalidade |
| Ensaios de recuperação | semestral | Troca de zona do banco, recuperação pontual ([DR](08-nfrs-de-solucao.md#6-recuperação-de-desastres)) |
| Rotação de segredos | semestral, e na hora em caso de suspeita | RB-08 |

**Plantão no piloto:** das 7h às 22h, todos os dias, porque o comércio funciona no fim de
semana; acionamento a qualquer hora só para SEV1. Todo SEV1 e SEV2 tem análise pós-incidente
sem culpados, com ações no roadmap.

## 10. Runbooks

Todos os comandos SQL rodam com o usuário de operação, nunca com o da aplicação. Os
estágios: 1 `Cadastrado`, 2 `Lido`, 3 `Enfileirado`, 4 `EmProcessamento`, 5 `Consolidado`,
6 `Erro`.

### RB-01 — Conta travada na publicação

**Sintoma.** Alerta de conta em `Erro` na publicação. Na tela, a consolidação fica em
andamento para sempre.

**Causa típica.** O broker ficou fora por mais tempo que as cinco tentativas cobrem (~30 s no
local, ~4 min no GCP), e o primeiro pendente da conta foi para `Erro`, segurando os seguintes.

**Diagnóstico.**

```sql
SELECT Id, Sequencia, Stage, TentativasEnvio, TentativasProcessamento,
       ProximaTentativaEm, StageAtualizadoEm, Erro
FROM dbo.LancamentosDiarios
WHERE ContaId = @Conta AND Stage IN (1, 2, 6)
ORDER BY Sequencia;
```

`Erro` com `TentativasProcessamento = 0` é falha de publicação. Confirmar que o Pub/Sub está
saudável (status do GCP, logs `Falha ao publicar`).

**Ação.** Com o broker de volta, devolver a linha à fila; o relay a publica no próximo ciclo,
na ordem da conta:

```sql
UPDATE dbo.LancamentosDiarios
SET Stage = 1, TentativasEnvio = 0, ProximaTentativaEm = NULL, Erro = NULL,
    StageAtualizadoEm = SYSDATETIMEOFFSET()
WHERE ContaId = @Conta AND Stage = 6 AND TentativasProcessamento = 0;
```

**Verificação.** A conta sai das consultas R4 e R6 em alguns ciclos; R1 limpa para a conta.

**Prevenção.** Falha passageira sem gastar tentativa; reprocesso pela API; F2 do
[roadmap](09-transicao-roadmap-e-riscos.md#3-fases).

### RB-02 — Lançamento em Erro na consolidação ou saldo que não fecha

**Sintoma.** Conta em `Erro` com `TentativasProcessamento > 0`; mensagem na *dead-letter*; ou
reconciliação R1 ou R3 com linhas.

**Impacto.** O lançamento não está no saldo. Hoje os seguintes da conta consolidam por cima,
e o "consolidado até o nº N" fica falso.

**Diagnóstico.** A coluna `Erro` da linha e os logs `Falha ao consolidar` com a conta e a
sequência. Conferir a saúde do banco (RB-06).

**Ação.**

1. Corrigir a causa.
2. Devolver a linha para ser publicada de novo:

   ```sql
   UPDATE dbo.LancamentosDiarios
   SET Stage = 1, TentativasEnvio = 0, TentativasProcessamento = 0,
       ProximaTentativaEm = NULL, Erro = NULL, StageAtualizadoEm = SYSDATETIMEOFFSET()
   WHERE Id = @Id AND Stage = 6;
   ```

3. Depois da consolidação, corrigir a `UltimaSequencia`, que o Consumer sobrescreve com a
   sequência que acabou de consolidar, mesmo que seja antiga:

   ```sql
   UPDATE s
   SET UltimaSequencia = x.Ultima
   FROM dbo.SaldoConsolidado AS s
   CROSS APPLY (
       SELECT MAX(Sequencia) AS Ultima
       FROM dbo.LancamentosDiarios AS l
       WHERE l.ContaId = s.ContaId AND l.Stage = 5
   ) AS x
   WHERE s.ContaId = @Conta;
   ```

4. Se R1 continuar com diferença, reconstruir o saldo com o Consumer parado
   ([script](05-dados-macro-e-migracao.md#55-reconstrução-do-read-model)).
5. Na *dead-letter*, dar ack na mensagem depois de reprocessar: a linha é a fonte.

**Verificação.** R1, R3 e R6 sem a conta.

**Prevenção.** Consolidação numa transação e guarda de ordem no saldo (F2).

### RB-03 — Fila acumulando ou Consumer parado

**Sintoma.** `oldest_unacked_message_age` crescendo; pendentes em `Enfileirado` aumentando.

**Diagnóstico.** Logs do Consumer (exceções; `Consumer ouvindo` na subida); número de
instâncias; saúde do banco; taxa de nack.

**Ação.** Se o processo está vivo sem consumir, reciclar as instâncias do Consumer. Se o
banco é a causa, RB-06. Se é volume, mais instâncias.

**Verificação.** A idade da mensagem mais antiga cai; R4 volta a minutos.

**Prevenção.** Sinal de vida do Consumer; alerta pela idade da fila.

### RB-04 — Relay não roda

**Sintoma.** Execuções do Job com falha; lançamentos em `Cadastrado` acumulando em contas com
pendente; lote parado.

**Diagnóstico.**

```bash
gcloud run jobs executions list --job lancamentos-relay --region southamerica-east1 --limit 10
```

Causas comuns: o Scheduler sem `run.invoker` no Job; a conta do relay sem acesso ao segredo
da connection string; a VPC sem rota até o banco; `Ciclo unico falhou` nos logs.

**Ação.** Corrigir a causa e rodar uma vez à mão:

```bash
gcloud run jobs execute lancamentos-relay --region southamerica-east1
```

**Verificação.** Execução com sucesso; R4 caindo.

**Prevenção.** Alerta por falhas seguidas; relay em lote para encurtar a recuperação.

### RB-05 — Lançamento preso em Enfileirado ou EmProcessamento

**Sintoma.** Reconciliação R5 com linhas.

**Causa.** A mensagem sumiu depois de publicada (emulador reiniciado no local, subscription
recriada, retenção vencida), ou o Consumer caiu entre a reserva e a consolidação.

**Ação.** Devolver para `Cadastrado`; o relay republica:

```sql
UPDATE dbo.LancamentosDiarios
SET Stage = 1, ProximaTentativaEm = NULL, StageAtualizadoEm = SYSDATETIMEOFFSET()
WHERE Id = @Id AND Stage IN (3, 4)
  AND StageAtualizadoEm < DATEADD(MINUTE, -15, SYSDATETIMEOFFSET());
```

É seguro mesmo que a mensagem original ainda chegue: o Consumer só aceita a linha em `Lido`
ou `Enfileirado`, e uma das duas entregas é descartada pelo estágio.

**Verificação.** R5 vazia; R1 limpa.

**Prevenção.** Varredura automática dos presos (F2).

### RB-06 — Banco lento ou bloqueios

**Sintoma.** SLOs de latência queimando; timeouts de SQL nos logs; CPU alta.

**Diagnóstico.** Com o usuário administrativo:

```sql
-- Quem bloqueia quem
SELECT r.session_id, r.blocking_session_id, r.wait_type, r.wait_time, r.status,
       SUBSTRING(t.text, 1, 200) AS comando
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0
   OR r.session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests);

-- Consultas que mais leem por execução
SELECT TOP (20)
       qs.total_logical_reads / qs.execution_count AS leituras_por_execucao,
       qs.execution_count,
       SUBSTRING(t.text, 1, 200) AS comando
FROM sys.dm_exec_query_stats AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS t
ORDER BY leituras_por_execucao DESC;
```

Consultas por conta no topo da segunda lista indicam o defeito do tipo do parâmetro
([modelo de dados](../software/05-modelo-de-dados.md#43-o-defeito-de-tipo-do-parâmetro)).

**Ação.** No curto prazo: aumentar a instância, reduzir o máximo de instâncias da WebApi para
aliviar conexões, encerrar a sessão que bloqueia se for claramente anômala. A correção é a
da F0: tipo do parâmetro, `READ_COMMITTED_SNAPSHOT`, índice.

**Verificação.** Latência de volta à meta; nenhuma espera longa por trava.

### RB-07 — Ataque ao login ou reuso de token

**Sintoma.** Pico de logins recusados; bloqueios do Cloud Armor; log de token reutilizado.

**Diagnóstico.** Logs do Cloud Armor por IP; distribuição dos recusados no tempo; para reuso,
quantos usuários e se o padrão é de duas abas (o mesmo navegador) ou de origens diferentes.

**Ação.**

- Ataque: endurecer a regra do Cloud Armor (bloquear faixas, baixar o limite).
- Reuso isolado: as sessões do usuário já foram revogadas pelo sistema; se repetir, falar com
  o usuário.
- Suspeita de senha descoberta: revogar as renovações do usuário e exigir redefinição.
- Dado pessoal exposto: tratar como incidente de segurança, com o prazo de comunicação da
  [LGPD](06-seguranca-e-threat-model.md#75-incidentes).

**Verificação.** Taxas de volta ao normal.

**Prevenção.** Bloqueio progressivo por conta, MFA, sessão por cookie.

### RB-08 — Rotação da chave do JWT

1. Gerar a chave nova: `openssl rand -base64 48`.
2. Gravar a versão nova: `gcloud secrets versions add lancamentos-jwt-chave --data-file=-`.
3. Publicar novas revisões da WebApi e do BFF ao mesmo tempo.
4. Os JWT antigos deixam de valer; o front renova no primeiro 401 e segue. As sessões não
   caem: o token de renovação não depende da chave.
5. Desabilitar a versão antiga do segredo.

**Verificação.** Logins e renovações normais; um pico curto de 401 é esperado.

### RB-09 — Restaurar o banco e reconstruir o saldo

**Quando.** Erro lógico: defeito que gravou dado errado, comando manual equivocado.

1. Escolher o instante anterior ao erro.
2. Clonar a instância nesse instante:

   ```bash
   gcloud sql instances clone lancamentos-sql lancamentos-sql-restaurado \
     --point-in-time '2026-10-07T14:00:00Z'
   ```

3. Rodar R1 a R6 no clone.
4. Decidir: apontar as aplicações para o clone (trocar o segredo da connection string e
   publicar revisões), aceitando perder o que foi gravado depois do instante, ou copiar do
   clone para o original só as linhas afetadas.
5. Reconstruir o saldo com o Consumer parado e rodar a reconciliação.
6. Se dados de usuários foram afetados, avaliar a comunicação (LGPD).

### RB-10 — Ambiente local: consolidação parada depois de reiniciar o emulador

O emulador do Pub/Sub guarda tudo em memória: reiniciar o container apaga tópico e
subscription.

```bash
docker compose restart events consumer   # recriam tópico e subscription
```

O que ficou em `Enfileirado` antes do reinício segue o RB-05. Para simular queda do broker
sem perder o estado: `docker compose pause pubsub` e `unpause`.
