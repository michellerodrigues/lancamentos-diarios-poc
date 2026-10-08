# Diagramas C4 — níveis 3 e 4, e sequências internas

Componentes de cada container (nível 3), as classes que carregam as regras (nível 4) e a
sequência dos fluxos internos que não podem errar. Retrato do commit `45bcd17`, em
07/10/2026. Os níveis 1 e 2 estão em
[C4 contexto e containers](../solucao/02-c4-contexto-e-containers.md).

**Notação.** Os diagramas usam a notação do C4 desenhada em `flowchart` do Mermaid, porque o
tipo C4 nativo do Mermaid ainda é marcado como experimental e o GitHub o desenha com pouco
controle de layout. As cores seguem o C4:

| Cor | Elemento |
|---|---|
| Azul-escuro | pessoa |
| Azul-médio | container |
| Azul-claro | componente |
| Cinza | sistema ou container fora do escopo do diagrama |

## Nível 3 — Componentes

### WebApi

```mermaid
flowchart TB
    bff(["BFF<br/>[Container]"])
    adm(["Administrador<br/>Swagger ou script de demonstração"])

    subgraph webapi["WebApi [Container: .NET 9, Minimal API]"]
        direction TB
        ep_auth["AuthEndpoints<br/>[Endpoint] /auth/*"]
        ep_lanc["LancamentosEndpoints<br/>[Endpoint] /lancamentos"]
        ep_saldo["SaldoEndpoints<br/>[Endpoint] /saldo, /contas"]
        posse["PosseDaContaFilter<br/>[Filtro de endpoint]"]
        disp["Dispatcher<br/>[Mediator] valida e resolve o handler"]
        h_lanc["Handlers de lançamento<br/>Criar, Criar em lote, Obter"]
        h_saldo["Handlers de saldo<br/>ObterSaldo, ListarContas"]
        h_auth["Handlers de autenticação<br/>Login, Google, Registrar, Renovar,<br/>Sair, Esqueci e Redefinir senha"]
        sessao["Sessão<br/>AberturaDeSessao, EmissorDeTokens,<br/>CadastroDeCliente, SegredoOpaco"]
        urepo["UsuarioRepository<br/>[Dapper]"]
        gval["ValidadorGoogle"]
        smtp["SmtpEnviadorEmail"]
        boot["DatabaseBootstrapper<br/>AuthBootstrapper"]
        lrepo["LancamentoRepository<br/>[Dapper, vem do .Events]"]
        relay["RelayLancamentos.PublicarAsync<br/>[vem do .Events]"]
        pub["PubSubPublicador<br/>[vem do .Events]"]
    end

    sql[("Banco Lancamentos<br/>[SQL Server]")]
    ps[["Pub/Sub<br/>lancamentos-registrados"]]
    gid["Google Identity<br/>[Sistema externo]"]
    mail["Servidor SMTP<br/>[Sistema externo]"]

    bff -->|"HTTP/JSON + Bearer"| ep_auth
    bff -->|"HTTP/JSON + Bearer"| ep_lanc
    bff -->|"HTTP/JSON + Bearer"| ep_saldo
    adm -->|"POST /lancamentos/lote"| ep_lanc
    posse -.->|"filtra"| ep_lanc
    posse -.->|"filtra"| ep_saldo
    ep_auth --> disp
    ep_lanc --> disp
    ep_saldo --> disp
    disp --> h_lanc
    disp --> h_saldo
    disp --> h_auth
    h_lanc --> lrepo
    h_lanc --> relay
    relay --> lrepo
    relay --> pub
    pub -->|"gRPC, orderingKey = conta"| ps
    h_saldo --> lrepo
    h_auth --> sessao
    h_auth --> urepo
    sessao --> urepo
    h_auth --> gval
    h_auth --> smtp
    gval -->|"chaves públicas do Google"| gid
    smtp -->|"SMTP"| mail
    lrepo -->|"TDS 1433"| sql
    urepo -->|"TDS 1433"| sql
    boot -->|"DDL idempotente na subida"| sql

    classDef pessoa fill:#08427b,stroke:#052e56,color:#ffffff
    classDef container fill:#438dd5,stroke:#2e6295,color:#ffffff
    classDef componente fill:#85bbf0,stroke:#5d82a8,color:#000000
    classDef externo fill:#999999,stroke:#6b6b6b,color:#ffffff
    class adm pessoa
    class bff container
    class ep_auth,ep_lanc,ep_saldo,posse,disp,h_lanc,h_saldo,h_auth,sessao,urepo,gval,smtp,boot,lrepo,relay,pub componente
    class sql,ps,gid,mail externo
```

