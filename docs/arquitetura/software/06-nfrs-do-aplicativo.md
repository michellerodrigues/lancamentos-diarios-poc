# NFRs do aplicativo

Os atributos de qualidade que dependem do código: desempenho de cada operação,
concorrência, resiliência, cache, timeouts e retentativas. Retrato do commit `45bcd17`, em
07/10/2026.

Os requisitos não funcionais da solução (SLA, disponibilidade, DR, capacidade, custo) estão
em [NFRs de solução](../solucao/08-nfrs-de-solucao.md). As medições citadas aqui vêm de
[requisitos-nao-funcionais.md](../../requisitos-nao-funcionais.md) e de
[capacidade-do-relay.md](../../capacidade-do-relay.md), feitas com a stack no `docker compose`
de um notebook. Cada afirmação diz se foi **medida**, se vem do **código** ou se é
**inferida**.

## 1. Resumo

| Id | Requisito do aplicativo | Meta | Hoje | Situação |
|---|---|---|---|---|
| NFR-APP-01 | Registrar um lançamento rápido, com o broker no ar ou não | p95 ≤ 150 ms no local; nunca mais que 2 s esperando o broker | mediana de 56 a 74 ms e máximo de 102 ms em amostras de 5 a 20 lançamentos; 2,05 s no pior caso com o broker pausado (medido) | Atendido na amostra medida; falta carga |
| NFR-APP-02 | Consultar o saldo barato e previsível | busca por índice, sem varrer o histórico | p95 de 28 ms com 28 linhas; o plano varre as tabelas (medido) | Não atendido |
| NFR-APP-03 | Inserções de contas diferentes não se bloqueiam | trava por conta | trava da tabela inteira (medido) | Não atendido |
| NFR-APP-04 | Leitura não espera escrita | `READ_COMMITTED_SNAPSHOT` | desligado (código) | Não atendido |
| NFR-APP-05 | Nenhum efeito duplicado sob reentrega | transição condicional | atendido (código e testes) | Atendido |
| NFR-APP-06 | Lançar com a consolidação fora do ar | 201 sem depender de relay, broker e Consumer | atendido com Consumer e relay parados e com o broker pausado (medido) | Atendido |
| NFR-APP-07 | Recuperar sozinho de falha passageira | sem intervenção para quedas de segundos | até 5 tentativas com espera crescente; depois, `Erro` terminal (código) | Parcial |
| NFR-APP-08 | Timeouts em cascata coerentes | quem chama espera mais que quem é chamado | BFF 10 s, comando SQL 30 s (código) | Não atendido |
| NFR-APP-09 | Repetir é seguro | chave de idempotência no POST | não há (código) | Não atendido |
| NFR-APP-10 | Dado pessoal fora de cache intermediário | `Cache-Control: no-store` em `/api/*` | sem cabeçalho (código) | Parcial |
| NFR-APP-11 | Configuração errada falha na subida | `ValidateOnStart` em todas as opções | atendido (código) | Atendido |
| NFR-APP-12 | Desligar sem perder trabalho | drenar o que está em andamento | Consumer espera 10 s; publicador, 5 s (código) | Parcial |

## 2. Desempenho

### 2.1 O que foi medido

| Operação | Resultado | Fonte |
|---|---|---|
| `POST /lancamentos`, tudo no ar | mediana de 74 ms | RNF-01 |
| `POST /lancamentos` com Consumer e relay parados | mediana de 56 ms, máximo de 102 ms | RNF-01 |
| `POST /lancamentos` com o Pub/Sub pausado | o primeiro em 2,05 s (o teto); os seguintes em ~23 ms, porque caem para o relay | RNF-01 |
| `GET /api/saldo` a 50, 100 e 200 req/s | p50 de 7,5 a 8,8 ms; p95 de ~28 ms; p99 de ~32 ms; 0% de perda | RNF-01, base de 28 linhas |
| Publicar uma mensagem e esperar | 17,4 ms | capacidade do relay |
| Marcar `Enfileirado`, linha a linha | 11,7 ms | capacidade do relay |
| Rodada do relay com 50 contas | 2,0 s | capacidade do relay |
| Consolidar um lançamento (dois commits) | 26,5 ms; ~38 por segundo numa conta | capacidade do relay |
| Subir, rodar e encerrar o relay em modo Job | 1,9 a 3,9 s | capacidade do relay |

