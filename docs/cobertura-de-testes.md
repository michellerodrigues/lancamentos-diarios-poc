# Cobertura dos testes

Medição feita em 06/10/2026 sobre o commit `342dcc9`, com o SDK .NET 10.0.401 e os
testes rodando no runtime 9.0.11.

## Resumo

- **298 testes automatizados, todos passando**, em cinco projetos xUnit. A execução dos
  testes leva cerca de 3 s; com o build incremental, o comando inteiro leva 61 s.
- **Cobertura de linhas: 40,2%** (950 de 2.364). **Ramos: 39,2%** (149 de 380). Sem os
  quatro `Program.cs`, que só montam a aplicação: 42,4% e 40,3%.
- **Os testes são todos de unidade.** Cobrem por inteiro a orquestração do relay e da
  consolidação (`RelayLancamentos` e `ConsolidadorDeMensagem`), além de validações,
  handlers, conversores e regras de acesso. A parte da regra de ordem, retentativa e
  idempotência que mora no SQL (o lançamento anterior que bloqueia, a passagem para Erro,
  o estágio esperado em cada `UPDATE`) fica no `LancamentoRepository`, que os testes só
  usam como mock. Também ficam sem teste o Pub/Sub, os endpoints HTTP, a composição da
  aplicação e o front-end.
