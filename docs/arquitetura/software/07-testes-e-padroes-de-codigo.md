# Estratégia de testes arquiteturais e padrões de código

Como provar que o aplicativo faz o que promete em cada camada, como manter a arquitetura de
pé enquanto o código cresce, e as regras de código que o repositório já segue. Retrato do
commit `45bcd17`, em 07/10/2026.

## 1. Situação atual

Detalhada em [cobertura-de-testes.md](../../cobertura-de-testes.md):

- **298 testes de unidade**, todos passando, em cerca de 3 s; 40,2% das linhas e 39,2% dos
  ramos.
- Cobertura completa onde a regra é orquestração: `RelayLancamentos`,
  `ConsolidadorDeMensagem`, `Dispatcher`, `PosseDaContaFilter`, validadores, conversores.
- **Sem teste:** o SQL (`LancamentoRepository`, 0 de 260 linhas), o Pub/Sub, os endpoints
  HTTP, os `Program.cs` e o front-end.
- **Sem CI**, e o `dotnet test` da solução sai com código 1 por causa do `.TestSupport`.

O que mais preocupa é a primeira lacuna: ordem, idempotência e retentativa moram no SQL, e
nos testes o repositório só existe como mock.

## 2. Estratégia em camadas

Cada camada prova uma coisa que a de baixo não consegue provar.

| Camada | Prova | Ferramenta | Roda | Hoje |
|---|---|---|---|---|
| Unidade | Regra de cada classe, com dublês | xUnit, Moq, NBuilder, FluentAssertions | todo commit | 298 testes |
| Arquitetura | Quem pode depender de quem; onde cada tipo mora; nomes | NetArchTest.Rules | todo commit | — |
| Integração de dados | O SQL faz o que o repositório promete, inclusive sob concorrência | Testcontainers (SQL Server) | todo commit | — |
| Integração HTTP | Autenticação, policies, posse e `ProblemDetails` no pipeline de verdade | `WebApplicationFactory` | todo commit | — |
| Integração de mensageria | Ordering key, ack, nack e reentrega | Testcontainers (emulador do Pub/Sub) | todo commit | — |
| Contrato | A API e o evento não mudaram sem querer | OpenAPI gerado no build + `oasdiff`; snapshot do JSON do evento; AsyncAPI validado | todo commit | — |
| Front-end | Interceptor, sessão, polling, máscara de moeda, guards | Jasmine e Karma (já configurados) | todo commit | — |
| Ponta a ponta | Do clique ao saldo consolidado | Playwright contra o `docker compose` | todo merge na `main` | manual |
| Resiliência | Lançar sem a consolidação; recuperar depois | Script com `docker compose stop` e `pause` | diário | manual |
| Carga | 50 consultas por segundo com até 5% de perda; relay e Consumer sob volume | `carga_saldo.py`, `RelayBench`, k6 | antes de cada release | manual |

