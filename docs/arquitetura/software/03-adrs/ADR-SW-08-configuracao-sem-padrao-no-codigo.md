# ADR-SW-08 — Configuração sem padrão no código, validada na subida

- **Status:** Aceita
- **Escopo:** todos os processos .NET

## Contexto

A mesma imagem roda no `docker compose` e no Cloud Run, e o que muda entre os dois é
configuração. Um valor padrão escondido no código faria um ambiente mal configurado subir
apontando para `localhost`, ou com um limite que ninguém escolheu.

## Decisão

- Cada seção do `appsettings` tem uma classe `*Options` **sem valor padrão** (só string vazia
  e zero).
- Cada uma expõe uma propriedade `Valida` e é registrada com
  `.Validate(o => o.Valida, "mensagem").ValidateOnStart()`.
- Os valores vêm do `appsettings.json` da aplicação, do `appsettings.Development.json` no
  `dotnet run`, e de variáveis de ambiente no compose e no Cloud Run (`Secao__Chave`).
- Os padrões de produção ficam na imagem, como `ENV` no Dockerfile:
  `ASPNETCORE_ENVIRONMENT=Production`, `Relay__LoopContinuo=false`,
  `PubSub__CriarRecursos=false`.

| Options | Seção | Validação |
|---|---|---|
| `BancoOptions` | `Banco` | connection string e timeout maior que zero |
| `PubSubOptions` | `PubSub` | projeto, tópico e subscription |
| `RelayOptions` | `Relay` | intervalo, lote, tentativas, backoff e expiração da reserva maiores que zero |
| `ConsumerOptions` | `Consumer` | tentativas e concorrência maiores que zero |
| `SaldoOptions` | `Saldo` | `LimitePendentesPadrao` maior que zero |
| `AuthOptions` (WebApi) | `Auth` | chave do JWT com 32 bytes ou mais, emissor, audiência, validades; URL do front; SMTP |
| `JwtOptions` (BFF) | `Auth:Jwt` | chave, emissor e audiência iguais aos da WebApi |
| `WebApiOptions` (BFF) | `WebApi` | URL absoluta e timeout maior que zero |

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Padrões no código | Esconde do operador o valor que está valendo |
| `IValidateOptions<T>` em classes próprias | Mais arquivos para a mesma regra |
| DataAnnotations | Não expressa regras cruzadas, como renovação maior que acesso |

## Consequências

**Ganhos**
- Falta de configuração derruba a subida com a mensagem certa. O caso mais importante: sem
  chave do JWT com 32 bytes, nem a WebApi nem o BFF sobem. O valor que está hoje no
  `appsettings.json` base (`MinhaChaveCompartilhada`, 23 bytes) não passa nessa validação, e
  por isso não serve de chave por acidente.
- Um só lugar para ler o que vale em cada ambiente.

**Custos**
- Todo ambiente precisa de todas as seções, inclusive as que o processo só usa
  indiretamente: a WebApi exige a seção `Relay` porque registra o relay para a publicação
  imediata.
- O `appsettings.json` base vai dentro da imagem com valores do ambiente local, inclusive a
  connection string com a senha do `sa`. Ver [gestão de segredos](../../solucao/06-seguranca-e-threat-model.md#5-gestão-de-segredos).

## Onde está no código

Os `Program.cs` e as classes `*Options` de cada projeto;
[`Events/LancamentosServiceCollectionExtensions.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/LancamentosServiceCollectionExtensions.cs).

## Revisitar quando

Os segredos passarem todos para o Secret Manager ([ADR-SOL-10](../../solucao/03-adrs/ADR-SOL-10-segredos-e-imagem-unica.md)):
o `appsettings.json` base deve deixar de ter connection string e chave, e só o ambiente as
fornece.