- **O `dotnet test` da solução sai com código 1** mesmo com todos os testes passando.
  A causa está em [Problema conhecido](#problema-conhecido).

## Execução

| Projeto de teste | Testes | Passaram | Falharam |
|---|---|---|---|
| `.WebApi.Tests` | 154 | 154 | 0 |
| `.Bff.Tests` | 56 | 56 | 0 |
| `.Common.Tests` | 43 | 43 | 0 |
| `.Events.Tests` | 29 | 29 | 0 |
| `.Consumer.Tests` | 16 | 16 | 0 |
| **Total** | **298** | **298** | **0** |

São 204 métodos de teste: 184 `[Fact]` e 20 `[Theory]`. Cada caso de `[Theory]` conta
como um teste, e daí os 298. O `.TestSupport` não tem testes: é a base `Cenario` e os
construtores NBuilder usados pelos outros projetos.

## Cobertura por projeto

| Projeto | Linhas cobertas | Linhas | Ramos |
|---|---|---|---|
| `.Common` | 26 de 26 | 100% | 100% (17 de 17) |
| `.Messages` | 71 de 72 | 98,6% | sem ramos |
| `.WebApi` | 509 de 1.073 | 47,4% | 42,1% (72 de 171) |
| `.Consumer` | 47 de 106 | 44,3% | 60,0% (6 de 10) |
| `.Bff` | 185 de 456 | 40,6% | 51,3% (40 de 78) |
| `.Events` | 112 de 631 | 17,7% | 13,5% (14 de 104) |
| **Total** | **950 de 2.364** | **40,2%** | **39,2% (149 de 380)** |

Cada projeto de teste gera o seu relatório do coverlet. Os números acima juntam os cinco:
uma linha conta como coberta se algum deles a executou. Por isso o `.Common` chega a 100%,
porque parte dele só é exercitada pelos testes da WebApi e do BFF. Os 98,6% do `.Messages`
vêm dos DTOs que os outros testes usam; ele não tem teste próprio. Arquivos gerados pelo
compilador não entram na conta, e os `Program.cs` entram, com 0%.

## O que os testes cobrem

**`.WebApi.Tests` (154)**

| Classe de teste | Testes | O que cobre |
|---|---|---|
| `CriarLancamentoValidatorTests` | 33 | Conta, valor, data futura no fuso de São Paulo, observação, tipo inválido e vários erros de uma vez |
| `CriarLancamentoHandlerTests` | 22 | Publicação imediata, predecessor pendente, reserva perdida para o relay, broker fora, normalização da conta e valor em centavos |
| `ObterSaldoHandlerTests` | 15 | Saldo consolidado e projetado, pendentes, limite da lista, conta normalizada, conta sem lançamentos |
| `RegrasDeAcessoTests` | 12 | Posse da conta por role e força da senha no cadastro |
| `RedefinicaoDeSenhaTests` | 10 | Link de uso único, revogação do link anterior, e-mail inexistente sem revelar, troca de senha que encerra as sessões |
| `PosseDaContaFilterTests` | 9 | Conta na rota, no comando e no recurso lido; admin passa |
| `LoginGoogleHandlerTests` | 9 | Usuário já vinculado, vínculo a cadastro com senha, primeiro acesso, e-mail não verificado |
| `CriarLancamentosEmLoteTests` | 9 | Lote numa chamada só, sequências, sem publicar no request; lista vazia, teto e erro apontando o item |
| `LoginHandlerTests` | 8 | Claims de conta e role, expiração em 15 min, só o hash do refresh no banco, mesma recusa para e-mail e senha |
| `DispatcherTests` | 7 | Handler de comando e de consulta, validação antes do handler, exceção original |
| `RenovarSessaoHandlerTests` | 6 | Troca do refresh, role relida do banco, reuso que derruba todas as sessões |
| `CadastroDeClienteTests` | 5 | Nova conta sorteada quando a primeira já existe, 409 para e-mail repetido |
| `ObterSaldoValidatorTests` | 5 | Limite de pendentes nulo ou positivo |
| `ObterLancamentoHandlerTests` | 4 | Conversão para a resposta, lançamento inexistente |

**`.Bff.Tests` (56)**

| Classe de teste | Testes | O que cobre |
|---|---|---|
| `SaldoTelaConversorTests` | 11 | Intervalo de polling ligado e desligado, textos formatados, lista de pendentes, "nunca" |
| `AutenticacaoServiceTests` | 9 | Cada rota de `/auth` repassada à WebApi com método e corpo certos |
| `LancamentoServiceTests` | 9 | Conta do token para o cliente e conta informada pelo admin, repasse do 400, token sem conta |
| `PosseDaContaFilterTests` | 7 | Conta na rota e no corpo, admin, corpo sem conta |
| `LancamentoCriadoTelaConversorTests` | 6 | Valor em formato brasileiro, datas em Brasília, rótulos de tipo e estágio |
| `ResultadosHttpTests` | 6 | Respostas da WebApi convertidas em 200, 201 e ProblemDetails |
| `AutenticacaoTests` | 5 | Repasse do Bearer para a WebApi e posse da conta vista do BFF |
| `SaldoServiceTests` | 3 | Payload convertido para a tela e recusa repassada |

**`.Common.Tests` (43)**: `DinheiroHelperTests` (21) cobre reais e centavos, arredondamento
e soma exata; `LancamentoHelperTests` (22) cobre moeda, data, data e hora de UTC para
Brasília e os rótulos.

**`.Events.Tests` (29)**: `RelayLancamentosTests` (23) cobre a publicação com sucesso, a
devolução com backoff exponencial até 60 s, o ciclo vazio, a falha numa conta que trava só
os sucessores dela e a publicação na ordem da sequência. `LancamentoEntityTests` (6) cobre
o sinal do valor pelo tipo.

**`.Consumer.Tests` (16)**: `ConsolidadorDeMensagemTests` cobre o caminho feliz, a
mensagem repetida confirmada sem reprocessar, a linha que saiu do estágio no meio, o corpo
inválido descartado, a falha que devolve a mensagem e o contrato do evento.

## Onde a cobertura está

**Linhas cobertas por inteiro**, nas maiores classes com lógica:

| Classe | Linhas | Ramos |
|---|---|---|
| `RelayLancamentos` (Events) | 70 de 70 | 12 de 12 |
| `ConsolidadorDeMensagem` (Consumer) | 45 de 45 | 6 de 6 |
| `SolicitarRedefinicaoSenhaHandler` (WebApi) | 42 de 42 | 2 de 2 |
| `CriarLancamentoValidator` (WebApi) | 31 de 31 | sem ramos |
| `ObterSaldoHandler` (WebApi) | 29 de 29 | 2 de 2 |
| `EmissorDeTokens` (WebApi) | 28 de 28 | sem ramos |
| `PosseDaContaFilter` (WebApi) | 28 de 28 | 22 de 24 |
| `Dispatcher` (WebApi) | 25 de 25 | 6 de 6 |
| `CriarLancamentosEmLoteHandler` (WebApi) | 25 de 25 | sem ramos |
| `LancamentoHelper` (Common) | 20 de 20 | 13 de 13 |

Também têm todas as linhas cobertas `AberturaDeSessao`, `RenovarSessaoHandler`,
`RedefinirSenhaHandler` e `ObterLancamentoHandler` (WebApi), e `SaldoTelaConversor` e
`LancamentoService` (BFF).

**Quase cobertas.** No `CriarLancamentoHandler` (59 de 71 linhas) faltam dois caminhos:

- O teto de 2 s da publicação imediata. Ele usa `Task.Delay` com o relógio real, e não o
  `TimeProvider`, o que dificulta o teste. Só foi exercitado na medição manual com o
  Pub/Sub pausado, descrita em [requisitos-nao-funcionais.md](requisitos-nao-funcionais.md).
- O `catch` geral. Nem o teste de broker fora passa por ele, porque o
  `RelayLancamentos` já absorve a exceção do publicador e devolve `false`.

**Sem cobertura.** São 49 arquivos e 57% das linhas válidas. Os que mais pesam:

| Classe | Linhas | Por que importa |
|---|---|---|
| `LancamentoRepository` (Events) | 0 de 260 | Todo o SQL: sequência por conta com `UPDLOCK, HOLDLOCK`, reserva com `READPAST`, transições de estágio e a soma ao saldo. É onde ordem e idempotência acontecem de fato, e nos testes só existe como mock. Sozinho, é 11% das linhas |
| `AuthEndpoints` (WebApi e BFF) | 0 de 79 e 0 de 48 | Rotas, policies e respostas de autenticação |
| `UsuarioRepository` (WebApi) | 0 de 68 | Unicidade de e-mail e conta, consumo do token de uso único |
| `PubSubPublicador` e `PubSubProvisionador` (Events) | 0 de 55 e 0 de 48 | Ordering key por conta, criação de tópico e subscription |
| `RelayWorker` (Events) | 0 de 48 | Loop local a cada 2 s e ciclo único no Cloud Run Job |
| `ConsolidacaoWorker` (Consumer) | 0 de 46 | Ack e nack no Pub/Sub, concorrência, desligamento |
| `AuthBootstrapper` e `DatabaseBootstrapper` | 0 de 46 e 0 de 34 | Esquema e usuários de demonstração criados na subida |
| `LancamentosApiClient` (BFF) | 0 de 43 | O cliente HTTP real entre BFF e WebApi |
| Os quatro `Program.cs` | 0 de 121 | Pipeline, injeção de dependência, autenticação |

## Front-end

O Angular tem a configuração de teste do scaffold (Karma, Jasmine e o script `npm test`),
mas nenhum arquivo `*.spec.ts`. Os serviços, o interceptor de autenticação, os guards, a
máscara de moeda, o polling e as telas estão sem teste.

## O que não tem teste

- **Integração HTTP.** Nenhum teste sobe a WebApi ou o BFF. JWT real, policies, filtro de
  posse no pipeline, ProblemDetails e a chamada BFF → WebApi ficam de fora.
- **SQL real.** Locks, sequência sob concorrência, unicidade e criação do esquema.
- **Pub/Sub real ou emulador.** Ordering key, ack, nack, reentrega e o fluxo de ponta a
  ponta, do lançamento ao saldo.
- **Front-end.** Nem unitário, nem de ponta a ponta.
- **Carga.** A carga do requisito do desafio foi medida à mão, com
  [tools/carga/carga_saldo.py](../tools/carga/carga_saldo.py). A
  [tools/RelayBench](../tools/RelayBench) mede só o relay e fica fora da solução.
- **CI.** Não há pipeline, então nenhum teste roda sozinho.

Próximos passos, pela ordem de retorno:

1. `<IsTestProject>false</IsTestProject>` no `.TestSupport`, para o `dotnet test` sair com 0.
2. Testes de integração do `LancamentoRepository` contra SQL Server em container
   (Testcontainers): sequência, reserva, consolidação sem soma em dobro.
3. `WebApplicationFactory` para a WebApi e o BFF: autenticação, policies e posse da conta
   no pipeline de verdade.
4. Trocar o `Task.Delay` do teto de 2 s pelo `TimeProvider` e testar esse caminho.
5. Um pipeline de CI que rode os testes e publique a cobertura.

## Problema conhecido

O `dotnet test` da solução termina com "Execução de Teste Anulada" e código 1. Todos os
298 testes passam, e quem falha é o `.TestSupport`:

- ele referencia o pacote `xunit`, cujo `xunit.core.props` marca o projeto como
  `IsTestProject=true`;
- o `dotnet test` tenta executá-lo, mas ele é uma biblioteca e não copia as dependências
  para o `bin`;
- o testhost para ao não achar o `Azure.Core` que o `deps.json` lista.

A correção é `<IsTestProject>false</IsTestProject>` no `.csproj` do `.TestSupport`, ou
trocar o `xunit` por `xunit.extensibility.core` + `xunit.assert`. Enquanto isso, uma CI
leria o código 1 como falha.

## Como reproduzir

```bash
dotnet test MeusLancamentosDiarios.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Cada projeto de teste deixa um `coverage.cobertura.xml` em `TestResults`. Para ver um
relatório em HTML com os cinco juntos:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/relatorio -assemblyfilters:"-*TestSupport*"
```
