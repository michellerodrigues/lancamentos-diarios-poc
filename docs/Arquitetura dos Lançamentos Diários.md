Lançamentos Diários - Outbox - JWT - GCP - POC

# Arquitetura dos Lançamentos Diários

Conta corrente com saldo consolidado assíncrono. O lançamento é gravado na hora e a consolidação acontece depois, por evento. A tela mostra o saldo consolidado, o que ainda está em trânsito e o saldo projetado. Cada usuário tem uma conta e só enxerga a dele; o administrador enxerga todas.

- .NET 9
- SQL Server
- Google Cloud Pub/Sub
- Angular 20 · PWA
- JWT · roles

[Fluxo](#fluxo)[Acesso](#acesso)[Estágios](#estagios)[Banco](#banco)[Ordem por conta](#ordem)[Rumo ao GCP](#gcp)[No celular](#celular)[Referências](#referencias)

Retrato do código em 02/10/2026

## O caminho de um lançamento

Do clique no Angular até o saldo consolidado. Cada caixa mostra onde a peça roda no local e no GCP.

**Local.** `docker compose up -d --build` sobe tudo em container: SQL Server, emulador do Pub/Sub, Mailpit para os e-mails, os quatro processos .NET e o front no nginx. As imagens são as mesmas que vão para o GCP; só as variáveis de ambiente mudam.

**GCP.** O Cloud Scheduler não dispara com intervalo menor que 1 minuto. Com o atalho da WebApi, o caminho feliz continua imediato e o Job só recolhe o que falhou, com até 60 s de atraso. A connection string fica no Secret Manager.

**Lote.** O Admin inclui até 1.000 lançamentos de uma vez em `POST /lancamentos/lote`, direto na WebApi. O lote grava tudo como `Cadastrado` e não usa o atalho 4a: quem publica é o relay, um lançamento por conta a cada ciclo.

O lançamento é gravado na hora e a consolidação vem depois, por evento. O atalho em verde publica direto quando a conta não tem nada pendente antes. O relay é a rede de proteção: recolhe o que ficou para trás e garante a ordem. O front só conhece o BFF, e toda chamada leva o JWT do usuário.

1. 1

   O Angular envia o lançamento ao BFF, o único serviço que ele conhece, com o JWT do usuário no header `Authorization`.
2. 2

   O BFF confere o token, a role e a conta, repassa à WebApi com o mesmo Bearer e devolve a resposta já formatada em pt-BR.
3. 3

   A WebApi confere token, role e conta de novo e grava a linha em `LancamentosDiarios` com `Stage = Cadastrado` e a próxima `Sequencia` da conta.
4. 4a

   Se nenhum lançamento anterior da conta está pendente, a WebApi reserva a linha e publica na hora. Espera no máximo 2 s pela confirmação e responde de qualquer jeito.
5. 4b

   Um gatilho dispara o ciclo do relay: localmente o `RelayWorker`, a cada 2 s; no GCP o Cloud Scheduler, a cada 60 s. O ciclo reserva até 50 linhas, em ordem de conta e sequência, e publica o que ficou para trás.
6. 5

   O Pub/Sub entrega uma mensagem por vez em cada conta, na ordem em que foram publicadas.
7. 6

   O Consumer marca `Consolidado` e soma o valor em `SaldoConsolidado` na mesma transação.
8. 7

   Enquanto houver pendente, a tela consulta o saldo a cada 2 s. Quando tudo consolida, o BFF devolve intervalo 0 e o polling para.

## Quem entra e o que pode

Só a WebApi emite token. O BFF valida o mesmo JWT e o repassa, e a WebApi valida de novo, porque é ela quem guarda o dado. Cada usuário tem exatamente uma conta.

Toda chamada passa pelas mesmas três conferências duas vezes: no BFF, para recusar cedo, e na WebApi, que guarda o dado. A role diz o que o usuário pode fazer; a posse, sobre qual conta. Um 401 faz o front renovar a sessão uma vez e repetir o pedido, e várias telas recebendo 401 juntas dividem a mesma renovação.

### Três portas, a mesma sessão

Senha, Google e cadastro terminam no mesmo par. No Google, o primeiro acesso cadastra, e um e-mail que já tem senha só é vinculado se o Google o der como verificado.

### A vida de uma renovação

O logout revoga a renovação; o JWT já emitido só vence, e por isso dura 15 minutos. Um token usado que reaparece indica cópia vazada, e não há como saber quem é o legítimo: todas as sessões do usuário caem.

| Policy | Roles | No BFF | Na WebApi |
| --- | --- | --- | --- |
| `ClienteOuAdmin` | Cliente, Admin | `/api/saldo/{conta}`, `/api/lancamentos`, `/api/auth/eu` | `/saldo/{conta}`, `/lancamentos`, `/lancamentos/{id}`, `/auth/eu` |
| `SomenteAdmin` | Admin | `/api/contas` | `/contas`, `/lancamentos/lote` |
| Pública | qualquer um | `/api/auth/*` menos `/eu`, `/health`, Swagger | `/auth/*` menos `/eu`, `/health`, Swagger |

Fechado por padrão: endpoint novo sem policy exige login, e o que é público declara `AllowAnonymous`. No Swagger, cada endpoint protegido mostra o cadeado e a policy que pede.

- `UX_Usuarios_ContaId` garante um dono por conta. A conta vai no claim `conta`, e a regra de posse compara com ela sem ir ao banco.
- A posse é um filtro de endpoint, o `PosseDaContaFilter`, igual no BFF e na WebApi: confere a conta da rota, do corpo e da resposta. O endpoint só faz o binding e chama o serviço.
- No cadastro a conta é sorteada no formato `AAA0000` e nunca reaproveita uma conta que já tenha lançamento, para ninguém herdar o extrato de outra pessoa.
- O link de "esqueci a senha" vale 30 minutos e uma vez só, e chega por SMTP; no local, no Mailpit em `localhost:8025`. A resposta é 202 exista o e-mail ou não.
- Senha com PBKDF2, pelo `PasswordHasher` do ASP.NET Core. E-mail sem cadastro gasta o mesmo tempo que senha errada, e as duas recusas têm a mesma mensagem.
- JWT HS256 com a mesma chave na WebApi e no BFF, de pelo menos 32 bytes; sem ela nada sobe. O passo seguinte é RS256 com JWKS, e o BFF deixa de guardar um segredo capaz de emitir token.
- Usuários de demonstração, criados com `Auth:SemearUsuariosDemo`: `admin@`, `cliente@` (ABC1234) e `cliente2@` (XYZ9999), todos em `lancamentos.local`, senha `Demo@2026`.

## Os estágios da linha

Não há tabela de outbox separada. A própria linha de `LancamentosDiarios` carrega o estágio, e é isso que impede publicar ou somar duas vezes.

Abaixo de cada estágio, quem o grava. Os quatro primeiros são o que a tela chama de pendente. `Erro` não se resolve sozinho e fica parado até alguém intervir. Na publicação, ele segura os lançamentos seguintes da conta.

- Todo `UPDATE` de transição traz o estágio esperado no `WHERE`. Uma reentrega do Pub/Sub encontra a linha já adiante, não altera nada e recebe ack sem somar de novo.
- `Cadastrado`, `Lido` e `Erro` bloqueiam a publicação dos sucessores da conta. Se um anterior falha no ciclo, os seguintes voltam para `Cadastrado` sem gastar tentativa.
- Uma linha presa em `Lido` por mais de 1 minuto, porque quem a reservou caiu no meio, volta a ser elegível para o relay.
- O Consumer aceita `Lido` além de `Enfileirado`. A mensagem pode chegar antes de o publicador gravar `Enfileirado`.

## Modelo do banco

Quatro tabelas no banco `Lancamentos`, SQL Server 2022 em container no local e Cloud SQL for SQL Server no GCP. Duas carregam o fluxo do lançamento; as outras duas, quem pode acessar. Não há tabela de outbox separada nem tabela de contas.

`LancamentosDiarios` é ao mesmo tempo o registro do lançamento e o outbox: o terceiro bloco de colunas é o que o fluxo usa para publicar e consolidar sem repetir. `SaldoConsolidado` é o read model da tela. As duas se ligam pela conta, sem chave estrangeira.

As tabelas de acesso são criadas pela WebApi, e não pelo esquema compartilhado: o Events e o Consumer não sabem que login existe. `TokensDeUsuario` guarda os tokens de renovação e os links de redefinição de senha, só como hash. `Usuarios` se liga às tabelas do fluxo pela conta, sem chave estrangeira.

- Dinheiro em centavos, em `bigint`. `SUM()` sobre ponto flutuante acumularia erro em cima de saldo. O valor é sempre positivo, e o sinal vem do `Tipo`.
- `Sequencia` é o `MAX + 1` da conta, calculado com `UPDLOCK, HOLDLOCK` na mesma transação do `INSERT`. O índice único é a garantia final: numa corrida, uma das inserções falha e tenta de novo.
- `ContaId` é texto de até 20 caracteres, gravado em maiúsculas pela WebApi. Ele vira a chave de ordenação do broker, e chave é comparação exata.
- Sem chave estrangeira entre as tabelas: a linha de saldo só nasce na primeira consolidação da conta, depois do primeiro lançamento.
- Token consumido não é apagado. É o `ConsumidoEm` preenchido que deixa reconhecer um token de renovação que reaparece.
- Mexendo à mão com o `sqlcmd`, passe `-I`. As tabelas de usuário têm índice filtrado, e sem `QUOTED_IDENTIFIER` ligado o SQL Server recusa `INSERT`, `UPDATE` e `DELETE` nelas.

## Ordem dentro da conta

Cada lançamento recebe uma sequência dentro da sua conta. A ordem vale por conta, e contas diferentes seguem em paralelo. O exemplo abaixo é a conta ABC1234 quando a publicação do #1 falha.

### HipóteseSe a WebApi publicasse sem olhar a conta

### Como estáO #2 espera o #1

O Pub/Sub ordena o que recebe, mas não sabe de uma mensagem que nunca chegou até ele. Por isso a trava fica no banco: a WebApi só publica quando a conta está em dia, e a reserva do relay nunca traz um lançamento com predecessor pendente. Com a trava, o #2 sai no ciclo seguinte ao do #1.

`orderingKey = "ABC1234"`

A chave é só a conta. Todas as mensagens dela dividem a chave e saem em fila. A sequência vai no corpo.

`orderingKey = "ABC1234-2"`

Com uma chave por mensagem, cada uma vira uma fila de um item só. A ordenação desliga sem aviso.

Não é preciso baixar o Consumer para uma mensagem por vez. Com a ordenação ligada, o Pub/Sub já entrega uma de cada vez por conta. `Concorrencia = 20` serve para contas diferentes andarem em paralelo.

**Data futura é recusada.** Um lançamento esperando a data ficaria pendente e travaria todos os seguintes da conta até lá. O validador barra data depois de hoje, no fuso de São Paulo, e o calendário da tela para em hoje. Um agendamento, se vier, é outra tabela e um job que cria o lançamento comum no dia.

**O lote segue a mesma trava.** Vinte itens numa conta saem em vinte ciclos do relay, porque cada um espera o anterior; contas diferentes andam em paralelo.

## Do local para o GCP

O mesmo motor nos dois lados: SQL Server em container aqui, Cloud SQL for SQL Server lá. Onde cada peça roda, o que já está decidido e o que é proposta.

Uma porta só: o Load Balancer serve o front em `/` e o BFF em `/api`, no mesmo domínio. Sem CORS, e com o HTTPS que o PWA e o login do Google exigem. A WebApi não fica exposta na internet: o BFF chega a ela pela VPC. Tracejado é proposta deste documento; o restante já estava decidido.

- **Front e BFF só pelo Load Balancer.** Os dois com ingress `internal-and-cloud-load-balancing` e serverless NEG. O front passa a chamar `/api` relativo, e o `Cors:Origens` do BFF só serve no local.
- **WebApi fechada.** Ingress `internal`. O BFF usa Direct VPC egress com `all-traffic`, numa sub-rede com Private Google Access, para a chamada contar como interna. O lote e o Swagger ficam fora da internet.
- **Consumer não abre porta HTTP.** É um Host genérico, sem Kestrel. Cloud Run Service exige um processo escutando na `PORT`; um worker pool não exige, mas é recente: confira se já está GA na região. A alternativa é a push subscription, descrita no README.
- **Só o banco passa pela VPC.** WebApi, Events e Consumer com egress `private-ranges-only`: Pub/Sub, SMTP e as chaves do Google saem direto, sem Cloud NAT. Para exigir IAM na WebApi também, o BFF mandaria o token de identidade em `X-Serverless-Authorization`, porque o `Authorization` já leva o JWT do usuário.

| Peça | Local, hoje | No GCP | Situação |
| --- | --- | --- | --- |
| Events · relay | RelayWorker em loop, a cada 2 s | Cloud Run Job disparado pelo Cloud Scheduler a cada 1 min, com `LoopContinuo = false`: roda um ciclo e encerra | Decidido |
| Banco | SQL Server 2022 em container, volume `sqlserver-dados` | Cloud SQL for SQL Server com IP privado na 1433 e Direct VPC egress. Não existe Cloud SQL Connector para .NET | Decidido |
| Pub/Sub | Emulador em memória. Tópico e subscription criados na subida | Pub/Sub gerenciado: um tópico com uma única subscription, que é a fila. `CriarRecursos = false`, e os dois vêm do Terraform, que ainda não existe no repositório | Terraform pendente |
| Consumer | Worker com pull subscription, sempre ligado | Worker pool do Cloud Run, que dispensa porta HTTP. Como Service, precisaria de um `/health`, CPU sempre alocada e `min-instances = 1`. Para escalar a zero, push subscription | Proposta |
| WebApi | Container na porta 5101 | Cloud Run Service com ingress interno e Direct VPC egress até o Cloud SQL | Proposta |
| BFF | Container na porta 5100 | Cloud Run Service atrás do Load Balancer, em `/api`, com egress pela VPC para chegar à WebApi | Proposta |
| Angular · PWA | nginx na porta 4200 | A mesma imagem nginx em Cloud Run, atrás do Load Balancer, em `/` | Proposta |
| Domínio e HTTPS | `localhost`, sem TLS | Load Balancer HTTPS externo com certificado gerenciado pelo Google. Falta escolher o domínio | Proposta |
| Chave do JWT | Em `appsettings.Development.json` e no compose, a mesma na WebApi e no BFF | Secret Manager, injetada nos dois serviços. Sem chave de 32 bytes ou mais, a aplicação não sobe | Decidido |
| E-mail | Mailpit em container, com a caixa em `localhost:8025` | Um provedor SMTP. Só muda a seção `Auth:Email` | A definir |
| Login com Google | Desligado até haver `GOOGLE_CLIENT_ID` no `.env` | Client ID com o domínio do Load Balancer como origem autorizada. Depende do domínio | Em aberto |
| Usuários demo | Criados na subida pela WebApi | Desligados. `Auth:SemearUsuariosDemo = false` é o padrão da imagem | Decidido |

Todas as peças já têm imagem, e `docker compose up -d --build` sobe a stack inteira. Script de deploy só existe para o Events (`deploy/events-cloud-run-job.sh`).

## No celular

O front já é PWA: manifest, ícones de 72 a 512 px e o service worker do Angular. Publicado em HTTPS, o usuário instala pelo navegador, sem loja e sem baixar nada além da própria página.

### Android Chrome

1. Abra o endereço do front no Chrome.
2. Toque em **Instalar** no aviso que aparece embaixo, ou no menu **⋮** → **Instalar app**. Em algumas versões o item se chama **Adicionar à tela inicial**.
3. Confirme. O ícone vai para a tela inicial e para a gaveta de apps, e abre em tela cheia, sem a barra do navegador.

O Chrome só oferece instalar com HTTPS, manifest válido e service worker ativo. Os três já existem; falta o HTTPS, que vem do Load Balancer.

### iPhone Safari, ou Chrome e Edge no iOS 16.4+

1. Abra o endereço do front.
2. Toque em **Compartilhar**, o quadrado com a seta para cima.
3. Escolha **Adicionar à Tela de Início** e toque em **Adicionar**.

O iOS não sugere a instalação sozinho. Quem não conhece o caminho não acha; vale uma dica na tela de entrada ou no slide da apresentação.

### Antes de publicar

- **O endereço do BFF.** `API_BASE_URL` está fixo em `http://localhost:5100/api`, em `src/app/core/api.config.ts`. No celular, `localhost` é o próprio aparelho. Com o Load Balancer vira `/api` relativo; no local, um proxy do `ng serve` e do nginx manda `/api` para o BFF.
- **HTTPS de verdade.** Sem ele o service worker não registra e não há instalação. Abrir por `http://192.168…` na rede de casa não serve para testar.
- **Endereços que citam o front.** A origem autorizada do Client ID do Google e o `Auth:RedefinicaoSenha:UrlDoFront`, que vai no link do e-mail, passam a apontar para o domínio novo.
- **Usuários de demonstração.** Desligados por padrão na imagem. Para apresentar no GCP, ligar `Auth:SemearUsuariosDemo` de propósito, e desligar depois.

### Depois de instalado

- **Atualização.** O service worker baixa a versão nova em segundo plano, e ela entra na próxima vez que o app abre. Funciona porque o nginx serve `index.html` e `ngsw.json` sem cache.
- **Sem rede.** O app abre do cache e a tela avisa "Sem conexão. Mostrando o último saldo recebido." Lançar exige conexão.
- **Na apresentação.** Um QR code com o endereço: quem escaneia abre no navegador e instala dali.
- **Loja é opcional.** A Play Store aceita o PWA embrulhado como TWA, com o Bubblewrap. A App Store exige um app nativo em volta, como o Capacitor.

## Referências

A POC combina cinco padrões do catálogo de Chris Richardson, em [microservices.io](https://microservices.io/patterns/).

| Padrão | O que é | Na POC |
| --- | --- | --- |
| [Transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html) | Grava o evento a publicar na mesma transação do dado de negócio, e publica depois | A coluna `Stage` de `LancamentosDiarios`, que nasce `Cadastrado` no mesmo `INSERT` do lançamento |
| [Polling publisher](https://microservices.io/patterns/data/polling-publisher.html) | Um processo consulta o outbox de tempos em tempos e publica o que encontra | `RelayLancamentos`, disparado pelo `RelayWorker` a cada 2 s no local e pelo Cloud Scheduler a cada 60 s na nuvem |
| [Transaction log tailing](https://microservices.io/patterns/data/transaction-log-tailing.html) | A alternativa ao polling: ler o log de transações do banco e publicar cada mudança | Não usado. No SQL Server seria o Change Data Capture com um conector como o Debezium |
| [Idempotent consumer](https://microservices.io/patterns/communication-style/idempotent-consumer.html) | O consumidor reconhece e descarta mensagens repetidas | O estágio esperado no `WHERE` de cada `UPDATE`. O catálogo sugere guardar os ids das mensagens já processadas; aqui o estágio da própria linha cumpre esse papel |
| [Access token](https://microservices.io/patterns/security/access-token.html) | Quem está na borda autentica a requisição e passa adiante um token, como um JWT, que identifica o usuário para os serviços | O JWT que o BFF valida e repassa à WebApi, com `sub`, `role` e `conta`. Aqui quem emite é a própria WebApi, e não o gateway |