| Componente | Responsabilidade | Código |
|---|---|---|
| Endpoints | Rotas, policy por grupo, binding e status HTTP | [`Endpoints/`](../../../MeusLancamentosDiarios.Integrator.WebApi/Endpoints) |
| `PosseDaContaFilter` | Cliente só alcança a própria conta: confere rota (`contaId`), corpo (`IReferenciaConta`) e o recurso devolvido | [`Auth/PosseDaContaFilter.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/PosseDaContaFilter.cs) |
| `Dispatcher` | Roda o validator da mensagem, se houver, e chama o handler resolvido por tipo | [`Cqrs/Dispatcher.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Cqrs/Dispatcher.cs) |
| `CriarLancamentoHandler` | Grava e tenta a publicação imediata, com teto de 2 s | [`Features/Lancamentos/Commands/CriarLancamento/`](../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Lancamentos/Commands/CriarLancamento) |
| `CriarLancamentosEmLoteHandler` | Grava até 1.000 itens numa transação; publicação fica com o relay | [`Features/Lancamentos/Commands/CriarLancamentosEmLote/`](../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Lancamentos/Commands/CriarLancamentosEmLote) |
| `ObterSaldoHandler` | Saldo consolidado, soma dos pendentes e lista dos pendentes | [`Features/Saldo/Queries/ObterSaldo/`](../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Saldo/Queries/ObterSaldo) |
| Handlers de autenticação | Cadastro, login, Google, renovação com rotação, logout, redefinição de senha | [`Features/Auth/`](../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Auth) |
| `AberturaDeSessao` | O par JWT + renovação que todo login devolve | [`Auth/Tokens/AberturaDeSessao.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Tokens/AberturaDeSessao.cs) |
| `EmissorDeTokens` | JWT HS256 com `sub`, `email`, `name`, `jti`, `conta` e `role` | [`Auth/Tokens/EmissorDeTokens.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Tokens/EmissorDeTokens.cs) |
| `CadastroDeCliente` | Cria o usuário Cliente com conta sorteada `AAA0000`, livre e sem histórico | [`Features/Auth/CadastroDeCliente.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Features/Auth/CadastroDeCliente.cs) |
| `UsuarioRepository` | Usuários e tokens opacos, só por hash; consumo de token num comando só | [`Auth/Data/UsuarioRepository.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Data/UsuarioRepository.cs) |
| `ValidadorGoogle` | Confere assinatura, emissor, validade e audiência do ID token | [`Auth/Externos/ValidadorGoogle.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Externos/ValidadorGoogle.cs) |
| `SmtpEnviadorEmail` | E-mail de redefinição de senha | [`Auth/Externos/SmtpEnviadorEmail.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Externos/SmtpEnviadorEmail.cs) |
| Bootstrappers | Banco (se `Banco:CriarBanco`), esquema dos lançamentos, esquema de usuários e usuários de demonstração | `DatabaseBootstrapper` (Events), [`Auth/Data/AuthBootstrapper.cs`](../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Data/AuthBootstrapper.cs) |

### BFF

```mermaid
flowchart TB
    spa(["SPA Angular<br/>[Container]"])

    subgraph bff["BFF [Container: .NET 9, Minimal API]"]
        direction TB
        jwt["JwtBearer + policies<br/>[valida o token da WebApi]"]
        ep_auth["AuthEndpoints<br/>[Endpoint] /api/auth/*"]
        ep_telas["BffEndpoints<br/>[Endpoint] /api/saldo, /api/contas,<br/>/api/lancamentos"]
        posse["PosseDaContaFilter<br/>[Filtro, cópia do da WebApi]"]
        s_auth["AutenticacaoService<br/>[repasse sem transformação]"]
        s_saldo["SaldoService"]
        s_lanc["LancamentoService<br/>[conta do token para o Cliente]"]
        conv["Conversores<br/>SaldoTela, PendenteTela,<br/>LancamentoCriadoTela"]
        client["LancamentosApiClient<br/>[HttpClient tipado, timeout 10 s]"]
        repassa["RepassarTokenHandler<br/>[DelegatingHandler]"]
    end

    webapi(["WebApi<br/>[Container]"])

    spa -->|"HTTPS/JSON + Bearer"| ep_auth
    spa -->|"HTTPS/JSON + Bearer"| ep_telas
    jwt -.->|"protege"| ep_telas
    posse -.->|"filtra"| ep_telas
    ep_auth --> s_auth
    ep_telas --> s_saldo
    ep_telas --> s_lanc
    s_saldo --> conv
    s_lanc --> conv
    s_auth --> client
    s_saldo --> client
    s_lanc --> client
    client --> repassa
    repassa -->|"HTTP/JSON + o mesmo Bearer"| webapi

    classDef container fill:#438dd5,stroke:#2e6295,color:#ffffff
    classDef componente fill:#85bbf0,stroke:#5d82a8,color:#000000
    class spa,webapi container
    class jwt,ep_auth,ep_telas,posse,s_auth,s_saldo,s_lanc,conv,client,repassa componente
```

| Componente | Responsabilidade | Código |
|---|---|---|
| `BffEndpoints` | As três rotas das telas, todas com `ClienteOuAdmin`; `/api/contas` com `SomenteAdmin` | [`Endpoints/BffEndpoints.cs`](../../../MeusLancamentosDiarios.Integrator.Bff/Endpoints/BffEndpoints.cs) |
| `AuthEndpoints` | `/api/auth/*` vira `/auth/*` na WebApi | [`Endpoints/AuthEndpoints.cs`](../../../MeusLancamentosDiarios.Integrator.Bff/Endpoints/AuthEndpoints.cs) |
| `LancamentoService` | Monta o `CriarLancamentoCommand`: conta vazia usa o claim `conta` | [`Services/LancamentoService.cs`](../../../MeusLancamentosDiarios.Integrator.Bff/Services/LancamentoService.cs) |
| `SaldoTelaConversor` | Moeda, datas e rótulos em pt-BR; `intervaloPollingMs` = 2000 com pendente, 0 sem | [`Conversores/SaldoTelaConversor.cs`](../../../MeusLancamentosDiarios.Integrator.Bff/Conversores/SaldoTelaConversor.cs) |
| `LancamentosApiClient` | A única porta para a WebApi. Preserva status e `ProblemDetails` | [`Clients/LancamentosApiClient.cs`](../../../MeusLancamentosDiarios.Integrator.Bff/Clients/LancamentosApiClient.cs) |
| `RepassarTokenHandler` | Copia o `Authorization` da requisição de entrada para a de saída | [`Auth/RepassarTokenHandler.cs`](../../../MeusLancamentosDiarios.Integrator.Bff/Auth/RepassarTokenHandler.cs) |

