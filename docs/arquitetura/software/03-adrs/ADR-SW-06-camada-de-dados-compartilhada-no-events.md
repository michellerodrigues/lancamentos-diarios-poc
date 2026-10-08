# ADR-SW-06 — Camada de dados compartilhada no `.Events`

- **Status:** Aceita
- **Escopo:** `.Events`, WebApi, Consumer

## Contexto

WebApi, relay e Consumer escrevem em `LancamentosDiarios`, e as regras de transição de
estágio estão no SQL ([ADR-SW-03](ADR-SW-03-outbox-na-propria-linha.md)). Se cada processo
tivesse a própria cópia do SQL, a regra de bloqueio ou o estágio esperado de uma transição
poderiam divergir entre eles sem nenhum teste perceber.

## Decisão

O projeto `.Events`, além do worker do relay, carrega:

- `ILancamentoRepository` e `LancamentoRepository`, com todo o SQL dos lançamentos;
- `DatabaseBootstrapper`, `BancoOptions` e `IDbConnectionFactory`;
- o contrato do evento, `LancamentoRegistradoEvent`, e as opções de JSON (`EventosJson`);
- o publicador (`PubSubPublicador`) e o relay (`RelayLancamentos`).

A WebApi e o Consumer referenciam o `.Events`. O registro de DI é compartilhado
(`AddLancamentosData`, `AddPubSub`, `AddRelay`).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Uma biblioteca `.Data` separada do worker | Mais limpo de nome; não foi feito para não ter mais um projeto na POC. É o caminho natural de evolução |
| SQL próprio em cada processo | Três cópias da regra de transição |
| Só a WebApi escreve; relay e Consumer chamam a WebApi por HTTP | A consolidação passaria a depender da WebApi no ar, e cada transição ganharia uma ida pela rede |

## Consequências

**Ganhos**
- Uma única implementação de cada transição, usada pelos três processos.
- Os testes do relay e do Consumer usam a mesma interface que a produção.

**Custos**
- O nome engana: quem procura acesso a dados não procura num projeto chamado Events.
- A imagem da WebApi leva o worker do relay e a biblioteca do Pub/Sub como dependência
  transitiva.
- O `appsettings.json` do `.Events` viria junto no publish da WebApi e do Consumer e
  disputaria o caminho (`NETSDK1152`). Os dois `.csproj` têm o alvo
  `IgnorarAppsettingsDasReferencias` para descartá-lo.
- O `.TestSupport` referencia o `.Events` para os construtores de `LancamentoEntity`.

## Onde está no código

- [`Events/LancamentosServiceCollectionExtensions.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/LancamentosServiceCollectionExtensions.cs)
- [`WebApi/MeusLancamentosDiarios.Integrator.WebApi.csproj`](../../../../MeusLancamentosDiarios.Integrator.WebApi/MeusLancamentosDiarios.Integrator.WebApi.csproj): referência e alvo de publish

## Revisitar quando

Surgir um quarto processo que acesse os lançamentos, ou o relay mudar de forma (por exemplo,
virar endpoint). Extrair `.Events.Data` (repositório, bootstrapper, opções) e deixar o
`.Events` só com o relay.
