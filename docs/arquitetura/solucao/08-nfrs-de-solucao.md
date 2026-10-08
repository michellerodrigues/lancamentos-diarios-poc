# NFRs de solução

Os compromissos da solução como um todo: nível de serviço, disponibilidade, escalabilidade,
capacidade, recuperação de desastres e custo. Retrato do commit `45bcd17`, em 07/10/2026.

Nada foi medido no GCP. Os números locais vêm de
[requisitos-nao-funcionais.md](../../requisitos-nao-funcionais.md) e de
[capacidade-do-relay.md](../../capacidade-do-relay.md); as metas abaixo são propostas, e as
estimativas dizem de onde saem. Os NFRs internos do aplicativo (latência por operação,
timeouts, retentativas) estão em [NFRs do aplicativo](../software/06-nfrs-do-aplicativo.md).

## 1. Resumo

| Categoria | Meta proposta | Hoje |
|---|---|---|
| Disponibilidade do lançamento | 99,9% em 28 dias, independente da consolidação | Independência medida no local; nenhuma medição de disponibilidade |
| Disponibilidade da consulta do saldo | 99,5% em 28 dias; no pico de 50 req/s, até 5% de perda (o requisito) | 0% de perda a 50, 100 e 200 req/s numa base de 28 linhas |
| Atualidade do saldo | 99% dos lançamentos consolidados em até 30 s | Segundos no caminho feliz (medido); até ~1 min na recuperação (inferido) |
| Escalabilidade | Contas em paralelo; dentro da conta, em série | Serializada pelo defeito de tipo do parâmetro |
| Recuperação | RPO ≤ 5 min e RTO ≤ 1 h para erro lógico; RPO 0 para queda de zona | Nada configurado |
| Custo | Orçamento por ambiente, com alerta | Não estimado |

## 2. SLA e SLO

A POC não tem SLA contratado. Abaixo, o que a plataforma oferece e os SLOs internos
propostos.

### 2.1 O teto que a plataforma permite

SLAs mensais publicados pelo Google para os serviços do caminho síncrono (conferir os
vigentes e as condições, como a alta disponibilidade configurada no Cloud SQL):

| Serviço | SLA |
|---|---|
| Cloud Load Balancing | 99,99% |
| Cloud Run (BFF e WebApi) | 99,95% cada |
| Cloud SQL com alta disponibilidade | 99,95% |
| Pub/Sub | 99,95% |

Lançar e consultar dependem, em série, do Load Balancer, do BFF, da WebApi e do banco:
0,9999 × 0,9995 × 0,9995 × 0,9995 ≈ **99,84%**, cerca de 70 minutos por mês de
indisponibilidade possível só pela plataforma. Um SLO de 99,9% para o lançamento fica acima
desse produto: é atingível porque as falhas não se somam na prática, mas não é garantido
pelos contratos. O Pub/Sub não entra nessa conta, porque lançar não depende dele.

### 2.2 SLOs propostos