### Events: o relay

```mermaid
flowchart TB
    gatilho(["Gatilho<br/>PeriodicTimer de 2 s no local,<br/>Cloud Scheduler a cada 1 min no GCP"])

    subgraph events["Events [Container: .NET 9 worker, Cloud Run Job no GCP]"]
        direction TB
        worker["RelayWorker<br/>[BackgroundService]<br/>loop contínuo ou ciclo único"]
        boot["DatabaseBootstrapper<br/>[esquema idempotente]"]
        prov["PubSubProvisionador<br/>[tópico e subscription, se CriarRecursos]"]
        ciclo["RelayLancamentos<br/>ExecutarCicloAsync, PublicarAsync,<br/>CalcularBackoff"]
        repo["LancamentoRepository<br/>[Dapper]"]
        pub["PubSubPublicador<br/>[PublisherClient com ordenação]"]
    end

    sql[("Banco Lancamentos<br/>[SQL Server]")]
    ps[["Pub/Sub<br/>lancamentos-registrados"]]

    gatilho --> worker
    worker --> boot
    worker --> prov
    worker --> ciclo
    ciclo -->|"reserva, marca, devolve"| repo
    ciclo --> pub
    repo --> sql
    boot --> sql
    pub -->|"Publish + ResumePublish"| ps
    prov -.->|"CreateTopic, CreateSubscription"| ps

    classDef componente fill:#85bbf0,stroke:#5d82a8,color:#000000
    classDef externo fill:#999999,stroke:#6b6b6b,color:#ffffff
    class worker,boot,prov,ciclo,repo,pub componente
    class gatilho,sql,ps externo
```

### Consumer

```mermaid
flowchart TB
    ps[["Pub/Sub<br/>subscription lancamentos-consolidacao"]]

    subgraph consumer["Consumer [Container: .NET 9 worker]"]
        direction TB
        worker["ConsolidacaoWorker<br/>[transporte: SubscriberClient,<br/>FlowControl de 20 mensagens]"]
        regra["ConsolidadorDeMensagem<br/>[regra, sem Pub/Sub]"]
        repo["LancamentoRepository<br/>[Dapper, vem do .Events]"]
        boot["DatabaseBootstrapper<br/>PubSubProvisionador"]
    end

    sql[("Banco Lancamentos<br/>[SQL Server]")]

    ps -->|"streaming pull, uma por vez por conta"| worker
    worker -->|"corpo + messageId"| regra
    regra -->|"Confirmar ou Devolver"| worker
    worker -->|"Ack ou Nack"| ps
    regra --> repo
    repo -->|"EmProcessamento, Consolidado + saldo"| sql
    boot --> sql

    classDef componente fill:#85bbf0,stroke:#5d82a8,color:#000000
    classDef externo fill:#999999,stroke:#6b6b6b,color:#ffffff
    class worker,regra,repo,boot componente
    class ps,sql externo
```

### Front-end

```mermaid
flowchart TB
    user(["Cliente ou Administrador"])

    subgraph spa["SPA [Container: Angular 20 standalone, PWA, servido por nginx]"]
        direction TB
        rotas["app.routes + guards<br/>exigeLogin, somenteConvidado"]
        t_saldo["SaldoConsolidadoComponent"]
        t_novo["NovoLancamentoComponent"]
        t_auth["Entrar, Cadastro, EsqueciSenha,<br/>RedefinirSenha, BotaoGoogle"]
        lserv["LancamentosService<br/>[signals, polling condicional]"]
        aserv["AuthService<br/>[sessão, uma renovação por vez]"]
        inter["autenticacaoInterceptor<br/>[Bearer; renova no 401]"]
        gis["google-identity.ts<br/>[carrega o script sob demanda]"]
        sw["Service worker<br/>[ngsw, só arquivos estáticos]"]
    end

    bff(["BFF<br/>[Container]"])
    google["Google Identity Services<br/>[Sistema externo]"]
    store[("localStorage<br/>lancamentos.sessao")]

    user --> rotas
    rotas --> t_saldo
    rotas --> t_novo
    rotas --> t_auth
    t_saldo --> lserv
    t_novo --> lserv
    t_auth --> aserv
    t_auth --> gis
    lserv --> inter
    aserv --> inter
    inter -->|"HTTPS/JSON"| bff
    aserv --> store
    gis -->|"script e ID token"| google

    classDef pessoa fill:#08427b,stroke:#052e56,color:#ffffff
    classDef container fill:#438dd5,stroke:#2e6295,color:#ffffff
    classDef componente fill:#85bbf0,stroke:#5d82a8,color:#000000
    classDef externo fill:#999999,stroke:#6b6b6b,color:#ffffff
    class user pessoa
    class bff container
    class rotas,t_saldo,t_novo,t_auth,lserv,aserv,inter,gis,sw componente
    class google,store externo
```