O local tem o banco no disco do Docker Desktop, onde cada commit custa ~11 ms. Num banco em
SSD gerenciado, o commit tende a ficar mais barato; a publicação, com rede de verdade, tende
a ficar mais cara. Nenhum número foi medido no GCP.

### 2.2 Orçamento de uma requisição

`POST /api/lancamentos`, no caminho feliz, faz:

| Passo | Idas e voltas | Onde |
|---|---|---|
| Front → BFF → WebApi | 2 HTTP | — |
| Validação do JWT, duas vezes | 0 (só CPU) | BFF e WebApi |
| `BEGIN`, `MAX + 1`, `INSERT`, `COMMIT` | 4 SQL | `InserirAsync` |
| Existe anterior pendente? | 1 SQL | `SemPredecessorPendenteAsync` |
| Reserva | 1 SQL | `ReservarUmAsync` |
| Publicação | 1 gRPC, com a espera de agrupamento do cliente | `PubSubPublicador` |
| Marca `Enfileirado` | 1 SQL | `MarcarEnfileiradoAsync` |

São 7 idas ao banco e uma ao broker. As três últimas só existem para tirar a espera do
caminho feliz; sem elas o lançamento sairia em 4 idas ao banco e esperaria o relay.

`GET /api/saldo/{conta}` faz uma ida ao banco com duas consultas (`QueryMultiple`) e outra
para a lista de pendentes. Com o defeito de tipo corrigido e o índice
`(ContaId, Stage)`, as três viram buscas por índice
([recomendações do modelo de dados](05-modelo-de-dados.md#6-recomendações)).

### 2.3 Gargalos conhecidos

1. **Parâmetro `nvarchar` contra coluna `varchar(20)`**: varredura e trava de tabela
   inteira. É o que impede extrapolar os números de leitura para volume real
   ([detalhe](05-modelo-de-dados.md#43-o-defeito-de-tipo-do-parâmetro)).
2. **Relay com um lançamento por conta por ciclo**: uma conta com 60 pendentes leva 60
   ciclos, uma hora no GCP. O relay em lote, medido na bancada, publica dezenas de milhares
   por rodada.
3. **Consumer em série dentro da conta**: ~38 por segundo por conta, no local. Para uma conta
   por comerciante, não é limite.

## 3. Concorrência

| Recurso | Configuração | Onde | Efeito |
|---|---|---|---|
| Pool de conexões SQL | 100 por processo (padrão do `SqlClient`) | connection string | Acima disso, a requisição espera até o timeout de conexão, 15 s por padrão |
| Consumer | 20 mensagens em andamento, somando as contas | `Consumer:Concorrencia` | Até 20 contas em paralelo; uma mensagem por vez dentro de cada conta |
| Relay | uma instância; 50 lançamentos por ciclo; um por conta | `Relay:TamanhoLote`, `--tasks 1` | Previsível; limita a recuperação ([capacidade](../../capacidade-do-relay.md)) |
| Publicador | singleton; o cliente é criado uma vez, protegido por `SemaphoreSlim` | `PubSubPublicador` | Seguro entre threads; mantém a ordem de chamada por chave |
| Repositórios, conversores | singletons sem estado | DI | — |
| Handlers, services do BFF | scoped | DI | Um por requisição |
| Sequência por conta | `UPDLOCK, HOLDLOCK` | `InserirAsync` | Inserções da mesma conta em fila, de propósito |
| Lote | uma transação, até 1.000 itens, contas travadas em ordem | `InserirLoteAsync` | Segura as contas do lote até o commit |

**Regra de ouro do fluxo:** dentro de uma conta, tudo é serial (sequência, publicação,
consolidação); entre contas, tudo é paralelo. Qualquer mudança que serialize contas entre si
é regressão. O defeito de tipo é exatamente isso.

## 4. Resiliência

### 4.1 Falhas e comportamento

| Falha | Comportamento | Mecanismo | Lacuna |
|---|---|---|---|
| Broker fora na publicação imediata | 201 com `Cadastrado`; o relay recolhe | `TentarPublicarAsync` nunca lança | A falha passageira conta tentativa |
| Broker lento | 201 depois de 2 s; a publicação segue em segundo plano | `Task.WhenAny` com o teto | No Cloud Run com CPU só durante a requisição, a continuação pode congelar até a próxima requisição; a reserva vence em 1 min e o relay recolhe (inferido) |
| Chave do Pub/Sub em estado de erro | `ResumePublish` no `catch` | `PubSubPublicador` | — |
| Processo cai com a linha em `Lido` | A reserva vence em 1 min e volta a ser candidata | `Relay:ReservaExpiraEm` | — |
| Broker fora por mais de ~30 s (local) ou ~4 min (GCP) | O primeiro lançamento de cada conta vai para `Erro`; a conta para de publicar | `DevolverParaFilaAsync` | `Erro` terminal, sem ferramenta |
| Falha ao consolidar | Nack e reentrega imediata; na 5ª, `Erro` | `DevolverParaProcessamentoAsync` | Sem espera entre tentativas; sem *dead-letter* |
| Exceção ao reservar no Consumer (banco fora) | Sobe sem tratamento ao `SubscriberClient` | `ConsolidadorDeMensagem` só protege a consolidação | Capturar e responder `Devolver` |
| Consumer cai entre os dois commits | Linha presa em `EmProcessamento`; a reentrega é descartada como repetida | — | Lançamento fora do saldo, para sempre |
| Mensagem some depois de publicada | Linha presa em `Enfileirado` | — | Nada republica |
| Colisão de sequência | Recalcula e tenta de novo, até 5 vezes | `InserirAsync` | — |
| Banco fora | WebApi devolve 500; relay registra e tenta no próximo ciclo, ou sai com código 1 no Job | — | — |
| WebApi fora ou lenta | BFF devolve 500 depois de até 10 s | `HttpClient.Timeout` | Deveria ser 502 ou 504 |
| JWT vencido | 401; o front renova uma vez e repete | `autenticacaoInterceptor` | Duas abas disparam o alarme de reuso |

### 4.2 Retentativas

| Onde | Quantas | Espera | Por que é seguro repetir |
|---|---|---|---|
| Front, no 401 | uma renovação e uma repetição | — | O pedido original foi recusado antes de executar |
| BFF → WebApi | nenhuma | — | — |
| Sequência no `INSERT` | até 5 | imediata | A transação anterior foi desfeita |
| Publicação (WebApi e relay) | 5 por lançamento | 2, 4, 8, 16 s, teto de 60 s | A reserva impede publicação concorrente; o Consumer descarta repetição |
| Cliente do Pub/Sub | a retentativa interna da biblioteca para erros transitórios de gRPC | padrão da biblioteca | — |
| Consumer | 5 por lançamento | reentrega imediata | Transição condicional |
| Cloud Run Job | até 3 execuções | — | O ciclo é idempotente |

**Falta a chave de idempotência no `POST /lancamentos`.** Se o BFF desistir em 10 s e a
WebApi fizer o commit depois, o usuário vê erro, repete e duplica (plausível, não medido).
Proposta: o front gera um `Idempotency-Key` por envio; a WebApi grava a chave com o
lançamento, num índice único `(ContaId, ChaveIdempotencia)`, e devolve o lançamento já
gravado quando a chave se repete.

### 4.3 Timeouts

| Timeout | Valor | Onde |
|---|---|---|
| Espera pela publicação imediata | 2 s | `CriarLancamentoHandler.LimitePublicacao` |
| BFF → WebApi | 10 s | `WebApi:Timeout` |
| Comando SQL | 30 s | `Banco:TimeoutComandoSegundos` |
| Abrir conexão SQL | 15 s | padrão do `SqlClient` |
| Tolerância de relógio do JWT | 30 s | `ClockSkew` |
| Prazo de ack da subscription | 60 s, estendido pela biblioteca enquanto processa | `PubSubProvisionador` |
| Desligamento do Consumer | 10 s | `ConsolidacaoWorker` |
| Desligamento do publicador | 5 s | `PubSubPublicador.DisposeAsync` |
| Execução do Job | 50 s | `deploy/events-cloud-run-job.sh` |
| Reserva | 1 min | `Relay:ReservaExpiraEm` |
| Requisições do front | sem timeout próprio | `HttpClient` do Angular |

**A cascata está invertida.** Quem chama deveria esperar mais do que quem é chamado. Hoje o
BFF desiste em 10 s, e um comando SQL da WebApi pode levar 30 s. Proposta:

| Camada | Hoje | Proposto |
|---|---|---|
| Front | sem limite | 15 s, com mensagem de tente de novo |
| BFF → WebApi | 10 s | 10 s |
| WebApi, requisição inteira | sem limite | 8 s (`AddRequestTimeouts`) |
| Comando SQL de lançamento e saldo | 30 s | 5 s |
| Comando SQL do lote | 30 s | 15 s |

## 5. Cache

| Camada | Hoje | Observação |
|---|---|---|
| Servidor | nenhum | O read model `SaldoConsolidado` faz o papel de cache da soma; a leitura do saldo precisa ser fresca, então não há cache sobre ele |
| HTTP | nenhum cabeçalho de cache nas APIs | Sem `ETag` nem `Last-Modified`, o navegador não guarda. Um proxy ou CDN na frente poderia: falta `Cache-Control: no-store` em `/api/*` |
| Front, dados | último saldo num signal, em memória | Sobrevive a uma falha de rede com a tela aberta; não sobrevive a reabrir o app |
| Front, app | service worker guarda os arquivos do build; `index.html` e `ngsw.json` vão sem cache no nginx | Nenhum `dataGroup`: as respostas da API não são guardadas |
| Front, configuração | `GET /api/auth/config` lido uma vez por carga | — |

**Sobre guardar o saldo para uso sem rede** (RF-16): um `dataGroup` do service worker para
`/api/saldo` resolveria a abertura a frio, mas deixaria dado financeiro no cache do
aparelho, e o logout não limpa esse cache. Se for feito, guardar o último saldo no IndexedDB
pelo próprio `LancamentosService` e apagar no logout.

## 6. Limites do aplicativo

| Limite | Valor | Onde |
|---|---|---|
| Valor de um lançamento | maior que zero, até R$ 1.000.000,00, duas casas | `CriarLancamentoValidator` |
| Conta | 3 a 20 caracteres, letras, números e hífen | idem |
| Observação | 200 caracteres | idem |
| Itens por lote | 1.000 | `CriarLancamentosEmLoteValidator` |
| Pendentes na resposta do saldo | 20 por padrão, configurável por chamada | `Saldo:LimitePendentesPadrao` |
| Lançamentos por ciclo do relay | 50 | `Relay:TamanhoLote` |
| Tentativas de publicação e de consolidação | 5 cada | `Relay:MaxTentativas`, `Consumer:MaxTentativas` |
| Senha | 8 a 128, com letra e número | `RegrasDeSenha` |
| Nome; e-mail | 100; 254 | `RegistrarUsuarioValidator` |
| Mensagem de erro gravada | 1.000 caracteres | `LancamentoRepository.Truncar` |
| Corpo de requisição | 30 MB, o padrão do Kestrel | — |

Um lote de 1.000 itens com observação cheia tem bem menos de 1 MB; o limite de 30 MB do
Kestrel pode baixar para 1 MB sem afetar nenhum uso legítimo.

## 7. Como verificar

| Requisito | Verificação existente | Verificação proposta |
|---|---|---|
| NFR-APP-01, 06 | Medição manual com `docker compose stop` e `pause` | Teste de resiliência automatizado ([07 · Testes](07-testes-e-padroes-de-codigo.md#6-ponta-a-ponta-resiliência-e-carga)) |
| NFR-APP-02, 03, 04 | `tools/carga/carga_saldo.py` com base pequena | Carga mista, base de milhões de linhas, plano de execução conferido |
| NFR-APP-05 | Testes de unidade do relay e do Consumer | Testes de integração do repositório com SQL Server real |
| NFR-APP-07 | Testes de backoff | Teste com o emulador pausado por mais de 30 s |
| NFR-APP-08, 09 | — | Teste de integração com WebApi lenta e repetição do POST |
| NFR-APP-11 | Só o código (`ValidateOnStart`) | Teste que sobe o host sem cada seção e espera a falha |