A ordem de entrada é a do retorno, a mesma do [plano da cobertura](../../cobertura-de-testes.md#o-que-não-tem-teste):
consertar o `.TestSupport`, integração do repositório, `WebApplicationFactory`, depois CI.

## 3. Testes de arquitetura

Regras que hoje só existem no README e na revisão de código. Com NetArchTest, viram teste que
quebra o build.

| Regra | Por quê |
|---|---|
| `.Common` não depende de `Microsoft.AspNetCore`, Dapper nem `Google.Cloud` | Events e Consumer rodam na imagem `runtime:9.0` |
| `.Messages` não depende de `.Events`, Dapper, `Google.Cloud` nem `Microsoft.AspNetCore` | É contrato publicado; não pode arrastar infraestrutura |
| O BFF não depende de `.Events`, Dapper, `SqlClient` nem Pub/Sub | O BFF não acessa banco nem broker |
| Os endpoints da WebApi não dependem de repositórios | O endpoint só faz binding e chama o dispatcher |
| `ConsolidadorDeMensagem` não depende de `Google.Cloud.PubSub` | A regra é separada do transporte ([ADR-SW-10](03-adrs/ADR-SW-10-consumer-com-regra-isolada-do-transporte.md)) |
| Handlers terminam em `Handler` e moram em `…WebApi.Features` | Convenção de pastas |
| Interfaces moram em namespaces `….Contracts` (fora do `.Messages`) | Convenção de pastas |
| Classes de produção são `sealed` | Convenção do repositório |
| Enums terminam em `Enum`; opções terminam em `Options` | Convenção de nomes |

Exemplo, num projeto novo `tests/MeusLancamentosDiarios.Integrator.Arquitetura.Tests`:

```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

public sealed class RegrasDeDependencia
{
    private static readonly Assembly Common = typeof(DinheiroHelper).Assembly;
    private static readonly Assembly Messages = typeof(CriarLancamentoCommand).Assembly;
    private static readonly Assembly Bff = typeof(SaldoTelaConversor).Assembly;
    private static readonly Assembly WebApi = typeof(Dispatcher).Assembly;
    private static readonly Assembly Consumer = typeof(ConsolidadorDeMensagem).Assembly;

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Dapper")]
    [InlineData("Google.Cloud")]
    public void Common_nao_depende_de(string dependencia) =>
        Types.InAssembly(Common)
            .ShouldNot().HaveDependencyOn(dependencia)
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Messages_nao_arrasta_infraestrutura() =>
        Types.InAssembly(Messages)
            .ShouldNot().HaveDependencyOnAny(
                "MeusLancamentosDiarios.Integrator.Events", "Dapper", "Google.Cloud", "Microsoft.AspNetCore")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Bff_nao_acessa_banco_nem_broker() =>
        Types.InAssembly(Bff)
            .ShouldNot().HaveDependencyOnAny(
                "MeusLancamentosDiarios.Integrator.Events", "Dapper", "Microsoft.Data.SqlClient", "Google.Cloud.PubSub")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Endpoints_nao_conhecem_repositorios() =>
        Types.InAssembly(WebApi)
            .That().ResideInNamespace("MeusLancamentosDiarios.Integrator.WebApi.Endpoints")
            .ShouldNot().HaveDependencyOnAny(
                "MeusLancamentosDiarios.Integrator.Events.Data",
                "MeusLancamentosDiarios.Integrator.WebApi.Auth.Data")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Regra_de_consolidacao_nao_conhece_o_PubSub() =>
        Types.InAssembly(Consumer)
            .That().HaveName("ConsolidadorDeMensagem")
            .ShouldNot().HaveDependencyOn("Google.Cloud.PubSub")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Handlers_moram_em_Features_e_sao_sealed() =>
        Types.InAssembly(WebApi)
            .That().HaveNameEndingWith("Handler")
            .And().AreNotInterfaces()
            .And().DoNotHaveNameEndingWith("ExceptionHandler")
            .Should().ResideInNamespaceStartingWith("MeusLancamentosDiarios.Integrator.WebApi.Features")
            .And().BeSealed()
            .GetResult().IsSuccessful.Should().BeTrue();
}
```

As regras de dependência valem também como teste de que o grafo de projetos da
[visão de módulos](01-sad-app.md#5-visão-de-módulos) continua verdadeiro.

## 4. Testes de integração

### 4.1 Repositório contra SQL Server real

Com Testcontainers (`Testcontainers.MsSql`), a mesma imagem do compose e o mesmo
`DatabaseBootstrapper` da aplicação: o teste usa o esquema real.

```csharp
public sealed class BancoDeTeste : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public IDbConnectionFactory Conexoes { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        var opcoes = Options.Create(new BancoOptions
        {
            ConnectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
                { InitialCatalog = "LancamentosTeste" }.ToString(),
            TimeoutComandoSegundos = 30,
            CriarBanco = true
        });

        Conexoes = new SqlServerConnectionFactory(opcoes);
        await new DatabaseBootstrapper(Conexoes, opcoes, NullLogger<DatabaseBootstrapper>.Instance)
            .GarantirEsquemaAsync();
    }

    public Task DisposeAsync() => _sql.DisposeAsync().AsTask();
}
```

Um container por classe de teste (`IClassFixture<BancoDeTeste>`), não por `[Fact]`.

| Cenário | Prova |
|---|---|
| 20 inserções concorrentes na mesma conta | Sequências de 1 a 20, sem buraco nem repetição |
| Inserções concorrentes em contas diferentes | Não esperam umas pelas outras (falha hoje, pelo defeito de tipo; passa depois da correção) |
| Reserva com o anterior em `Cadastrado`, `Lido` ou `Erro` | O sucessor não vem |
| Reserva com `Lido` vencido e com `ProximaTentativaEm` no futuro | O vencido vem; o do futuro não |
| Duas reservas concorrentes | Nenhuma linha reservada pelas duas |
| `ConsolidarAsync` duas vezes com a mesma linha | O saldo soma uma vez |
| `DevolverParaFilaAsync` na 5ª tentativa | A linha vai para `Erro` |
| Lote com colisão de sequência | Tudo ou nada |
| `ConsumirTokenAsync` concorrente com o mesmo hash | Um `Valido`, o outro `JaConsumido` |
| Cadastro com conta que tem histórico | `ContaEmUso` |
| Bootstrapper rodado duas vezes | Nenhum erro, nenhum objeto duplicado |

### 4.2 APIs com `WebApplicationFactory`

Sobe a WebApi e o BFF em memória, com JWT de verdade emitido por um `EmissorDeTokens` de
teste e o banco do Testcontainers.

| Cenário | Resposta esperada |
|---|---|
| Sem token em rota protegida | 401 |
| Cliente pedindo saldo de outra conta, pela rota | 403 |
| Cliente lançando em outra conta, pelo corpo | 403 |
| Cliente lendo `GET /lancamentos/{id}` de outra conta | 403, conferido na saída |
| Cliente em `POST /lancamentos/lote` | 403 |
| Endpoint novo sem policy | 401 sem token (fallback) |
| `/health` sem token | 200 |
| Validação | 400 com `errors` por propriedade |
| Swagger em `Production` sem `Swagger:Habilitado` | 404 |
| BFF repassa o Bearer à WebApi | A WebApi recebe o mesmo `Authorization` |

### 4.3 Mensageria com o emulador

Testcontainers com a imagem do emulador do compose
(`gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators`), `PUBSUB_EMULATOR_HOST`
apontando para o container, e o `PubSubProvisionador` criando tópico e subscription com
ordenação. Cenários: duas mensagens da mesma conta chegam em ordem; um nack faz a mesma
mensagem voltar antes da próxima da conta; contas diferentes chegam em paralelo.

## 5. Testes de contrato

| Contrato | Como | Falha quando |
|---|---|---|
| API da WebApi e do BFF | Gerar o OpenAPI no build (`Microsoft.Extensions.ApiDescription.Server`) e comparar com o arquivo em [`contratos/`](../contratos) usando `oasdiff breaking base.json novo.json --fail-on ERR` | Campo removido, tipo mudado, rota removida, status de sucesso trocado |
| Evento | Teste de snapshot: serializar um `LancamentoRegistradoEvent` fixo com `EventosJson.Options` e comparar com o JSON aprovado | Nome de campo, formato de data ou enum mudou |
| Evento, leitor tolerante | Desserializar um JSON com um campo a mais | O Consumer passou a recusar campo desconhecido |
| AsyncAPI | `asyncapi validate docs/arquitetura/contratos/asyncapi-lancamentos.v1.yaml` | O documento ficou inválido |
| BFF × WebApi | Já garantido em compilação pelo pacote `.Messages` | — |

**Gerar o OpenAPI no build exige um cuidado.** O gerador executa o `Program.cs` até o
`app.Run()`, e a WebApi garante o esquema do banco antes disso. As duas chamadas de
bootstrap precisam ser puladas quando o processo é o gerador:

```csharp
var gerandoOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

if (!gerandoOpenApi)
{
    await app.Services.GetRequiredService<DatabaseBootstrapper>().GarantirEsquemaAsync();
    await app.Services.GetRequiredService<AuthBootstrapper>().GarantirAsync();
}
```

Antes de ligar o `oasdiff`, corrigir as três divergências entre o documento gerado e o
comportamento real ([04 · Contratos](04-contratos-internos-e-apis.md#3-api-da-webapi)), senão
o contrato aprovado já nasce errado.

## 6. Ponta a ponta, resiliência e carga

**Ponta a ponta (Playwright, contra o `docker compose`):**

1. Entrar como `cliente@lancamentos.local`, lançar um crédito, ver o pendente e, em poucos
   segundos, o saldo consolidado com o valor certo.
2. Entrar como `admin@lancamentos.local`, trocar de conta, ver o saldo de `XYZ9999`.
3. Esqueci a senha: pedir o link, ler o e-mail na API do Mailpit, redefinir e entrar.
4. Sessão vencida: avançar o tempo, ver a renovação silenciosa.

**Resiliência**, automatizando o roteiro de
[RNF-01](../../requisitos-nao-funcionais.md#como-reproduzir):

| Cenário | Passos | Aceite |
|---|---|---|
| Consolidação parada | `docker compose stop consumer events`, lançar 20, `start` | 20 respostas 201; tudo consolidado em até 10 s depois da volta |
| Broker pausado | `docker compose pause pubsub` por 20 s, lançar 5 | O primeiro em até 2,1 s, os outros em menos de 100 ms; consolidado depois do `unpause` |
| Broker fora por mais de 30 s | `pause` por 60 s | Hoje: a conta vai para `Erro`. Serve de teste de regressão quando o `Erro` deixar de ser terminal |

**Carga:**

| Teste | Ferramenta | Aceite |
|---|---|---|
| 50 consultas por segundo por 60 s | `python tools/carga/carga_saldo.py 50 60` | Perda ≤ 5% (o requisito); p95 a definir no ambiente-alvo |
| Carga mista, leitura e escrita, base de milhões de linhas | k6, com base semeada por SQL num banco descartável | Sem varredura no plano; p95 estável com a base crescendo |
| Relay em rodada cheia | `tools/RelayBench` | Os números de [capacidade-do-relay.md](../../capacidade-do-relay.md) |

## 7. Padrões de código

### 7.1 Estrutura

```
<Projeto>/
├── <Área>/                 código da área
│   ├── Contracts/          interfaces da área, com namespace próprio
│   └── <Tipo>.cs           um tipo por arquivo
├── Features/<Área>/Commands|Queries/<Caso>/   só na WebApi: Handler, Validator, Query
├── Endpoints/              só nas APIs: Map<Área>
└── Program.cs              composição
```

- Interface em `Contracts/`, ao lado da implementação: `Cqrs/Contracts/IDispatcher.cs` junto
  de `Cqrs/Dispatcher.cs`. A exceção é o `.Messages`, que é todo ele contrato.
- `Bff/Contracts/` é outra coisa: os payloads da tela.
- Testes espelham o projeto: `tests/<Projeto>.Tests/<mesma pasta>/<Classe>Tests.cs`.

### 7.2 Nomes

| Sufixo | Para | Exemplo |
|---|---|---|
| `Entity` | Linha do banco | `LancamentoEntity` |
| `Command`, `Query` | Mensagem do CQRS | `CriarLancamentoCommand`, `ObterSaldoQuery` |
| `Handler`, `Validator` | Caso de uso e sua validação | `CriarLancamentoHandler` |
| `Response` | O que a WebApi devolve | `SaldoResponse` |
| `Request` | O que o BFF recebe da tela | `NovoLancamentoRequest` |
| `Tela` | O que o BFF devolve à tela | `SaldoTela` |
| `Service` | Camada de serviço do BFF | `SaldoService` |
| `Conversor` | De-para entre dois contratos | `SaldoTelaConversor` |
| `Helper` | Funções puras compartilhadas | `DinheiroHelper` |
| `Enum` | Todo enum | `StageLancamentoEnum` |
| `Options` | Seção do appsettings | `RelayOptions` |
| `Repository` | Acesso a dados | `UsuarioRepository` |
| `Bootstrapper` | Algo garantido na subida | `AuthBootstrapper` |

O domínio é nomeado em português (`Lancamento`, `Conta`, `Saldo`); termos técnicos
consagrados ficam em inglês (`Handler`, `Repository`, `Options`).

### 7.3 .NET

- Classes `sealed`; construtor primário para dependências.
- Mensagens e DTOs como `record` com `required` e `init`.
- `CancellationToken` como último parâmetro, sempre repassado. `CancellationToken.None` só em
  compensação, com o porquê no comentário (o cancelamento pode ser a causa da falha).
- Tempo só pelo `TimeProvider`. Id novo com `Guid.CreateVersion7()`.
- Configuração só por `*Options`, sem padrão no código, com `Valida` e `ValidateOnStart`
  ([ADR-SW-08](03-adrs/ADR-SW-08-configuracao-sem-padrao-no-codigo.md)).
- Recusa esperada é `ProblemaException` com o status; o endpoint não tem `try/catch`.
- SQL em constantes no repositório, sempre por `CommandDefinition` com timeout e token.
  **Transição de estágio leva o estágio esperado no `WHERE`. Parâmetro de coluna `varchar`
  vai como `DbString { IsAnsi = true }`.**
- Log com template, nunca com interpolação: `{ContaId}`, `{Sequencia}`, `{UsuarioId}`. Nunca
  logar senha, token, connection string nem e-mail.
- `internal` para o que só existe por implementação, com `InternalsVisibleTo` para o projeto
  de teste e para `DynamicProxyGenAssembly2` (Moq).
- Comentário explica o porquê, não o quê.

### 7.4 Angular

- Componentes standalone, `ChangeDetectionStrategy.OnPush`, `inject()` em vez de construtor.
- Estado em signals; derivado com `computed`.
- Só os services de `core/` usam `HttpClient`; componente nunca chama o BFF.
- Rotas com `loadComponent`; guards e interceptor funcionais.
- Texto de moeda e data vem pronto do BFF; o front só formata a digitação (`moeda.ts`).
- Prettier: `printWidth` 100, aspas simples.

### 7.5 Testes

O estilo Case/When/Then de [ADR-SW-13](03-adrs/ADR-SW-13-testes-case-when-then.md): classe
aninhada por contexto (`QuandoAMensagemEDuplicada`), um Then por `[Fact]`
(`Entao_nao_toca_no_saldo`), `[Theory]` com classes de equivalência, construtores `Dado.*`,
mock estrito quando a ausência de chamada importa.

## 8. Automação e revisão

| Item | Hoje | Proposto |
|---|---|---|
| Estilo C# | sem `.editorconfig` na raiz | `.editorconfig` e `dotnet format --verify-no-changes` no CI |
| Analisadores | padrão do SDK | `AnalysisLevel` `latest-recommended` num `Directory.Build.props`; avisos novos como erro |
| Propriedades comuns | repetidas em cada `.csproj` | `Directory.Build.props` (`TargetFramework`, `Nullable`, `ImplicitUsings`) |
| Versões de pacote | repetidas em cada `.csproj` | `Directory.Packages.props` (gerenciamento central) |
| Restauração | sem arquivo de trava | `RestorePackagesWithLockFile` e `--locked-mode` no CI |
| Cobertura | medida à mão | Publicada no CI, com piso no valor atual e subindo |
| Portões do CI | não há CI | build, testes, arquitetura, contrato, build do front com os budgets, varredura da imagem |

**Lista de revisão de PR**

- Mudou uma decisão? Há ADR novo ou um ADR marcado como substituído.
- Mudou contrato (rota, DTO, evento)? O arquivo em `docs/arquitetura/contratos` foi
  atualizado, e a mudança é compatível ([regras](04-contratos-internos-e-apis.md#9-versionamento-e-compatibilidade)).
- Nova transição de estágio? Tem o estágio esperado no `WHERE` e teste de integração.
- Nova configuração? Tem `Options` com `Valida`, e o valor está no compose e no deploy.
- O teste está na camada certa: regra em unidade, SQL em integração, pipeline em
  `WebApplicationFactory`.