O `AuthService` lê a sessão do `localStorage` uma vez, ao abrir; a decisão e o preço estão em
[ADR-SW-14](03-adrs/ADR-SW-14-sessao-no-localstorage.md). O polling está em
[ADR-SW-11](03-adrs/ADR-SW-11-polling-condicional-no-front.md).

## Nível 4 — Código

Só as classes que carregam regra. DTOs e opções aparecem quando ajudam a ler a relação.

### CQRS na WebApi

```mermaid
classDiagram
    direction LR
    class ICommand~TResultado~ {
        <<interface>>
    }
    class IQuery~TResultado~ {
        <<interface>>
    }
    class IDispatcher {
        <<interface>>
        +EnviarAsync(comando, ct) Task~TResultado~
        +ConsultarAsync(consulta, ct) Task~TResultado~
    }
    class Dispatcher {
        -IServiceProvider provider
        -DespacharAsync(mensagem, handlerAberto, ct) Task~TResultado~
        -ValidarAsync(mensagem, ct) Task
    }
    class ICommandHandler {
        <<interface>>
        +HandleAsync(comando, ct) Task~TResultado~
    }
    class IQueryHandler {
        <<interface>>
        +HandleAsync(consulta, ct) Task~TResultado~
    }
    class CriarLancamentoCommand {
        <<record>>
        +string ContaId
        +TipoLancamentoEnum Tipo
        +decimal Valor
        +DateOnly DataLancamento
        +string Observacao
    }
    class CriarLancamentoValidator {
        +decimal ValorMaximo$
        +string PadraoConta$
    }
    class CriarLancamentoHandler {
        +TimeSpan LimitePublicacao$
        +HandleAsync(comando, ct) Task~CriarLancamentoResponse~
        +Montar(comando, registro)$ LancamentoEntity
        -TentarPublicarAsync(lancamento, ct) Task~StageLancamentoEnum~
    }
    class ObterSaldoQuery {
        <<record>>
        +string ContaId
        +int LimitePendentes
    }
    class ObterSaldoHandler {
        +HandleAsync(consulta, ct) Task~SaldoResponse~
    }
    class ProblemaException {
        +int Status
        +NaoAutorizado(titulo)$ ProblemaException
        +Conflito(titulo)$ ProblemaException
        +Invalido(titulo)$ ProblemaException
    }
    class ValidationExceptionHandler {
        +TryHandleAsync(contexto, excecao, ct) ValueTask~bool~
    }
    class ProblemaExceptionHandler {
        +TryHandleAsync(contexto, excecao, ct) ValueTask~bool~
    }

    IDispatcher <|.. Dispatcher
    ICommand <|.. CriarLancamentoCommand
    IQuery <|.. ObterSaldoQuery
    ICommandHandler <|.. CriarLancamentoHandler
    IQueryHandler <|.. ObterSaldoHandler
    Dispatcher ..> CriarLancamentoValidator : valida antes do handler
    Dispatcher ..> ICommandHandler : resolve pelo tipo
    Dispatcher ..> IQueryHandler : resolve pelo tipo
    CriarLancamentoHandler ..> CriarLancamentoCommand : trata
    ObterSaldoHandler ..> ObterSaldoQuery : trata
    ProblemaExceptionHandler ..> ProblemaException : traduz para HTTP
```

`ICommand<T>` e `IQuery<T>` moram no `.Common`, para o comando do `.Messages` ser a própria
mensagem que o BFF serializa. `ICommandHandler<TCommand, TResultado>` e
`IQueryHandler<TQuery, TResultado>` moram na WebApi: só ela trata.

### Persistência dos lançamentos

