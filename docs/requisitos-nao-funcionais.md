# Requisitos não funcionais

O que a POC garante além das funções: disponibilidade, desempenho, consistência,
segurança, observabilidade e operação. Levantado no commit `342dcc9`. As medições foram
feitas em 06/10/2026, com a stack inteira no `docker compose` de um notebook e o gerador de
carga na mesma máquina. Quando importa, o texto diz se a afirmação foi **medida**, se vem
do **código** ou se é **inferida**.

Os requisitos funcionais estão em [requisitos-funcionais.md](requisitos-funcionais.md), e
a cobertura dos testes em [cobertura-de-testes.md](cobertura-de-testes.md).

## Resumo

**O requisito do desafio ([RNF-01](#rnf-01--o-requisito-do-desafio)) é atendido em parte.**

- **Parte 1:** o lançamento continua no ar quando a esteira de consolidação cai. Isso foi
  medido com o Consumer e o relay parados e com o Pub/Sub pausado por ~20 s. A consulta do
  saldo consolidado, porém, roda no mesmo processo, no mesmo pool de conexões e no mesmo
  banco do lançamento.
- **Parte 2:** a leitura passou com 0% de perda a 50, 100 e 200 requisições por segundo.
  Mas foi numa base de 28 linhas, e um defeito de tipo de parâmetro faz cada consulta
  varrer as tabelas inteiras. Com volume real, a estimativa é que o número não se sustente.
- **As correções mais baratas são pequenas:** tipar a conta como `varchar(20)` nas
  consultas por conta, uma mudança de código em poucos pontos, e ligar o
  `READ_COMMITTED_SNAPSHOT`, que é configuração do banco. Depois delas, é preciso medir de
  novo com volume e carga mista.

| Categoria | Atendido | Parcial | Não atendido |
|---|---|---|---|
| Disponibilidade e resiliência | — | RNF-01, 02, 03, 04, 05 | — |
| Desempenho e escalabilidade | — | RNF-06, 07, 09 | RNF-08 |
| Consistência e entrega | RNF-11, 12, 14 | RNF-10, 13 | RNF-15 |
| Segurança | RNF-16, 17, 19, 20, 21 | RNF-18, 22, 23 | RNF-24 |
| Observabilidade | RNF-27 | RNF-25, 26 | RNF-28 |
| Implantação | RNF-30 | RNF-29 | RNF-31 |
| Manutenção e testes | RNF-32, 33 | RNF-34 | — |
| Usabilidade | — | RNF-35, 36, 37, 38 | — |

O RNF-17 vale na API: no front, duas abas abertas disparam por engano o alarme de token
reutilizado. O RNF-30 vale no código e no script de deploy, sem execução no GCP.

## RNF-01 — O requisito do desafio

> O serviço de controle de lançamento não deve ficar indisponível se o sistema de
> consolidado diário cair. Em dias de picos, o serviço de consolidado diário recebe 50
> requisições por segundo, com no máximo 5% de perda de requisições.

### Como foi lido

- **"Serviço de controle de lançamento"** é o caminho de escrita: `POST /api/lancamentos`
  no BFF, que chega ao `POST /lancamentos` da WebApi.
- **"Sistema de consolidado diário"** tem duas metades:
  - a esteira assíncrona: relay (Events), Pub/Sub e Consumer, que monta a tabela
    `SaldoConsolidado`;
  - a consulta: `GET /saldo/{conta}`, servida pela **própria WebApi**.
- **Atenção:** o "consolidado diário" aqui é o saldo corrente de cada conta, sem corte por
  dia (ver RF-08 em [requisitos-funcionais.md](requisitos-funcionais.md)).
- **"50 requisições por segundo com até 5% de perda"** foi lido como 50 consultas por
  segundo ao saldo. O texto fala de requisições que o serviço recebe, e tolerar perda só
  faz sentido para consulta, que o cliente repete. Perder 5% dos lançamentos a consolidar
  corromperia o saldo. Essa outra leitura, de 50 lançamentos por segundo a consolidar,
  também foi avaliada, com perda zero como meta.
- **Perda** é toda resposta fora de 2xx, timeout ou erro de conexão.

### Parte 1 — o lançamento sem a consolidação

**Como funciona.** O POST grava o lançamento e só depois tenta publicar no Pub/Sub, com
teto de 2 s. Responde 201 qualquer que seja o resultado da publicação, porque a própria
linha é o outbox: se a publicação falha, o relay recolhe depois. Se a gravação falha, a
resposta é 500. O POST não chama o Consumer nem o processo Events, e não lê nem grava o
`SaldoConsolidado`. A publicação imediata usa o código do relay, rodando dentro da WebApi
(`CriarLancamentoHandler.cs:53-143`).

**Medido** numa conta de teste criada para isso. A aplicação não apaga lançamentos, então
a conta foi removida depois, direto no banco:

| Cenário | Lançamentos | Resposta | Depois |
|---|---|---|---|
| Tudo no ar | 5 | 201, mediana de 74 ms | consolidou em 1 s |
| Consumer e Events parados (`docker compose stop`) | 20 | 201, mediana de 56 ms, máximo de 102 ms | consolidou 3,1 s depois da volta |
| Pub/Sub pausado por ~20 s (`docker compose pause`) | 5 | 201; o primeiro em 2,05 s (o teto), os outros em ~23 ms | consolidou 12 s depois da volta |

Com o Consumer e o Events parados, a consulta do saldo atendeu 50 requisições por segundo
por 20 s, com 0% de perda.

**Modos de falha:**

| O que cai | O que acontece com o lançamento | Base |
|---|---|---|
| Consumer | Continua: 201, e as mensagens esperam na fila | medido |
| Relay (Events) | O avulso continua: 201, publicado pela própria WebApi. O lote espera o relay voltar | medido (avulso) |
| Pub/Sub por segundos | Continua: o primeiro lançamento de cada conta espera até 2 s | medido |
| Pub/Sub por mais de ~30 s (local) ou ~4 min (GCP) | Continua 201, mas o primeiro lançamento de cada conta esgota as 5 tentativas e vai para Erro, que é terminal. A conta para de consolidar até alguém intervir, e não há ferramenta para isso | código |
| Consumer congelado no meio de uma consolidação (pausa, GC longo, rede) | **Para**, para todas as contas. A trava do número sequencial do POST hoje cobre a tabela inteira (ver Parte 2) e espera a transação do Consumer | inferido do plano e dos locks |
| Consulta do saldo acima do previsto, ou lenta | **Degrada junto**: mesmo processo, mesmo pool de 100 conexões e mesmo banco, sem limite de concorrência separando leitura e escrita | inferido |
| Banco | Para, junto com o consolidado: o banco é um só | código |

**Veredito da parte 1.** Atende quando cai a esteira assíncrona. Não atende quando o que
cai ou satura é a consulta do consolidado, que divide processo, pool e banco com o
lançamento.

### Parte 2 — 50 consultas por segundo com até 5% de perda

**Medido** com [tools/carga/carga_saldo.py](../tools/carga/carga_saldo.py):
`GET /api/saldo/ABC1234` pelo BFF, com token de cliente, chegada em taxa constante e
timeout de 5 s.

| Carga | Requisições | Perda | p50 | p95 | p99 |
|---|---|---|---|---|---|
| 50/s por 60 s | 3.000 | 0% | 7,5 ms | 27,9 ms | 31,8 ms |
| 100/s por 30 s | 3.000 | 0% | 7,8 ms | 27,8 ms | 31,5 ms |
| 200/s por 20 s | 4.000 | 0% | 8,8 ms | 28,0 ms | 32,1 ms |

**O que a medição não prova.** A base tinha 28 linhas em 4 contas, só havia leitura, e a
carga saiu da mesma máquina, sem rede, TLS, load balancer nem Cloud SQL.

**O defeito que impede extrapolar** (confirmado no cache de planos e com `SHOWPLAN`):

- **A causa.** O Dapper envia a conta como `nvarchar(4000)`, e a coluna `ContaId` é
  `varchar(20)` (`DatabaseBootstrapper.cs:25,72`). Com a collation do banco, a conversão
  impede a busca pelo índice.
- **Na consulta.** Cada `GET /saldo` faz duas varreduras completas de `LancamentosDiarios`
  e uma de `SaldoConsolidado`. Numa base de 1 milhão de linhas, isso daria cerca de 35 mil
  leituras de página por consulta e 1,75 milhão por segundo a 50 req/s (estimativa).
- **Nas travas.** A mesma conversão atinge o `MAX + 1` do POST e a atualização do saldo pelo
  Consumer. Medindo os locks de uma leitura equivalente:

  | Trava | Com `nvarchar` | Com `varchar(20)` |
  |---|---|---|
  | Número sequencial do POST | 29 locks de faixa (a tabela inteira) | 2 |
  | Saldo consolidado | 5 locks (todas as contas) | 1 |

  A trava "por conta" vale hoje para a tabela toda: inserções e consolidações de contas
  diferentes entram numa fila só.
- **Sem `READ_COMMITTED_SNAPSHOT`,** cada consulta espera qualquer escrita em andamento,
  de qualquer conta. Neste ambiente pequeno, a consulta de saldo mais lenta registrada no
  banco levou 1,6 s, o mesmo tempo da maior espera por lock de leitura.

**A correção é barata:** passar a conta como `varchar(20)`
(`new DbString { Value = conta, IsAnsi = true, Length = 20 }`) ou trocar a coluna para
`nvarchar(20)`, e ligar o `READ_COMMITTED_SNAPSHOT`. Com o parâmetro certo, o plano já
vira busca pelo índice. Ainda falta um índice em (`ContaId`, `Stage`) para a lista de
pendentes não percorrer o histórico da conta.

**O polling multiplica a leitura.** Enquanto há pendente, cada tela aberta consulta a cada
2 s, sem recuo. Se a consolidação trava, cerca de 100 telas abertas já somam os 50 req/s.

**Na outra leitura, 50 lançamentos por segundo a consolidar:**

- **Numa conta só, não atende.**
  - A publicação imediata só acontece se nenhum lançamento anterior da conta espera
    publicação. Em rajada, a conta cai para o relay e só volta à publicação imediata
    quando a fila dela esvazia.
  - O relay publica um lançamento por conta por ciclo: 0,5 por segundo no local e 1 por
    minuto no GCP.
  - O Consumer consolida cada conta em série, a cerca de 38 por segundo, medido só no banco.
  - Nada se perde, mas o atraso cresce sem limite.
- **Em muitas contas, é plausível, mas não foi medido.** Hoje a conversão de tipo serializa
  as contas.

**Perda de lançamentos.**

- **Antes do broker, nenhum se perde enquanto a falha couber nas 5 tentativas.** A linha
  nasce como outbox e o relay a recolhe. Na 5ª falha, o lançamento vai para Erro, que é
  terminal: continua gravado e fica fora do saldo, mas a conta trava de forma visível.
- **Depois do broker, há três caminhos** em que o lançamento continua gravado, mas fica fora
  do saldo, e nada o recupera:
  1. O Consumer cai entre marcar o lançamento como em processamento e somá-lo. São dois
     commits separados, e a reentrega é tratada como repetida.
  2. A mensagem some depois de publicada: emulador reiniciado, subscription recriada ou
     retenção vencida. Nada republica o que já estava "Enfileirado".
  3. O Consumer põe o lançamento em Erro, e os seguintes da conta consolidam por cima.
     "Consolidado até o lançamento nº N" deixa de garantir que tudo antes de N entrou.

**Veredito da parte 2.** Para consultas, atende no ambiente local, mas não está comprovado
para produção enquanto a conversão de tipo existir. Para lançamentos a consolidar numa
conta só, não atende.

### O que fazer, por ordem de retorno

1. **Tipar a conta como `varchar(20)`** em todas as consultas por conta e ligar o
   `READ_COMMITTED_SNAPSHOT`. São mudanças pequenas e tiram as varreduras e as travas de
   tabela inteira.
2. **Separar a consulta do lançamento.** Uma réplica de leitura, ou ao menos um pool e uma
   connection string só para a consulta, com limite de concorrência. No GCP, um serviço
   próprio para o saldo.
3. **Tirar o Erro de terminal.**
   - Falha passageira (broker fora, timeout, lock) não deve contar como tentativa.
   - A subscription precisa de retentativa com espera e de dead-letter.
   - Falta uma forma de reprocessar e um alerta.
   - A tela precisa mostrar o Erro.
4. **Reconciliar o consolidado.**
   - Juntar a marcação de "em processamento" e a soma numa transação só.
   - Somar o lançamento N só se N−1 já entrou.
   - Uma varredura que republique o que ficou preso em "Enfileirado" ou "Em processamento".
5. **Levar o relay em lote da bancada para o produto.** Ele já foi medido em
   [capacidade-do-relay.md](capacidade-do-relay.md).
6. **Polling com recuo:** de 2 s até 60 s, parando depois de alguns minutos com um aviso de
   atraso.
7. **Chave de idempotência no POST** e timeouts coerentes. Hoje o BFF desiste em 10 s, e
   cada comando SQL da WebApi pode levar até 30 s. Se o commit acontecer antes de a
   requisição ser cancelada, o usuário vê erro, repete e duplica (plausível, não medido).
8. **Medir de novo no ambiente-alvo:** base grande, muitas contas, leitura e escrita
   juntas, e um teste de carga automatizado.

### O que não está comprovado

- Nenhum número em Cloud Run, Cloud SQL, load balancer ou Pub/Sub real.
- Base grande, muitas contas e carga mista de leitura e escrita.
- O lote (`POST /lancamentos/lote`) sob falha.
- Broker parado (conexão recusada) ou fora por minutos, e o caminho até o Erro.
- Consumer congelado no meio de uma consolidação, lock longo no saldo e esgotamento do
  pool: só inferidos do código e dos planos.
- O efeito das correções propostas.

### Como reproduzir

- **Carga:** com a stack no ar, `python tools/carga/carga_saldo.py 50 60`. Não grava
  lançamentos; só o login abre uma sessão.
- **Resiliência:**
  1. Pare a consolidação com `docker compose stop consumer events`.
  2. Lance pela tela ou pelo Swagger do BFF. Os lançamentos são aceitos e aparecem como
     pendentes.
  3. Suba de novo com `docker compose start events consumer`. Os lançamentos consolidam em
     segundos.
  4. Para o broker, use `docker compose pause pubsub` e `unpause` por menos de 30 s.

  Use uma conta de teste: a aplicação não apaga lançamentos, então ela fica no banco até um
  `-Zerar` do [script de demonstração](../scripts/inicializar-banco-demo.ps1).

## Demais requisitos

### Disponibilidade e resiliência

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-02 | Recuperar falhas de publicação | A linha volta para a fila com espera de 2, 4, 8 e 16 s, e o `ResumePublish` libera a conta no cliente do Pub/Sub. Uma reserva parada há mais de 1 min volta a valer. Na 5ª falha, vai para Erro, que é terminal | Parcial |
| RNF-03 | Recuperar falhas de consolidação | Nack e nova entrega, até 5 tentativas. A subscription não tem espera entre tentativas nem dead-letter, então uma falha passageira pode esgotar as tentativas em segundos (inferido) | Parcial |
| RNF-04 | Subir e se recuperar sozinho | `restart` e `depends_on` com healthcheck no compose, e esquema e tópico criados de forma idempotente. Mas Events e Consumer não têm healthcheck, e um Consumer com a assinatura morta continua "no ar" sem consumir | Parcial |
| RNF-05 | Resiliência entre BFF, WebApi e banco | Timeouts explícitos: 10 s no BFF, 30 s nos comandos SQL. Não há retentativa nem circuit breaker, e os dois timeouts não combinam | Parcial |

### Desempenho e escalabilidade

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-06 | Lançar rápido mesmo com o broker lento | A espera pelo broker tem teto de 2 s (medido). O INSERT antes dela não tem prazo próprio, e o `MAX + 1` trava a tabela inteira (RNF-01) | Parcial |
| RNF-07 | Janela de consolidação curta | Segundos no caminho feliz (medido). Quando a conta cai para o relay, um lançamento por ciclo: os 13 lançamentos da conta ABC1234 do script de demonstração levam ~26 s no local e ~13 min no GCP | Parcial |
| RNF-08 | Consulta do saldo barata e previsível | Read model com uma linha por conta e polling só com pendente. Mas a conversão de tipo faz a consulta varrer as tabelas inteiras | Não atendido |
| RNF-09 | Escalar na horizontal | WebApi e BFF sem estado e Consumer com concorrência de 20. Mas a conversão de tipo serializa inserções e consolidações, e o relay é instância única | Parcial |

### Consistência e entrega

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-10 | Ordem por conta | Publicação e entrega em ordem: ordering key = conta, e predecessor pendente segura o sucessor. No saldo, o lançamento N+1 pode consolidar sem o N (RNF-01, perda) | Parcial |
| RNF-11 | Sequência única e sem buracos por conta | `MAX + 1` na transação do INSERT, índice único e nova tentativa em colisão. Conferido no banco: cada conta vai de 1 até o total | Atendido |
| RNF-12 | Dinheiro exato e consolidação atômica | Centavos em `bigint`, no máximo duas casas, e marcar e somar o saldo na mesma transação | Atendido |
| RNF-13 | Todo lançamento aceito é publicado (outbox) | Vale dentro das 5 tentativas. Depois, Erro; e mensagem perdida após a publicação não volta | Parcial |
| RNF-14 | Nunca somar duas vezes | Cada transição confere o estágio esperado, e a mensagem repetida é confirmada sem reprocessar. A publicação pode sair duplicada (entrega pelo menos uma vez), e o Consumer descarta | Atendido |
| RNF-15 | Repetir a inclusão não duplica | Não há chave de idempotência. Atenuantes: a WebApi não devolve erro depois de gravar, e o botão trava durante o envio | Não atendido |

### Segurança

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-16 | Token de acesso curto e validação estrita | JWT de 15 min, emissor, audiência, assinatura e algoritmo conferidos, e chave de ao menos 32 bytes, sem a qual a WebApi e o BFF não sobem. HS256 com a chave compartilhada entre WebApi e BFF; o README aponta RS256 como próximo passo | Atendido |
| RNF-17 | Renovação de uso único com detecção de reuso | Token opaco, só o hash no banco, trocado a cada uso; reuso derruba todas as sessões. No front, duas abas disparam esse alarme por engano | Atendido (API) |
| RNF-18 | Senhas protegidas e sem revelar quem tem cadastro | PBKDF2, e o login responde igual para e-mail inexistente e senha errada, com o mesmo custo de PBKDF2 nos dois casos. Mas o cadastro responde 409 para e-mail existente, e não há limite de tentativas | Parcial |
| RNF-19 | Acesso por perfil e posse, fechado por padrão | Rota sem regra exige login, e a posse é conferida no BFF e na WebApi | Atendido |
| RNF-20 | Redefinição de senha segura | Link de uso único por 30 min, só o hash no banco e 202 para qualquer e-mail. A redefinição revoga as renovações de sessão. O tempo de resposta pode revelar quem tem cadastro, porque o e-mail só é enviado, dentro da requisição, quando o cadastro existe (inferido) | Atendido |
| RNF-21 | Login com Google seguro | Assinatura e audiência conferidas, e e-mail verificado no primeiro acesso. A vinculação automática a um cadastro com senha, pelo e-mail, é uma decisão de risco a registrar | Atendido |
| RNF-22 | Pouca superfície exposta | CORS só no BFF, Swagger desligado em produção e contêineres .NET sem root. No local, porém: sem TLS, portas abertas em todas as interfaces, nginx como root, sem cabeçalhos de segurança, e tokens no `localStorage` sem CSP | Parcial |
| RNF-23 | Segredos fora do código | Chave do JWT fora do `appsettings` base. No GCP, só a connection string do relay sai do Secret Manager, pelo script de deploy; para a WebApi e o BFF, isso está só documentado. Mas a senha do `sa` e a chave de desenvolvimento estão em texto no repositório público, e a do `sa` está no `appsettings` base que vai nas imagens | Parcial |
| RNF-24 | Limite de requisições e força bruta | Não há rate limiting nem bloqueio de tentativas | Não atendido |

### Observabilidade

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-25 | Health checks | `/health` na WebApi e no BFF, mas fixo: não testa banco nem broker. Events e Consumer não têm | Parcial |
| RNF-26 | Logs com contexto de negócio | Templates com `{ContaId}#{Sequencia}`, mas sem formato JSON e sem correlação entre BFF, WebApi, relay e Consumer | Parcial |
| RNF-27 | Rastrear um lançamento na esteira | A própria linha guarda estágio, tentativas, próxima tentativa e erro. `GET /lancamentos/{id}` expõe o estágio, as tentativas e o erro. Não registra quem lançou | Atendido |
| RNF-28 | Tracing, métricas e alertas | Nenhum | Não atendido |

### Implantação

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-29 | Mesma imagem no local e no GCP | As imagens .NET mudam só por variável de ambiente, e o Pub/Sub troca de emulador para real sozinho. O front tem o endereço da API fixo em `localhost` | Parcial |
| RNF-30 | Relay como Cloud Run Job | Ciclo único, código de saída para retentativa e script de deploy com Scheduler de 1 min. Não executado no GCP | Atendido (código) |
| RNF-31 | Deploy dos outros serviços e infraestrutura como código | Só documentado. Não há Terraform, e o Consumer não abre porta HTTP para rodar como Cloud Run Service | Não atendido |

### Manutenção e testes

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-32 | Código organizado para crescer | CQRS com dispatcher próprio, validação declarativa, handlers registrados por varredura e contratos em pacotes `POCMica.*` | Atendido |
| RNF-33 | Configuração que falha cedo | Nenhum valor padrão no código, e cada opção é validada na subida | Atendido |
| RNF-34 | Testes automatizados | 298 testes de unidade, 40% das linhas. Sem integração, front, carga nem CI. Ver [cobertura-de-testes.md](cobertura-de-testes.md) | Parcial |

### Usabilidade

| Id | Requisito | Como a POC atende | Situação |
|---|---|---|---|
| RNF-35 | Deixar claro o que já consolidou | Aviso, saldo projetado, estágio de cada pendente e "consolidado até o nº N". Faltam os totais por tipo do mockup, e a lista para em 20 sem aviso. O Erro some da tela, e o "até o nº N" pode ser falso (RNF-10) | Parcial |
| RNF-36 | Atualizar sem tráfego à toa | Sem pendente, não há tráfego. Com pendente, 2 s fixos, que continuam depois de erro e não terminam se um lançamento travar | Parcial |
| RNF-37 | Valores e datas em pt-BR, no fuso de São Paulo | Formatados no servidor, que também valida a data no fuso de São Paulo. A tela calcula a data máxima no fuso do aparelho | Parcial |
| RNF-38 | Instalar como app e usar sem rede | Manifest e service worker. O último saldo só fica em memória, lançar exige rede, e fora de `localhost` a instalação exige HTTPS | Parcial |

## Documentação que contradiz o código

- O README, na seção "Ordem dentro da conta", diz que um lançamento em Erro trava a conta.
  Isso só vale na publicação; na consolidação, os seguintes passam por cima.
- `docs/capacidade-do-relay.md:240` estima ~750 consolidações por segundo entre contas.
  Enquanto houver a conversão de tipo, a atualização do saldo serializa todas as contas.
- O exemplo de deploy do relay no README usa `CLOUDSQL_INSTANCE`, mas o script exige
  `VPC_NETWORK`, `VPC_SUBNET` e `CONNECTION_STRING_SECRET`.
- A tabela de imagens do README põe o Consumer como Cloud Run Service. O processo não abre
  a porta HTTP que o Cloud Run Service exige. O documento de arquitetura propõe um worker
  pool, e o README, na seção do `.Consumer`, a troca da subscription para push.
- O documento de arquitetura promete o último saldo sem rede. Numa abertura a frio, sem
  rede, não há saldo nenhum.