| SLO | Indicador | Meta | Janela | Fonte |
|---|---|---|---|---|
| Lançar disponível | Proporção de `POST /api/lancamentos` sem 5xx nem timeout | 99,9% | 28 dias | Logs do Load Balancer |
| Lançar rápido | p95 da latência de `POST /api/lancamentos` | ≤ 500 ms | 28 dias | Load Balancer |
| Consultar disponível | Proporção de `GET /api/saldo/*` sem 5xx nem timeout | 99,5% | 28 dias | Load Balancer |
| Consultar rápido | p95 da latência de `GET /api/saldo/*` | ≤ 300 ms | 28 dias | Load Balancer |
| Saldo em dia | Proporção de lançamentos com `Consolidado` em até 30 s do registro | 99% | 28 dias | Métrica de atraso da consolidação |
| Saldo correto | Contas com divergência na reconciliação | 0 | diária | [Reconciliação R1](05-dados-macro-e-migracao.md#6-reconciliação) |

Com 99,9% em 28 dias, o orçamento de erro é de cerca de 40 minutos. Alertas por consumo
desse orçamento estão no [plano de observabilidade](10-observabilidade-e-operacao.md#6-slos-e-alertas).

**Relação com o requisito do desafio.** "50 requisições por segundo com no máximo 5% de
perda" é um piso de 95% na consulta durante o pico. O SLO de 99,5% é mais exigente; o piso
do desafio vira um teste de carga de aceitação.

## 3. Disponibilidade

| Fluxo | Depende de | Não depende de |
|---|---|---|
| Lançar | Load Balancer, BFF, WebApi, banco | Pub/Sub, relay, Consumer |
| Consultar | Load Balancer, BFF, WebApi, banco | Pub/Sub, relay, Consumer |
| Consolidar | Pub/Sub ou relay, Consumer, banco | Front, BFF |
| Entrar | Load Balancer, BFF, WebApi, banco; Google, no login com Google | Pub/Sub, relay, Consumer |
| Redefinir senha | Os mesmos do login e o provedor de e-mail | — |

| Falha | Efeito | Recuperação |
|---|---|---|
| Uma instância cai | Nenhum para o usuário; o Cloud Run sobe outra | Automática |
| Uma zona cai | Cloud Run e Pub/Sub são regionais; o Cloud SQL com HA troca para a zona de reserva | Automática; o banco fica indisponível durante a troca, da ordem de um minuto |
| Pub/Sub fora | Lança e consulta normalmente; a consolidação para | Volta sozinha; acima de ~4 min, contas vão para `Erro` (terminal hoje) |
| Consumer fora | Mensagens esperam na subscription (até 7 dias) | Volta sozinha |
| Banco fora | Lançar e consultar param | Failover, ou restauração ([6](#6-recuperação-de-desastres)) |
| A região cai | Tudo para | Recuperação de desastre |

**A consulta divide processo, pool e banco com o lançamento** (RNF-01): se a leitura
saturar, o lançamento degrada junto. Isolar a leitura (pool e connection string próprios,
limite de concorrência, depois réplica) é o que protege o requisito principal.

## 4. Escalabilidade

| Componente | Como escala | Limite conhecido | O que fazer |
|---|---|---|---|
| Front | Instâncias do Cloud Run; conteúdo estático | — | Cloud CDN, se necessário |
| BFF | Horizontal, sem estado | Timeout de 10 s para a WebApi | Máximo de instâncias |
| WebApi | Horizontal, sem estado | O banco: pool, travas, conversão de tipo | Corrigir o tipo; `READ_COMMITTED_SNAPSHOT`; limitar o pool |
| Relay | Uma execução por minuto | 50 lançamentos por rodada, um por conta | Relay em lote: dezenas de milhares por rodada (medido na bancada) |
| Pub/Sub | Gerenciado | 1 MB/s por ordering key, cerca de 2.700 mensagens de 367 bytes por segundo numa conta | Nenhuma conta chega perto |
| Consumer | Instâncias × 20 em paralelo; em série dentro da conta | ~38 consolidações por segundo numa conta (medido no local) | Mais instâncias ou mais concorrência |
| Banco | Vertical; réplica de leitura conforme a edição | Uma instância de escrita | Índices, isolamento, réplica para leitura |

**O ponto de atenção é a recuperação.** Depois de uma queda do broker, os lançamentos de cada
conta com pendente seguem pelo relay, que hoje publica um por conta por minuto. Uma hora de
broker fora no cenário médio abaixo acumula ~150 mil lançamentos; a 50 por minuto, a fila
levaria mais de dois dias para esvaziar. Com o relay em lote, uma ou duas rodadas.

## 5. Capacidade

Estimativas a partir de premissas, para dimensionar e para o teste de carga. Premissas:
30 lançamentos por conta por dia, concentrados em 10 horas, com pico de 5 vezes a média;
~300 bytes por lançamento no banco, com índices ([modelo de dados](../software/05-modelo-de-dados.md#5-volume-e-crescimento)).

| | Piloto | Médio | Grande |
|---|---|---|---|
| Contas | 100 | 10 mil | 100 mil |
| Lançamentos por dia | 3 mil | 300 mil | 3 milhões |
| Pico de lançamentos | ~0,4 por segundo | ~42 por segundo | ~420 por segundo |
| Pico de consultas de saldo | poucas por segundo | ~50 por segundo (o requisito) | ~500 por segundo |
| Banco por ano | ~0,3 GB | ~33 GB | ~330 GB |
| Mensagens por mês | ~90 mil | ~9 milhões | ~90 milhões |
| Fila de uma hora de broker fora | ~1,5 mil | ~150 mil | ~1,5 milhão |
| BFF e WebApi | 1 instância cada | 1 a 3 | 3 a 10 |
| Consumer | 1 instância | 1 | 2 ou mais |
| Cloud SQL | 2 vCPU; a edição Express pode bastar | 2 a 4 vCPU, HA | 8 vCPU, HA, réplica de leitura |
| Relay | O atual atende | **Relay em lote obrigatório** | Relay em lote |

Antes de adotar qualquer linha desta tabela: corrigir o tipo do parâmetro, ligar o
`READ_COMMITTED_SNAPSHOT` e medir no GCP com volume, carga mista e o perfil do cenário.

## 6. Recuperação de desastres

| Cenário | RPO | RTO | Mecanismo |
|---|---|---|---|
| Processo ou instância | 0 | segundos | Cloud Run sobe outra; o outbox e o Pub/Sub não perdem nada |
| Zona | 0 | minutos | Cloud SQL com HA regional, réplica síncrona na outra zona; Cloud Run em várias zonas |
| Erro lógico (bug, operação errada) | ≤ 5 min | ≤ 1 h | Recuperação pontual (PITR) para uma instância nova; reconciliação; saldo reconstruído a partir dos lançamentos |
| Mensagens perdidas no Pub/Sub | 0 | minutos | A linha é a fonte: republicar o que ficou em `Enfileirado` ([R5](05-dados-macro-e-migracao.md#6-reconciliação)) |
| Região | ver abaixo | ver abaixo | Backups ou réplica fora da região; Terraform recria o resto |

**A perda da região esbarra na LGPD.** Hoje o GCP tem uma região no Brasil. Guardar backup
ou réplica fora de `southamerica-east1` é transferência internacional de dados pessoais (art.
33): precisa de base legal e de decisão com o jurídico. As opções:

| Opção | RPO | RTO | Custo | LGPD |
|---|---|---|---|---|
| Só backups na região | — | indefinido se a região não voltar | menor | dados no país |
| Backups em outra região | ≤ 24 h, ou menos com exportação mais frequente | ≤ 4 h: recriar com Terraform e restaurar | baixo | transferência internacional |
| Réplica entre regiões | minutos | ≤ 1 h: promover a réplica | uma segunda instância | transferência internacional; requisitos de edição do SQL Server |

**Procedimentos e ensaios**

| Ensaio | Frequência | Como |
|---|---|---|
| Troca de zona do banco | semestral, em homologação | `gcloud sql instances failover` e medir o tempo sem lançar |
| Recuperação pontual | semestral | Restaurar um instante em instância nova, rodar a reconciliação, comparar |
| Reconstrução do saldo | a cada mudança no Consumer | [Script de reconstrução](05-dados-macro-e-migracao.md#55-reconstrução-do-read-model) em homologação |
| Ambiente do zero | anual | `terraform apply` num projeto vazio e deploy das imagens |

## 7. Custo

Sem valores: a POC não estimou na calculadora do GCP. O modelo abaixo diz o que pesa e o que
fazer para não pesar. Para estimar, use a calculadora com os cenários da [seção 5](#5-capacidade)
e a região `southamerica-east1`.

| Item | Fixo ou variável | Como cresce | Peso esperado |
|---|---|---|---|
| Cloud SQL for SQL Server: instância e licença | fixo | vCPU e memória; a HA mantém uma instância de reserva | **o maior** |
| Instâncias mínimas: BFF, WebApi, Consumer | fixo | número de instâncias | alto |
| Load Balancer e Cloud Armor | fixo e por requisição | regras e requisições | médio |
| Cloud Run por uso | variável | vCPU-segundo, memória e requisições | baixo a médio |
| Job do relay | variável | 1.440 execuções por dia, de poucos segundos cada | baixo; conferir se há cobrança mínima por execução |
| Logs | variável | volume ingerido acima da cota gratuita | baixo, se os níveis forem controlados |
| Pub/Sub | variável | volume; a cobrança tem mínimo por requisição | desprezível até milhões de mensagens |
| Cloud Scheduler, Secret Manager, Artifact Registry | quase fixo | jobs, acessos, GB guardado | desprezível |

| Alavanca | Economia | Preço |
|---|---|---|
| Edição Express do SQL Server no piloto | Sem licença | 10 GB por banco e limites de recursos |
| Banco zonal, sem HA, em dev e homologação | Metade da instância | Sem failover nesses ambientes |
| Instâncias mínimas zero em dev e homologação | Sem custo ocioso | Partida a frio |
| PostgreSQL no lugar do SQL Server | Sem licença | Reescrever o SQL ([ADR-SOL-02](03-adrs/ADR-SOL-02-sql-server-como-motor-unico.md)) |
| Filtros de exclusão de log | Menos ingestão | Menos detalhe em investigação |
| Consumer por push | Escala a zero | Mudança de transporte ([ADR-SOL-09](03-adrs/ADR-SOL-09-consumer-em-worker-pool.md)) |

Controles: orçamento com alerta em 50%, 90% e 100% por projeto, e rótulos de custo por
componente (`componente=webapi`, `ambiente=prd`).

## 8. Outros atributos

| Atributo | Onde |
|---|---|
| Segurança e privacidade | [Segurança, compliance e threat model](06-seguranca-e-threat-model.md) |
| Operabilidade | [Observabilidade e operação](10-observabilidade-e-operacao.md) |
| Manutenibilidade | [Testes e padrões de código](../software/07-testes-e-padroes-de-codigo.md) |
| Portabilidade | Mesma imagem em todos os ambientes; mapeamento para a AWS no [README](../../../README.md#vindo-da-aws) |
| Usabilidade | RNF-35 a 38 em [requisitos-nao-funcionais.md](../../requisitos-nao-funcionais.md#usabilidade) |