```mermaid
classDiagram
    direction LR
    class ILancamentoRepository {
        <<interface>>
        +InserirAsync(lancamento, ct) Task~long~
        +InserirLoteAsync(lancamentos, ct) Task
        +SemPredecessorPendenteAsync(contaId, sequencia, ct) Task~bool~
        +ReservarUmAsync(id, ct) Task~bool~
        +ReservarParaPublicacaoAsync(limite, reservaExpiraEm, ct) Task~IReadOnlyList~
        +MarcarEnfileiradoAsync(id, mensagemId, ct) Task
        +LiberarReservaAsync(id, ct) Task
        +DevolverParaFilaAsync(id, motivo, maxTentativas, proximaTentativaEm, ct) Task
        +IniciarProcessamentoAsync(id, ct) Task~LancamentoEntity~
        +ConsolidarAsync(lancamento, ct) Task~bool~
        +DevolverParaProcessamentoAsync(id, motivo, maxTentativas, ct) Task
        +ListarPendentesAsync(contaId, limite, ct) Task~IReadOnlyList~
        +ObterSaldoAsync(contaId, ct) Task~SaldoAtual~
        +ListarContasAsync(ct) Task~IReadOnlyList~
        +ObterPorIdAsync(id, ct) Task~LancamentoEntity~
    }
    class LancamentoRepository {
        -int TentativasSequencia$
        -EstagiosPendentes$
        -EstagiosBloqueantes$
    }
    class IDbConnectionFactory {
        <<interface>>
        +AbrirAsync(ct) Task~SqlConnection~
        +AbrirNoServidorAsync(ct) Task~SqlConnection~
        +int TimeoutComandoSegundos
    }
    class SqlServerConnectionFactory
    class BancoOptions {
        +string ConnectionString
        +int TimeoutComandoSegundos
        +bool CriarBanco
        +bool Valida
    }
    class LancamentoEntity {
        +Guid Id
        +string ContaId
        +long Sequencia
        +TipoLancamentoEnum Tipo
        +long ValorCentavos
        +DateOnly DataLancamento
        +DateTimeOffset DataRegistro
        +StageLancamentoEnum Stage
        +int TentativasEnvio
        +int TentativasProcessamento
        +DateTimeOffset ProximaTentativaEm
        +string Erro
        +long DeltaCentavos
    }
    class SaldoAtual {
        <<record>>
        +long SaldoConsolidadoCentavos
        +long UltimaSequenciaConsolidada
        +long CreditosPendentesCentavos
        +long DebitosPendentesCentavos
        +int QuantidadePendentes
        +long SaldoProjetadoCentavos
        +bool ConsolidacaoEmAndamento
    }
    class StageLancamentoEnum {
        <<enumeration>>
        Cadastrado = 1
        Lido = 2
        Enfileirado = 3
        EmProcessamento = 4
        Consolidado = 5
        Erro = 6
    }
    class DatabaseBootstrapper {
        +GarantirEsquemaAsync(ct) Task
    }

    ILancamentoRepository <|.. LancamentoRepository
    IDbConnectionFactory <|.. SqlServerConnectionFactory
    LancamentoRepository --> IDbConnectionFactory
    SqlServerConnectionFactory --> BancoOptions
    DatabaseBootstrapper --> IDbConnectionFactory
    LancamentoRepository ..> LancamentoEntity : lê e devolve
    LancamentoRepository ..> SaldoAtual : monta
    LancamentoEntity --> StageLancamentoEnum
```

`EstagiosBloqueantes` (`Cadastrado`, `Lido`, `Erro`) é o que impede publicar o sucessor de um
pendente. `EstagiosPendentes` (`Cadastrado` a `EmProcessamento`) é o que a tela chama de
pendente.

### Publicação e consolidação

```mermaid
classDiagram
    direction LR
    class RelayLancamentos {
        +PublicarAsync(lancamento, ct) Task~bool~
        +ExecutarCicloAsync(ct) Task~int~
        +CalcularBackoff(tentativa) TimeSpan
    }
    class IPublicadorEventos {
        <<interface>>
        +PublicarAsync(evento, ct) Task~string~
    }
    class PubSubPublicador {
        -PublisherClient cliente
        -SemaphoreSlim lock
        +DisposeAsync() ValueTask
    }
    class RelayWorker {
        #ExecuteAsync(stoppingToken) Task
        -ExecutarCicloUnicoAsync(ct) Task
        -ExecutarEmLoopAsync(ct) Task
    }
    class BackgroundService {
        <<abstract>>
    }
    class RelayOptions {
        +TimeSpan Intervalo
        +int TamanhoLote
        +int MaxTentativas
        +TimeSpan BackoffMaximo
        +TimeSpan ReservaExpiraEm
        +bool LoopContinuo
    }
    class LancamentoRegistradoEvent {
        <<record>>
        +Guid LancamentoId
        +string ContaId
        +long Sequencia
        +TipoLancamentoEnum Tipo
        +long ValorCentavos
        +DateOnly DataLancamento
        +DateTimeOffset DataRegistro
        +string Observacao
    }
    class ConsolidacaoWorker {
        #ExecuteAsync(stoppingToken) Task
    }
    class ConsolidadorDeMensagem {
        +ProcessarAsync(corpo, mensagemId, ct) Task~RespostaAoBrokerEnum~
    }
    class RespostaAoBrokerEnum {
        <<enumeration>>
        Confirmar
        Devolver
    }
    class ILancamentoRepository {
        <<interface>>
    }

    IPublicadorEventos <|.. PubSubPublicador
    BackgroundService <|-- RelayWorker
    BackgroundService <|-- ConsolidacaoWorker
    RelayWorker --> RelayLancamentos : chama o ciclo
    RelayLancamentos --> IPublicadorEventos
    RelayLancamentos --> ILancamentoRepository
    RelayLancamentos --> RelayOptions
    RelayLancamentos ..> LancamentoRegistradoEvent : monta
    ConsolidacaoWorker --> ConsolidadorDeMensagem
    ConsolidadorDeMensagem --> ILancamentoRepository
    ConsolidadorDeMensagem ..> LancamentoRegistradoEvent : desserializa
    ConsolidadorDeMensagem ..> RespostaAoBrokerEnum : devolve
```

A WebApi usa o mesmo `RelayLancamentos.PublicarAsync` na publicação imediata. Por isso uma
falha ali conta tentativa e aplica o backoff exatamente como no relay.

### Autenticação e posse

