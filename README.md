# Lançamentos Diários — POC

Conta corrente com saldo consolidado assíncrono: o lançamento é gravado na hora, a
consolidação acontece depois, por evento. A tela mostra o saldo consolidado, o que
ainda está em trânsito e o saldo projetado.

Stack: **.NET 9** (Minimal API + CQRS + FluentValidation + Dapper), **SQL Server**,
**Google Cloud Pub/Sub**, **Angular 20** como PWA.

Mesmo motor local e em produção: SQL Server em container aqui, Cloud SQL for SQL
Server no GCP. Um dialeto só, um caminho de código.

---

## O fluxo

```
Angular (PWA)
    │  http://localhost:4200
    ▼
BFF  ──────────────►  WebApi  ──────────────►  LancamentosDiarios (SQL Server)
:5100                 :5101                     Stage = Cadastrado
                                                        │
                                                        ▼
                                          Events (relay)  lê a tabela
                                                        │  Stage = Lido
                                                        ▼
                                              Google Cloud Pub/Sub
                                                        │  Stage = Enfileirado
                                                        ▼
                                          Consumer  recebe da fila
                                                        │  Stage = EmProcessamento
                                                        ▼
                                          SaldoConsolidado (mesmo banco)
                                                           Stage = Consolidado
```

O front **nunca** fala com a WebApi direto — só com o BFF, sempre com o Bearer do
usuário logado (veja [Autenticação e acesso](#autenticação-e-acesso)).

## A máquina de estados

É o padrão *Transactional Outbox* (ver [Referências](#referências)), numa variação sem
tabela de outbox separada: a própria `LancamentosDiarios` carrega o estágio numa coluna.
É ela que garante que nada seja publicado nem somado ao saldo duas vezes.

| # | Stage | Quem grava | O que significa |
|---|-------|-----------|-----------------|
| 1 | `Cadastrado` | WebApi | Lançamento gravado, ninguém leu ainda |
| 2 | `Lido` | WebApi ou Events | Reservado para publicar, pela publicação imediata ou pelo relay |
| 3 | `Enfileirado` | WebApi ou Events | Publicado no Pub/Sub |
| 4 | `EmProcessamento` | Consumer | Recebido da fila, consolidando |
| 5 | `Consolidado` | Consumer | Somado ao saldo consolidado |
| 6 | `Erro` | Events ou Consumer | Falhou após esgotar as tentativas. Na publicação, segura os lançamentos seguintes da conta |

Os estágios 1 a 4 são o que a tela chama de **pendente**, e o que alimenta o
"saldo projetado" e o aviso de consolidação em andamento.

**Idempotência**: todo `UPDATE` de transição traz o estágio esperado no `WHERE`. Como
o Pub/Sub entrega *pelo menos uma vez*, uma reentrega encontra a linha já fora do
estágio de origem, não atualiza nada, e o consumidor dá ack sem somar de novo.

**Reserva vencida**: um `Lido` parado há mais de `Relay:ReservaExpiraEm` (1 minuto) volta
a ser candidato na própria consulta de reserva do relay. Não há processo separado para
isso: é o que recolhe a linha de quem reservou e caiu antes de publicar ou de marcar.

## Ordem dentro da conta

Cada lançamento pertence a uma **conta** (`ContaId`) e recebe uma **sequência**
(`Sequencia`) dentro dela, começando em 1. A ordem importa por conta; contas
diferentes são independentes e seguem em paralelo.

A garantia vem de duas peças que se completam:

| Garante | Quem |
|---|---|
| ordem de **publicação** — sem buracos | a tabela de outbox |
| ordem de **entrega** — retry, reentrega | a *ordering key* do Pub/Sub |

**Por que o broker sozinho não basta.** O Pub/Sub ordena o que foi publicado; ele não
sabe de um lançamento que nunca chegou até ele. Se a sequência 1 falhasse ao publicar e
a 2 desse certo, o broker entregaria a 2 sem ter o que segurar — e a inversão seria
permanente. Por isso a consulta de reserva nunca traz um lançamento que tenha
predecessor pendente na mesma conta, e a publicação imediata da WebApi só acontece
quando a conta está em dia.

**A ordering key é a CONTA**, não o lançamento:

```
orderingKey = "ABC1234"          ← só a conta
payload     = { sequencia: 2 }   ← o número vai no corpo
```

Uma chave por mensagem (`ABC1234-2`) desligaria a ordenação sem aviso: ordenação só
existe *entre* mensagens que compartilham a chave.

**Não é preciso limitar a concorrência do consumidor a 1.** Com ordenação habilitada o
Pub/Sub já entrega uma mensagem por vez dentro de cada chave e só libera a próxima após
o ack. Baixar `Consumer:Concorrencia` para 1 serializaria contas independentes entre si,
sem ganho de ordem nenhum.

**Erro trava a conta, de propósito.** Uma linha em `Erro` é estágio bloqueante: os
sucessores da mesma conta não passam por cima dela. Consolidar fora de ordem em silêncio
seria pior do que parar e pedir intervenção.

### Duas armadilhas que custaram caro

**`ResumePublish`.** Quando um publish com ordering key falha, o cliente do Pub/Sub põe
*a chave inteira* em estado de erro e passa a recusar tudo dela com `Cannot publish due
to error state in ordering` — inclusive as próximas tentativas do mesmo lançamento. É
deliberado: é assim que ele evita entregar fora de ordem. Sem chamar `ResumePublish` no
`catch`, a conta nunca mais publica.

**Backoff.** Sem espera crescente entre tentativas, uma indisponibilidade de dez segundos
do broker queima as cinco tentativas em segundos e para o lançamento em `Erro` —
derrubando a conta inteira por uma falha passageira. `ProximaTentativaEm` segura a
retentativa, com o atraso dobrando até o teto de `Relay:BackoffMaximo`.

## Autenticação e acesso

```
Angular ──Bearer──► BFF ──o mesmo Bearer──► WebApi
                    valida o JWT,            valida o JWT de novo,
                    role e conta             role e conta; é quem emite o token
```

**Quem emite.** Só a WebApi, em `/auth/*`: cadastro, login, Google, renovação, logout,
"esqueci a senha" e redefinição. O BFF expõe as mesmas rotas em `/api/auth/*` como
repasse puro — o front continua falando só com o BFF.

**O token.** JWT HS256 de 15 minutos com `sub`, `email`, `name`, `role` e `conta`.
WebApi e BFF validam com a mesma `Auth:Jwt:Chave` (mínimo de 32 bytes; sem ela a
aplicação nem sobe). A chave não fica no `appsettings.json`: local vem do
`appsettings.Development.json` e do compose; no GCP, do Secret Manager.

**Renovação e logout.** Junto com o JWT vai um token de renovação opaco, de uso único,
válido por 7 dias; o banco guarda só o SHA-256 dele. Cada renovação troca o par inteiro.
Um token já trocado que reaparece é sinal de cópia vazada, e derruba todas as sessões do
usuário. O logout revoga a renovação; o JWT já emitido só vence — daí os 15 minutos.
O front renova sozinho no primeiro 401, uma renovação por vez.

**Roles.** Duas policies, aplicadas nos endpoints do BFF e da WebApi:

| Policy | Roles | Endpoints |
|---|---|---|
| `ClienteOuAdmin` | Cliente, Admin | saldo, novo lançamento, consulta de lançamento, `/auth/eu` |
| `SomenteAdmin` | Admin | `/contas`, `/lancamentos/lote` |

Fechado por padrão: endpoint sem policy exige login (fallback policy). O que é público —
`/health`, OpenAPI e `/auth` — declara `AllowAnonymous`.

**Uma conta por usuário.** `UX_Usuarios_ContaId` garante um dono por conta, e a conta
vai no claim `conta`. A role diz o que o usuário pode fazer; a posse, sobre qual conta:
o Cliente só alcança a dele (403 nas outras), o Admin alcança qualquer uma. O BFF confere
para recusar cedo e a WebApi confere de novo, porque é ela quem guarda o dado. No
cadastro a conta é sorteada no formato `AAA0000` e nunca reaproveita uma conta que já
tenha lançamento.

**Google.** O botão do Google Identity Services entrega um ID token ao front; a WebApi
confere a assinatura e se a audiência é o nosso Client ID, e troca pela nossa sessão.
No primeiro acesso cadastra; se o e-mail já tem cadastro com senha, vincula — só com
e-mail verificado pelo Google. Para ligar:

1. No Google Cloud Console, crie um *OAuth Client ID* do tipo **Aplicativo da Web**, com
   `http://localhost:4200` em *Origens JavaScript autorizadas*.
2. Ponha `GOOGLE_CLIENT_ID=<client id>` num `.env` ao lado do `docker-compose.yml` (ou
   `Auth:Google:ClientId` no `appsettings.Development.json` da WebApi, para `dotnet run`).

Sem Client ID o botão simplesmente não aparece.

**Esqueci a senha.** Link de uso único, válido por 30 minutos, mandado por SMTP. Local,
o e-mail cai no **Mailpit**, em http://localhost:8025. A resposta é 202 exista o e-mail
ou não, para não contar quais têm cadastro. Redefinir a senha encerra todas as sessões.

### Usuários de demonstração

Com `Auth:SemearUsuariosDemo=true` (ligado no compose e no Development), a WebApi cria na
subida, todos com a senha `Demo@2026`:

| E-mail | Role | Conta |
|---|---|---|
| `admin@lancamentos.local` | Admin | `ADM0001` |
| `cliente@lancamentos.local` | Cliente | `ABC1234` |
| `cliente2@lancamentos.local` | Cliente | `XYZ9999` |

`ABC1234` e `XYZ9999` já tinham lançamentos de antes do login existir; ganharam um dono.

### Lançamentos em lote

`POST /lancamentos/lote`, direto na WebApi e só para Admin: até 1.000 lançamentos, em
contas quaisquer, numa transação só. Cada item segue as regras do lançamento avulso, e o
erro aponta o item (`Itens[3].Valor`). Para simular carga na apresentação sem a tela.

O lote não publica no request; entrega ao relay. O relay publica **um lançamento por
conta por ciclo** (2 s no local — veja [capacidade-do-relay.md](docs/capacidade-do-relay.md)),
então 20 itens numa conta levam uns 40 s para consolidar, enquanto contas diferentes
andam em paralelo. Na tela, a lista de pendentes esvazia na ordem da conta.

Pelo Swagger da WebApi (http://localhost:5101/swagger): `POST /auth/login` com o admin,
copie o `tokenAcesso`, clique em **Authorize** e chame `POST /lancamentos/lote`:

```json
{
  "itens": [
    { "contaId": "ABC1234", "tipo": "Credito", "valor": 100.00, "dataLancamento": "2026-10-01" },
    { "contaId": "XYZ9999", "tipo": "Debito",  "valor": 35.90,  "dataLancamento": "2026-10-01", "observacao": "tarifa" },
    { "contaId": "ABC1234", "tipo": "Debito",  "valor": 20.00,  "dataLancamento": "2026-09-30" }
  ]
}
```

Ou gerando N lançamentos aleatórios pelo PowerShell:

```powershell
$api   = "http://localhost:5101"
$login = Invoke-RestMethod -Method Post "$api/auth/login" -ContentType "application/json" `
           -Body '{"email":"admin@lancamentos.local","senha":"Demo@2026"}'

$itens = 1..50 | ForEach-Object {
    @{
        contaId        = Get-Random -InputObject "ABC1234", "XYZ9999"
        tipo           = Get-Random -InputObject "Credito", "Debito"
        valor          = [math]::Round((Get-Random -Minimum 100 -Maximum 50000) / 100, 2)
        dataLancamento = Get-Date -Format "yyyy-MM-dd"
    }
}

Invoke-RestMethod -Method Post "$api/lancamentos/lote" -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $($login.tokenAcesso)" } `
  -Body (@{ itens = $itens } | ConvertTo-Json -Depth 3)
```

## Rodando

### Tudo em Docker

Para apresentar, ou para rodar sem .NET nem Node na máquina — só Docker:

```bash
docker compose up -d --build     # http://localhost:4200

# em caso de reset, rodar antes do up:
docker system df       # quanto ocupam imagens, containers, volumes e cache de build
docker system prune    # apaga containers parados, redes sem uso, imagens sem tag e cache de build
docker builder prune   # só o cache de build, sem mexer nos containers
```

Sobe os oito containers: SQL Server, emulador do Pub/Sub, Mailpit, WebApi, BFF, Events,
Consumer e o front servido por nginx. As portas no host são as mesmas do `dotnet run`
(`:4200`, `:5100`, `:5101`), então o front funciona sem mudar o endereço do BFF.

| Endereço | O quê |
|---|---|
| http://localhost:4200 | a tela — entre com um dos [usuários de demonstração](#usuários-de-demonstração) |
| http://localhost:5100/swagger | Swagger do BFF |
| http://localhost:5101/swagger | Swagger da WebApi, com o lote |
| http://localhost:8025 | Mailpit: os e-mails de "esqueci a senha" |

A ordem de subida é controlada por healthcheck. A WebApi é a única que cria banco e
esquema, e só responde `/health` depois disso; o Events e o Consumer esperam por ela.
O script de esquema é idempotente, mas não protege contra três processos criando o
banco no mesmo instante.

| Imagem | Base | No GCP |
|---|---|---|
| `lancamentos/webapi` | `aspnet:9.0` | a definir |
| `lancamentos/bff` | `aspnet:9.0` | a definir |
| `lancamentos/events` | `runtime:9.0` | Cloud Run Job |
| `lancamentos/consumer` | `runtime:9.0` | Cloud Run Service |
| `lancamentos/frontend` | `nginx:1.27-alpine` | a definir |

As imagens trazem os padrões de produção (`PubSub__CriarRecursos=false`, e o Events
com `Relay__LoopContinuo=false`); o `docker-compose.yml` sobrescreve por variável de
ambiente o que muda no local. É a mesma imagem nos dois lados.

`docker compose down` para tudo e mantém os saldos; `docker compose down -v` zera.

### Dados de demonstração

O [`scripts/inicializar-banco-demo.ps1`](scripts/inicializar-banco-demo.ps1) deixa o banco
pronto para apresentar: lança uma semana de movimento em três contas e espera tudo
consolidar.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\inicializar-banco-demo.ps1
```

| Conta | Quem entra | Movimento lançado | Saldo |
|---|---|---|---|
| `ABC1234` | `cliente@lancamentos.local` | padaria: vendas, fornecedor, aluguel, salários | R$ 9.164,88 |
| `XYZ9999` | `cliente2@lancamentos.local` | oficina: serviços, peças, folha de pagamento | R$ 3.138,50 |
| `ADM0001` | `admin@lancamentos.local` | aporte inicial e licenças | R$ 8.750,00 |

O script passa pelo caminho da aplicação, sem escrever direto no banco: login do admin,
`POST /lancamentos/lote`, relay, Pub/Sub e Consumer. No fim, confere o saldo de cada
conta contra a soma esperada. Leva uns 30 s, porque o relay publica um lançamento por
conta a cada ciclo. As datas vão de seis dias atrás até hoje, porque a WebApi recusa
data futura. A tela mostra o saldo de cada conta, não a lista de lançamentos. Para
mostrar a consolidação ao vivo, rode o script na apresentação e recarregue a tela logo
depois do passo 3: a lista de pendentes esvazia na ordem da conta.

Rodar de novo é seguro. Se o movimento de demonstração já estiver no banco, o script só
espera e confere; se as contas tiverem outro movimento, ele para sem lançar nada. Serve
para as duas formas de rodar, já que a WebApi atende em `:5101` nas duas.

Para recomeçar do zero, na stack do Docker:

```powershell
# apaga o banco inteiro, inclusive os cadastros feitos pela tela; pede confirmação
powershell -ExecutionPolicy Bypass -File .\scripts\inicializar-banco-demo.ps1 -Zerar
```

O `-Zerar` faz o `docker compose down -v` e sobe a stack outra vez. Os e-mails do
Mailpit também somem, porque ele não tem volume. A confirmação mostra as contas
cadastradas pela tela que vão junto, e o padrão é não apagar.

O `-ExecutionPolicy Bypass` existe porque o Windows bloqueia scripts por padrão. Ele
vale só para esse comando e não muda a política da máquina.

### Desenvolvimento com `dotnet run`

#### Pré-requisitos

- .NET SDK 9
- Node 20 e Angular CLI 20
- Docker (SQL Server + emulador do Pub/Sub + Mailpit)

#### 1. Infraestrutura

```bash
docker compose up -d sqlserver pubsub mailpit   # SQL :1433, Pub/Sub :8085, Mailpit :8025
```

Só os serviços de infraestrutura: um `docker compose up -d` sem nomes sobe a stack
inteira, e os containers da aplicação ocupariam as portas do `dotnet run`.

Os clientes do Google usam `EmulatorDetection.EmulatorOrProduction`: com
`PUBSUB_EMULATOR_HOST` definida falam com o container, sem ela vão para o Pub/Sub real.
Nenhuma linha de código muda entre os dois casos.

> O emulador do Pub/Sub guarda tudo em memória. Reiniciar o container apaga tópico e
> subscription — reinicie o Events e o Consumer para recriá-los. Para simular queda do
> broker sem perder o estado, use `docker compose pause/unpause pubsub`.

#### 2. Os quatro processos .NET

Cada um numa aba:

```bash
dotnet run --project MeusLancamentosDiarios.Integrator.WebApi    # :5101
dotnet run --project MeusLancamentosDiarios.Integrator.Bff       # :5100
dotnet run --project MeusLancamentosDiarios.Integrator.Events    # relay
dotnet run --project MeusLancamentosDiarios.Integrator.Consumer  # consolidação
```

`Events` e `Consumer` já trazem `PUBSUB_EMULATOR_HOST=localhost:8085` no
`launchSettings.json`. WebApi e BFF leem a chave do JWT do `appsettings.Development.json`,
e a WebApi semeia os usuários de demonstração.

#### 3. O front

```bash
cd MeusLancamentosDiarios.Integrator.FrontEnd
npm start          # http://localhost:4200
```

### Banco

SQL Server no container, banco `Lancamentos`, compartilhado pelos três processos que
acessam dados. Banco e esquema são criados na subida, de forma idempotente — sem EF Core
não há migrations. Para zerar tudo:

```bash
docker compose down -v && docker compose up -d sqlserver pubsub mailpit
```

Mexendo à mão com o `sqlcmd`, passe `-I`: as tabelas de usuário têm índice filtrado, e
sem `QUOTED_IDENTIFIER` ligado o SQL Server recusa `INSERT`, `UPDATE` e `DELETE` nelas.

A coluna `Usuarios.Role` nasceu `Papel`. Num banco criado antes da troca, a subida da
WebApi renomeia com `sp_rename` e preserva os dados; banco novo já nasce com `Role`.

Em produção, `Banco:CriarBanco = false`: o banco vem do provisionamento e a aplicação
não deve ter permissão para criá-lo.

## Os projetos

| Projeto | O que é |
|---|---|
| `.WebApi` | Minimal API de domínio. CQRS com dispatcher próprio, FluentValidation, Dapper. Usuários e emissão do JWT. |
| `.Bff` | Minimal API exclusiva do Angular. Sem regra de negócio: agrega, formata em pt-BR e repassa o Bearer. |
| `.Messages` | Só os requests e responses da WebApi — o que o BFF envia e lê, e o que outro cliente precisaria. Pacote `POCMica.MeusLancamentosDiarios.Integrator.Messages`. |
| `.Common` | O que BFF, WebApi e workers têm em comum e não é mensagem: enums, helpers, roles, claims, regra de posse, `ICommand`/`IQuery`. Sem ASP.NET Core. Pacote `POCMica.MeusLancamentosDiarios.Integrator.Common`. |
| `.Events` | Worker do relay + contratos de evento + camada de dados compartilhada. |
| `.Consumer` | Worker que assina o Pub/Sub e aplica o lançamento ao saldo. |
| `.FrontEnd` | Angular 20 standalone, signals, PWA. |
| `tests/` | Cinco projetos de teste mais o `.TestSupport` compartilhado. |

### Organização do código

**Camadas do BFF.** O endpoint só faz binding e devolve o resultado:

```
endpoint ──► PosseDaContaFilter ──► I*Service ──► ILancamentosApiClient ──► WebApi
              recusa 403 cedo        regra da tela      HTTP + Bearer repassado
                                     e IConversor (de-para para o payload da tela)
```

A WebApi tem o mesmo `PosseDaContaFilter`, ligado por `.ExigirPosseDaConta()` no
endpoint: confere a conta da rota, do corpo (`IReferenciaConta`) e da resposta.

**Quem depende de quem.**

```
Bff ─────► Messages ─► Common ◄─ Events ◄─ Consumer
WebApi ──► Messages      ▲          ▲
   └─────────────────────┴──────────┘
```

O `.Messages` tem só request e response: tudo que a WebApi recebe no corpo ou devolve,
inclusive o lote e o detalhe do lançamento, que o BFF não usa mas outro cliente usaria.
As queries (`ObterSaldoQuery`, `ObterLancamentoQuery`...) ficam na WebApi: são montadas
da rota e do token, nunca viajam como corpo. O `.Common` não leva ASP.NET Core porque o
Events e o Consumer dependem dele e rodam na imagem `runtime:9.0`; por isso o
`PosseDaContaFilter` continua com uma cópia em cada API, e só a regra
(`PodeAcessarConta`) é compartilhada. O evento do Pub/Sub fica no `.Events`: é contrato
entre o relay e o Consumer, não entre BFF e WebApi.

**Interfaces em `Contracts/`.** Cada interface fica numa subpasta `Contracts` ao lado da
implementação, com namespace próprio — `Cqrs/Contracts/IDispatcher.cs` junto de
`Cqrs/Dispatcher.cs`, e `Common/Cqrs/Contracts/ICommand.cs`. Um tipo por arquivo. A
exceção é o `.Messages`, que é todo ele contrato. A pasta `Bff/Contracts`, no topo, é
outra coisa: os payloads da tela.

**Nomes.**

| Sufixo | Para | Exemplo |
|---|---|---|
| `Entity` | linha do banco | `LancamentoEntity`, `UsuarioEntity` |
| `Command` / `Query` | mensagem do CQRS | `CriarLancamentoCommand`, `ObterSaldoQuery` |
| `Response` | o que a WebApi devolve, inclusive os itens | `CriarLancamentoResponse`, `LancamentoDoLoteResponse` |
| `Tela` | o que o BFF devolve ao front | `SaldoTela`, `PendenteTela` |
| `Service` | camada de serviço do BFF | `SaldoService` |
| `Conversor` | de-para entre dois contratos | `SaldoTelaConversor` |
| `Helper` | funções puras compartilhadas | `DinheiroHelper`, `LancamentoHelper` |
| `Enum` | todo enum | `StageLancamentoEnum`, `TipoLancamentoEnum` |
| `Options` | seção do appsettings | `JwtOptions`, `SaldoOptions` |

**Configuração só no appsettings.** As classes `*Options` não têm valor padrão no código.
Cada uma expõe `Valida` e é registrada com `.Validate(o => o.Valida).ValidateOnStart()`:
faltou um valor, a aplicação não sobe, em vez de rodar com um padrão escondido. Exemplo: o
saldo detalha `Saldo:LimitePendentesPadrao` pendentes (20) quando a chamada não manda
`?limitePendentes=`; mandar zero ou negativo é 400.

### Testes

```bash
dotnet test
```

| Projeto de teste | Cobre |
|---|---|
| `.Events.Tests` | relay, ordem por conta, backoff, conversão de dinheiro |
| `.WebApi.Tests` | criação avulsa e em lote, validators, dispatcher, saldo e limite de pendentes; login, Google, renovação, redefinição de senha, cadastro e posse da conta |
| `.Bff.Tests` | services, conversores para o payload da tela, repasse do Bearer, roteamento do `/auth` e posse da conta |
| `.Common.Tests` | `DinheiroHelper` (centavos ↔ reais, arredondamento) e `LancamentoHelper` (formatação pt-BR) |
| `.Consumer.Tests` | consolidação, idempotência, corpo inválido, falha |
| `.TestSupport` | base `Cenario` e construtores NBuilder, compartilhados |

**xUnit + Moq + NBuilder + FluentAssertions**, no estilo **Case / When / Then**:

- a classe externa nomeia o objeto sob teste;
- cada classe aninhada descreve **um contexto** — `QuandoExistePredecessorPendenteNaConta`;
- `Case()` monta o cenário, `When()` executa a ação uma vez, e cada `[Fact]` afirma
  **um** Then. Quando um quebra, o nome do teste já diz qual comportamento se perdeu.

Os cenários com muitas variações usam **classes de equivalência** em `[Theory]`, em vez
de um teste por valor: o validador de conta, por exemplo, cobre vazia, abaixo do mínimo,
nos dois limites, acima do máximo, com espaço e com símbolo — numa tabela só.

O `Cenario` implementa `IAsyncLifetime`, então o xUnit monta o contexto antes de cada
`[Fact]` e os fatos não interferem entre si.

### Por que a camada de dados mora no `.Events`

`WebApi` e `Consumer` referenciam `.Events`, que carrega os contratos de evento **e** o
repositório Dapper. Assim os três processos falam com a tabela pelo mesmo código, em vez
de cada um ter a sua cópia do SQL de transição de estágio.

## Decisões que valem registro

**Dinheiro em centavos (`bigint`).** `SUM()` sobre ponto flutuante acumularia erro em
cima de saldo. A conversão fica em `Dinheiro.ParaReais` / `ParaCentavos`.

**Reserva num comando só.** `WITH candidatos AS (SELECT TOP (n) … WITH (UPDLOCK,
READPAST, ROWLOCK)) UPDATE … OUTPUT inserted.*` seleciona e marca sem janela entre ler e
reservar. `READPAST` faz instâncias concorrentes do relay pularem linhas já travadas por
outra, em vez de esperarem por elas.

**`UPDATE` seguido de `INSERT` no lugar de `MERGE`.** O `MERGE` do SQL Server tem
histórico de problemas de concorrência; o padrão `UPDATE … IF @@ROWCOUNT = 0 INSERT`
dentro da transação é mais previsível.

**Sem type handlers do Dapper.** `uniqueidentifier`, `date` e `datetimeoffset` têm
equivalente direto em .NET, então Guid, `DateOnly` e `DateTimeOffset` fazem round-trip
sozinhos. Com SQLite isso exigia três handlers — e cada um deles produziu um bug antes
de acertar o formato.

**Polling condicional no lugar de SignalR.** O BFF devolve `intervaloPollingMs > 0`
apenas enquanto há lançamento em trânsito, e `0` quando tudo consolidou — em repouso o
front não faz requisição nenhuma. SignalR exigiria um caminho `Consumer → BFF` (segunda
subscription ou Redis como backplane) para economizar ~2 segundos. A troca é local ao
`LancamentosService`: os componentes leem signals e não sabem a origem do dado.

**Data futura é recusada, não agendada.** O validator barra data de lançamento depois de
hoje (no fuso de São Paulo), e o calendário da tela tem `max` em hoje. Segurar um
lançamento futuro até a data não serve: a conta consolida em ordem de sequência, e um
pendente bloqueia os sucessores — um crédito do dia 6 travaria tudo o que fosse lançado
até lá. Se um dia houver agendamento, ele é outra coisa: uma tabela de agendados e um job
que, na data, cria um lançamento comum.

**JWT simétrico, chave compartilhada.** HS256 com a mesma chave na WebApi e no BFF é o
mínimo para os dois validarem o mesmo token. O passo seguinte é RS256: a WebApi assina com
a chave privada e publica a pública em JWKS, e o BFF deixa de ter um segredo capaz de
emitir token.

**Dispatcher de CQRS próprio, sem MediatR.** São ~60 linhas e evita trazer a mudança de
licenciamento do MediatR para dentro do projeto.

**`NuGet.config` com `<clear />`.** A POC não consome pacote interno; sem isso o restore
tenta o feed privado `Agro_Feed` e falha com 401.

**Cores do carrefoursolucoes.com.br.** O front usa o esquema de cores do site, medido
nele: azul `#1E5BC6` como marca, magenta `#E6007E` só na ação principal (novo
lançamento) e o degradê do destaque no saldo projetado. Os tokens têm nome de função
(`--azul`, `--credito`, `--fundo`), então trocar a identidade é mexer só no `:root` do
`styles.scss`. Paleta, derivados e contraste em
[docs/identidade-visual.md](docs/identidade-visual.md).

**Vermelho no débito.** Fora da paleta do site, usado só no formulário como semântica de
valor negativo; o magenta do site é de ação, não de alerta. Na lista de pendentes o
débito fica em cinza.

**Fonte Kumbh Sans.** Livre (SIL OFL) e a mais próxima da Azo Sans, que é paga e não pode
ficar num repositório público. A escolha foi por medição, entre 20 fontes do Google Fonts.
No front ela vem do pacote `@fontsource-variable/kumbh-sans`, então vai no build e o PWA
funciona offline com ela; no documento de arquitetura, vem do Google Fonts. Detalhes em
[docs/identidade-visual.md](docs/identidade-visual.md#tipografia).

## Indo para o GCP

### Vindo da AWS

| AWS | GCP | Papel |
|---|---|---|
| EventBridge Scheduler | **Cloud Scheduler** | o gatilho por cron |
| Lambda | **Cloud Run Job** | executa e encerra; mesmo container, mesma DI |
| Lambda | Cloud Functions 2ª geração | exige o `Functions.Framework` — reescreve o projeto |
| EventBridge Bus + Rules | Eventarc | roteamento de eventos entre serviços |
| SNS / SQS | Pub/Sub | tópico e fila |
| SQS → Lambda | Pub/Sub *push* → Cloud Run Service | entrega por HTTP, escala a zero |
| ECS / Fargate (serviço) | Cloud Run Service `min-instances ≥ 1` | processo sempre ligado |
| ALB + certificado do ACM | **Load Balancer HTTPS externo** | uma porta só: `/` para o front, `/api/*` para o BFF |
| CloudFront + S3 | Load Balancer + nginx no Cloud Run | servir o build do Angular |
| API Gateway (HTTP API) | API Gateway ou Apigee | só na frente do BFF: limite de requisições e JWT na borda |

Para .NET, **Cloud Run Job** é a escolha em cima de Cloud Functions: o Job roda o
container que você já tem, enquanto Cloud Functions obrigaria a reescrever o projeto no
formato de função.

**ALB ou API Gateway na porta de entrada.** O Load Balancer do desenho serve o front e
o BFF no mesmo domínio, sem CORS. Na AWS, a tradução direta é o ALB, com regra por
caminho. O API Gateway cobre só a metade da API. Para usá-lo, o desenho vira CloudFront
com o domínio, `/` num bucket S3 com o build do Angular, que dispensa o nginx, e `/api/*`
no API Gateway, que chega ao BFF no ECS por um VPC Link. A WebApi continua fora da
internet nos dois casos.

O API Gateway acrescenta limite de requisições por rota e cobrança por requisição, mais
barata que um ALB parado numa demo de pouco tráfego. A validação do JWT na borda não
funciona com o token de hoje. O validador do HTTP API só recebe `issuer` e `audience`,
busca as chaves públicas no JWKS do emissor e só aceita algoritmos de chave pública,
como o RS256. Não há onde colocar a chave HS256 compartilhada. As saídas são três:
- um Lambda authorizer, que vira o terceiro lugar com a chave;
- migrar para RS256, com o JWKS publicado num endereço HTTPS público;
- deixar a validação só no BFF e na WebApi, como hoje.

Sem limite de requisições como requisito, o ALB basta, porque o BFF já confere token,
role e conta.

### `.Events` — Cloud Scheduler → Cloud Run Job

Decisão tomada: o relay vai como **Cloud Run Job** disparado pelo **Cloud Scheduler**.
Cloud Functions obrigaria a reescrever o projeto no formato de função; o Job roda o
container que já existe.

O ciclo está isolado em `RelayLancamentos.ExecutarCicloAsync()`. Quem chama muda, o
código do relay não:

| Local | GCP |
|---|---|
| `RelayWorker` chama o ciclo em loop | Cloud Scheduler dispara um Cloud Run Job |
| `PUBSUB_EMULATOR_HOST` apontando pro container | Pub/Sub real, via credencial da service account |
| `CriarRecursos: true` cria tópico e subscription | `false` — provisionamento no Terraform |
| SQL Server em container | Cloud SQL for SQL Server, IP privado |

`Relay:LoopContinuo = false` liga o modo de ciclo único. Nele o worker roda uma vez e
**encerra o processo** via `IHostApplicationLifetime.StopApplication()` — sair de
`ExecuteAsync` não basta, o host continuaria esperando sinal de shutdown e o Job travaria
até o timeout. Falha no ciclo sai com código 1, que é como o Cloud Run Job identifica o
erro e aplica a política de retentativa.

Artefatos: [`MeusLancamentosDiarios.Integrator.Events/Dockerfile`](MeusLancamentosDiarios.Integrator.Events/Dockerfile)
e [`deploy/events-cloud-run-job.sh`](deploy/events-cloud-run-job.sh).

```bash
PROJECT_ID=meu-projeto \
CLOUDSQL_INSTANCE=meu-projeto:southamerica-east1:lancamentos \
./deploy/events-cloud-run-job.sh
```

#### Conectando ao Cloud SQL

**Não existe Cloud SQL Connector para .NET** — a biblioteca oficial cobre Go, Java,
Python e Node. E o socket Unix de `--add-cloudsql-instances` atende MySQL e PostgreSQL,
não SQL Server.

Sobra o caminho direto, que é também o mais simples: **IP privado da instância na porta
1433**, com **Direct VPC egress** dando ao Job acesso à VPC. A connection string inteira
fica no Secret Manager e é injetada como variável no runtime — a senha nunca entra na
imagem nem na linha de comando.

Verificado: a imagem do relay rodando em container conecta no SQL Server, reserva,
publica e encerra com código 0. Era exatamente isto que o SQLite impedia.

#### IAM da service account do relay

| Papel | Para quê |
|---|---|
| `roles/pubsub.publisher` | publicar no tópico `lancamentos-registrados` |
| `roles/cloudsql.client` | abrir conexão com o Cloud SQL |
| `roles/run.invoker` | permitir que o Scheduler execute o Job |

```bash
SA=lancamentos-relay@$PROJECT_ID.iam.gserviceaccount.com

gcloud pubsub topics add-iam-policy-binding lancamentos-registrados \
  --member "serviceAccount:$SA" --role roles/pubsub.publisher

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member "serviceAccount:$SA" --role roles/cloudsql.client

gcloud run jobs add-iam-policy-binding lancamentos-relay \
  --region southamerica-east1 \
  --member "serviceAccount:$SA" --role roles/run.invoker
```

#### O Scheduler tem granularidade mínima de 1 minuto

Localmente o relay roda a cada 2 segundos. Com Cloud Scheduler, o mais rápido possível é
**1 minuto** — o campo de minuto do unix-cron vai de 0 a 59, não há unidade menor. O
EventBridge Scheduler tem o mesmo piso: `rate()` aceita apenas `minutes | hours | days`, e
a documentação declara *"60 second precision"*.

Na prática, um lançamento pode esperar até ~60s antes de ser publicado, e o aviso de
"consolidação em andamento" fica na tela esse tempo todo. Quatro saídas:

| | Latência | Custo / complexidade |
|---|---|---|
| 1. Aceitar o 1 minuto | até 60s | nenhum |
| 2. Cloud Tasks se reagendando | ~5s | fila + reagendamento; instância quente |
| 3. Cloud Run Service `min-instances=1` | ~2s | uma instância parada |
| 4. **Híbrido** | **~0ms** | ~10 linhas no handler |

**1. Aceitar.** A tela já trata o atraso com honestidade: mostra o saldo projetado e o
aviso. Serve se o negócio tolera.

**2. Cloud Tasks.** É a forma de descer de 1 minuto no GCP — o `scheduleTime` da task tem
precisão de microssegundos. O relay enfileira a próxima task com `agora + 5s` ao terminar
o ciclo. Mas a 5 segundos são ~17 mil invocações/dia: o Cloud Run não escala a zero nesse
ritmo, o custo se aproxima do `min-instances=1` e ainda sobra latência. É o caminho mais
caro para o pior resultado entre 2, 3 e 4.

**3. Relay sempre ligado.** Cloud Run Service com `min-instances = 1` e
`LoopContinuo = true` — é literalmente o código de hoje, sem alteração nenhuma.

**4. Híbrido — o que eu recomendaria.** A WebApi publica no Pub/Sub logo após o commit, e
o Job de 1 em 1 minuto vira só a rede de proteção: varre o que ficou em `Cadastrado`
porque o publish falhou. A ordem importa — commit primeiro, publish depois; não há
atomicidade possível entre banco e broker, e é justamente por isso que a outbox existe.
Caminho feliz instantâneo, garantia de entrega intacta, e o piso de 1 minuto deixa de
incomodar porque só governa a varredura. É o padrão usual de outbox em produção.

A opção 4 é a implementada, em `CriarLancamentoHandler`: a WebApi publica logo após
gravar quando a conta não tem anterior pendente, e o relay recolhe o resto.

Quanto o relay consegue publicar numa rodada de 1 minuto, com números medidos:
[docs/capacidade-do-relay.md](docs/capacidade-do-relay.md).

### `.Consumer` — reativo, não encaixa em Scheduler

Cloud Scheduler não serve aqui: o Consumer não roda de tempos em tempos, ele fica
escutando uma *pull subscription*. Hoje é um processo sempre ligado — Cloud Run Service
com `min-instances = 1`, ou GKE.

Para deixá-lo serverless, troque a subscription de **pull para push**: o Pub/Sub passa a
fazer `POST` num endpoint HTTP, o `BackgroundService` sai e vira um endpoint de Minimal
API que chama a mesma lógica de consolidação. Aí o Cloud Run escala a zero entre
mensagens. É o equivalente ao *event source mapping* de SQS → Lambda.

## Referências

A POC combina quatro padrões do catálogo de Chris Richardson, em
[microservices.io](https://microservices.io/patterns/):

| Padrão | O que é | Na POC |
|---|---|---|
| [Transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html) | Grava o evento a publicar na mesma transação do dado de negócio, e publica depois | A coluna `Stage` de `LancamentosDiarios`, que nasce `Cadastrado` no mesmo `INSERT` do lançamento |
| [Polling publisher](https://microservices.io/patterns/data/polling-publisher.html) | Um processo consulta o outbox de tempos em tempos e publica o que encontra | `RelayLancamentos`, disparado pelo `RelayWorker` a cada 2 s no local e pelo Cloud Scheduler a cada 60 s no GCP |
| [Transaction log tailing](https://microservices.io/patterns/data/transaction-log-tailing.html) | A alternativa ao polling: ler o log de transações do banco e publicar cada mudança | Não usado. No SQL Server seria o Change Data Capture com um conector como o Debezium |
| [Idempotent consumer](https://microservices.io/patterns/communication-style/idempotent-consumer.html) | O consumidor reconhece e descarta mensagens repetidas | O estágio esperado no `WHERE` de cada `UPDATE`. O catálogo sugere guardar os ids das mensagens já processadas; aqui o estágio da própria linha cumpre esse papel |