```mermaid
classDiagram
    direction LR
    class AberturaDeSessao {
        +AbrirAsync(usuario, ct) Task~SessaoResponse~
    }
    class EmissorDeTokens {
        +CriarAcesso(usuario) TokenDeAcesso
    }
    class SegredoOpaco {
        <<record>>
        +string Texto
        +Gerar()$ SegredoOpaco
        +Hashear(texto)$ bytes
    }
    class CadastroDeCliente {
        +CadastrarAsync(email, nome, senha, googleId, ct) Task~UsuarioEntity~
        +SortearConta()$ string
    }
    class IUsuarioRepository {
        <<interface>>
        +ObterPorEmailAsync(email, ct) Task~UsuarioEntity~
        +InserirAsync(usuario, ct) Task~ResultadoInsercaoUsuarioEnum~
        +GravarTokenAsync(usuarioId, finalidade, hash, expiraEm, ct) Task
        +ConsumirTokenAsync(hash, finalidade, ct) Task~ConsumoDeToken~
        +RevogarTokensAsync(usuarioId, finalidade, ct) Task
    }
    class LoginHandler
    class LoginGoogleHandler
    class RenovarSessaoHandler
    class RedefinirSenhaHandler
    class IValidadorGoogle {
        <<interface>>
        +ValidarAsync(credencial, ct) Task~IdentidadeGoogle~
    }
    class PosseDaContaFilter {
        +InvokeAsync(contexto, proximo) ValueTask~object~
    }
    class IReferenciaConta {
        <<interface>>
        +string ContaId
    }
    class UsuarioAutenticadoExtensions {
        +ContaId(usuario)$ string
        +IsAdmin(usuario)$ bool
        +PodeAcessarConta(usuario, contaId)$ bool
    }

    LoginHandler --> IUsuarioRepository
    LoginHandler --> AberturaDeSessao
    LoginGoogleHandler --> IValidadorGoogle
    LoginGoogleHandler --> CadastroDeCliente
    LoginGoogleHandler --> AberturaDeSessao
    RenovarSessaoHandler --> IUsuarioRepository
    RenovarSessaoHandler --> AberturaDeSessao
    RedefinirSenhaHandler --> IUsuarioRepository
    AberturaDeSessao --> EmissorDeTokens
    AberturaDeSessao --> IUsuarioRepository
    AberturaDeSessao ..> SegredoOpaco : gera a renovação
    CadastroDeCliente --> IUsuarioRepository
    PosseDaContaFilter ..> IReferenciaConta : lê a conta do corpo e da resposta
    PosseDaContaFilter ..> UsuarioAutenticadoExtensions : aplica a regra
```

`SegredoOpaco` guarda o texto e o hash SHA-256 (`byte[]`): o texto vai ao cliente, o banco só
vê o hash.

### BFF

```mermaid
classDiagram
    direction LR
    class ISaldoService {
        <<interface>>
        +ObterAsync(contaId, ct) Task~RespostaApi~
        +ListarContasAsync(ct) Task~RespostaApi~
    }
    class ILancamentoService {
        <<interface>>
        +IncluirAsync(pedido, usuario, ct) Task~RespostaApi~
    }
    class IAutenticacaoService {
        <<interface>>
        +EntrarAsync(comando, ct) Task~RespostaRepassada~
        +RenovarAsync(comando, ct) Task~RespostaRepassada~
    }
    class ILancamentosApiClient {
        <<interface>>
        +ObterSaldoAsync(contaId, ct) Task~RespostaApi~
        +ListarContasAsync(ct) Task~RespostaApi~
        +CriarLancamentoAsync(comando, ct) Task~RespostaApi~
        +RepassarAsync(metodo, caminho, corpo, ct) Task~RespostaRepassada~
    }
    class IConversor {
        <<interface>>
        +Converter(origem) TDestino
    }
    class RespostaApi~T~ {
        <<record>>
        +bool Sucesso
        +T Valor
        +int StatusCode
        +JsonElement Problema
        +Mapear(converter) RespostaApi
    }
    class RepassarTokenHandler {
        #SendAsync(request, ct) Task~HttpResponseMessage~
    }
    class SaldoService
    class LancamentoService
    class AutenticacaoService
    class LancamentosApiClient
    class SaldoTelaConversor {
        +int IntervaloPollingMs$
    }

    ISaldoService <|.. SaldoService
    ILancamentoService <|.. LancamentoService
    IAutenticacaoService <|.. AutenticacaoService
    ILancamentosApiClient <|.. LancamentosApiClient
    IConversor <|.. SaldoTelaConversor
    SaldoService --> ILancamentosApiClient
    SaldoService --> IConversor
    LancamentoService --> ILancamentosApiClient
    LancamentoService --> IConversor
    AutenticacaoService --> ILancamentosApiClient
    LancamentosApiClient ..> RespostaApi : devolve
    LancamentosApiClient ..> RepassarTokenHandler : pipeline do HttpClient
```

O conversor é `IConversor<TOrigem, TDestino>`. Os genéricos de dois parâmetros ficam fora do
diagrama porque o Mermaid não os desenha.

## Sequências dos fluxos internos críticos

### 1. Registrar um lançamento: caminho feliz

```mermaid
sequenceDiagram
    autonumber
    actor U as Cliente
    participant F as Front
    participant B as BFF
    participant W as WebApi
    participant DB as SQL Server
    participant PS as Pub/Sub
    U->>F: confirma tipo, valor, data e observação
    F->>B: POST /api/lancamentos com Bearer
    B->>B: valida o JWT, a policy ClienteOuAdmin e a posse
    B->>B: sem conta no corpo, usa o claim conta
    B->>W: POST /lancamentos com o mesmo Bearer
    W->>W: valida o JWT, a policy e a posse de novo
    W->>W: CriarLancamentoValidator
    W->>DB: BEGIN, MAX(Sequencia)+1 da conta com UPDLOCK e HOLDLOCK
    W->>DB: INSERT com Stage = Cadastrado, COMMIT
    W->>DB: existe anterior da conta em Cadastrado, Lido ou Erro?
    DB-->>W: não
    W->>DB: UPDATE Stage = Lido WHERE Id = id AND Stage = Cadastrado
    DB-->>W: 1 linha
    W->>PS: Publish com orderingKey = conta
    PS-->>W: messageId
    W->>DB: UPDATE Stage = Enfileirado WHERE Stage = Lido
    W-->>B: 201 CriarLancamentoResponse com stage Enfileirado
    B->>B: LancamentoCriadoTelaConversor
    B-->>F: 201 LancamentoCriadoTela
    F->>F: mostra a confirmação e recarrega o saldo
```

A sequência é atribuída dentro da transação do `INSERT`. Se dois `INSERT` da mesma conta
colidirem, o índice único `(ContaId, Sequencia)` recusa um deles, que recalcula e tenta de
novo, até 5 vezes.

### 2. Registrar um lançamento: quando a publicação imediata não acontece

```mermaid
sequenceDiagram
    participant W as WebApi
    participant DB as SQL Server
    participant PS as Pub/Sub
    participant R as Relay
    Note over W,DB: lançamento já gravado como Cadastrado
    alt há anterior pendente na conta
        W-->>W: responde 201 com stage Cadastrado
        Note over R: o relay publica a conta em ordem no próximo ciclo
    else o relay reservou a linha antes
        W->>DB: UPDATE Stage = Lido WHERE Stage = Cadastrado
        DB-->>W: 0 linhas
        W-->>W: responde 201 com stage Lido
    else o broker passou do teto de 2 s
        W->>PS: Publish
        W-->>W: para de esperar e responde 201 com stage Lido
        PS-->>W: confirma depois, e a linha vai para Enfileirado
        Note over W,R: travando de vez, a reserva vence em 1 min e o relay recolhe
    else o broker falhou
        W->>PS: Publish
        PS--xW: erro
        W->>PS: ResumePublish da conta
        W->>DB: volta para Cadastrado, TentativasEnvio+1 e ProximaTentativaEm
        W-->>W: responde 201 com stage Cadastrado
    end
```

Em nenhum dos casos a WebApi devolve erro depois do commit: o usuário repetiria o lançamento
e criaria a duplicata.

### 3. Ciclo do relay com falha numa conta

```mermaid
sequenceDiagram
    autonumber
    participant T as Gatilho
    participant R as RelayLancamentos
    participant DB as SQL Server
    participant PS as Pub/Sub
    T->>R: ExecutarCicloAsync
    R->>DB: WITH candidatos TOP 50 com UPDLOCK, READPAST, ROWLOCK, UPDATE para Lido, OUTPUT
    Note over R,DB: candidato é Cadastrado ou Lido com reserva vencida, fora do backoff e sem anterior bloqueante
    DB-->>R: linhas já em Lido, por conta e sequência
    loop cada lançamento reservado
        alt a conta já falhou neste ciclo
            R->>DB: Lido para Cadastrado, sem gastar tentativa
        else
            R->>PS: Publish com orderingKey = conta
            alt publicou
                PS-->>R: messageId
                R->>DB: Lido para Enfileirado
            else falhou
                PS--xR: erro
                R->>PS: ResumePublish da conta
                R->>DB: Lido para Cadastrado com espera de 2, 4, 8 ou 16 s, ou Erro na 5ª falha
                R->>R: marca a conta como travada no ciclo
            end
        end
    end
```

Como a reserva nunca traz um lançamento com anterior bloqueante, cada conta tem no máximo um
lançamento por ciclo. É o que limita o relay a 50 lançamentos por rodada e uma conta por vez;
a alternativa em lote está medida em [capacidade-do-relay.md](../../capacidade-do-relay.md).

### 4. Ciclo único no Cloud Run Job

```mermaid
sequenceDiagram
    participant CS as Cloud Scheduler
    participant RJ as Cloud Run Job
    participant RW as RelayWorker
    participant R as RelayLancamentos
    CS->>RJ: POST jobs/lancamentos-relay:run com OAuth da service account
    RJ->>RW: sobe o container com LoopContinuo = false
    RW->>RW: garante o esquema e, se configurado, tópico e subscription
    RW->>R: ExecutarCicloAsync
    alt o ciclo terminou
        R-->>RW: quantidade publicada
        RW->>RJ: StopApplication, código de saída 0
    else exceção
        RW->>RJ: StopApplication, código de saída 1
        Note over RJ: o Job reexecuta até 3 vezes dentro do timeout de 50 s
    end
```

Sair do `ExecuteAsync` não encerra o host; sem o `StopApplication` o Job ficaria parado até
o timeout e contaria como falha.

### 5. Consolidação no Consumer

```mermaid
sequenceDiagram
    autonumber
    participant PS as Pub/Sub
    participant CW as ConsolidacaoWorker
    participant C as ConsolidadorDeMensagem
    participant DB as SQL Server
    PS->>CW: mensagem, uma por vez em cada conta
    CW->>C: ProcessarAsync com corpo e messageId
    alt corpo inválido ou vazio
        C-->>CW: Confirmar, a reentrega não conserta
    else corpo válido
        C->>DB: UPDATE para EmProcessamento, TentativasProcessamento+1, OUTPUT, WHERE Stage IN Lido ou Enfileirado
        alt nenhuma linha
            C-->>CW: Confirmar, mensagem repetida ou lançamento já finalizado
        else linha devolvida
            C->>DB: BEGIN, UPDATE para Consolidado WHERE Stage = EmProcessamento
            C->>DB: UPDATE SaldoConsolidado com UPDLOCK e SERIALIZABLE, ou INSERT, COMMIT
            alt consolidou
                C-->>CW: Confirmar
            else exceção
                C->>DB: volta para Enfileirado, ou Erro na 5ª tentativa
                C-->>CW: Devolver
            end
        end
    end
    CW-->>PS: Ack para Confirmar, Nack para Devolver
```

O valor somado vem da linha no banco, não do corpo da mensagem: o corpo só aponta o
`lancamentoId`. A reserva (`EmProcessamento`) e a consolidação são dois commits; uma queda
entre os dois deixa o lançamento fora do saldo
([dívida D2](01-sad-app.md#11-dívida-técnica-conhecida)).

### 6. Login, renovação e detecção de reuso

```mermaid
sequenceDiagram
    autonumber
    participant F as Front
    participant B as BFF
    participant W as WebApi
    participant DB as SQL Server
    F->>B: POST /api/auth/login
    B->>W: POST /auth/login, repasse sem transformação
    W->>DB: usuário pelo e-mail normalizado
    W->>W: PBKDF2, contra um hash fictício se o e-mail não existe
    W->>DB: INSERT do SHA-256 do token de renovação, vence em 7 dias
    W-->>F: 200 com JWT de 15 min e token de renovação, via BFF
    F->>F: guarda a sessão no localStorage
    Note over F,W: 15 minutos depois
    F->>B: GET /api/saldo/ABC1234 com JWT vencido
    B-->>F: 401
    F->>B: POST /api/auth/renovar, uma renovação por vez
    B->>W: POST /auth/renovar
    W->>DB: UPDATE ConsumidoEm OUTPUT UsuarioId WHERE hash não consumido e não vencido
    alt token válido
        W->>DB: relê o usuário, com a role atual
        W->>DB: INSERT do novo token de renovação
        W-->>F: 200 com o par novo, via BFF
        F->>B: repete o GET com o JWT novo
    else token já consumido
        W->>DB: revoga todos os tokens de renovação do usuário
        W-->>F: 401, via BFF
        F->>F: descarta a sessão e vai para /entrar
    end
```

### 7. Tela de saldo com polling condicional

```mermaid
sequenceDiagram
    participant T as SaldoConsolidadoComponent
    participant L as LancamentosService
    participant B as BFF
    participant W as WebApi
    T->>L: acompanhar ao abrir a tela
    loop enquanto intervaloPollingMs for maior que zero
        L->>B: GET /api/saldo/ABC1234
        B->>W: GET /saldo/ABC1234
        W-->>B: SaldoResponse com consolidado e pendentes
        B-->>L: SaldoTela com intervaloPollingMs 2000 se há pendente, 0 se não
        L->>L: atualiza os signals e agenda o próximo setTimeout
    end
    Note over L: sem pendente, nenhuma requisição até o usuário lançar ou recarregar
    T->>L: pararAcompanhamento ao sair da tela
```

Se a consulta falha, o service mantém o último saldo e liga o aviso "Sem conexão". Uma tela
aberta antes de um lançamento feito em outro lugar só volta a consultar quando é recarregada.

### 8. Esqueci a senha e redefinição

```mermaid
sequenceDiagram
    autonumber
    participant F as Front
    participant B as BFF
    participant W as WebApi
    participant DB as SQL Server
    participant M as SMTP
    F->>B: POST /api/auth/esqueci-senha
    B->>W: POST /auth/esqueci-senha
    W->>DB: usuário pelo e-mail
    alt sem cadastro
        W-->>F: 202, via BFF
    else com cadastro
        W->>DB: revoga links anteriores
        W->>DB: INSERT do hash do link, vence em 30 min
        W->>M: e-mail com o link /redefinir-senha e o token
        W-->>F: 202, via BFF, mesmo se o SMTP falhar
    end
    Note over F: o usuário abre o link do e-mail
    F->>B: POST /api/auth/redefinir-senha com token e nova senha
    B->>W: POST /auth/redefinir-senha
    W->>W: valida a senha nova antes de gastar o token
    W->>DB: consome o token com UPDATE e OUTPUT
    W->>DB: grava o hash PBKDF2 da senha nova
    W->>DB: revoga todos os tokens de renovação do usuário
    W-->>F: 204, via BFF
```

O envio acontece dentro da requisição, então o tempo de resposta pode revelar quem tem
cadastro ([RNF-20](../../requisitos-nao-funcionais.md#segurança)).
