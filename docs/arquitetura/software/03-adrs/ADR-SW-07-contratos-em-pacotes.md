# ADR-SW-07 — Contratos em pacotes `.Messages` e `.Common`

- **Status:** Aceita
- **Escopo:** BFF, WebApi, workers; pacotes no nuget.org

## Contexto

O BFF envia à WebApi exatamente o que ela recebe e lê exatamente o que ela devolve. Copiar
esses tipos no BFF deixaria os dois lados divergirem em silêncio. Os workers também precisam
de parte do vocabulário (enums de estágio e tipo, helpers de dinheiro), mas rodam na imagem
`runtime:9.0`, sem ASP.NET Core. E um outro cliente da WebApi, no futuro, precisaria dos
mesmos contratos.

## Decisão

- **`.Messages`**: só os requests e responses da WebApi, inclusive o lote e o detalhe do
  lançamento, que o BFF não usa. Os comandos são a própria mensagem (`CriarLancamentoCommand`
  implementa `ICommand<CriarLancamentoResponse>`). As consultas ficam na WebApi, porque são
  montadas da rota e do token e nunca viajam como corpo.
- **`.Common`**: enums, `DinheiroHelper`, `LancamentoHelper`, `Roles`, `Politicas`,
  `ClaimsDoToken`, `UsuarioAutenticadoExtensions.PodeAcessarConta`, `IReferenciaConta`,
  `ICommand` e `IQuery`. **Sem dependência de ASP.NET Core.**
- **Publicados no nuget.org** como `POCMica.MeusLancamentosDiarios.Integrator.Common` e
  `POCMica.MeusLancamentosDiarios.Integrator.Messages`, versão 1.0.0, licença MIT, com o
  README de cada projeto como página do pacote. O prefixo `POCMica.` é exigido pela API key;
  o namespace do código não muda.
- Dentro da solução, os projetos se referenciam diretamente, não pelo pacote.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| DTOs copiados no BFF | Divergência silenciosa entre os lados |
| Cliente gerado do OpenAPI (NSwag, Kiota) | Válido; os comandos deixariam de ser as mensagens do CQRS, e o código gerado teria de ser regenerado a cada mudança |
| Um pacote único com ASP.NET Core | Os workers não podem depender dele |

## Consequências

**Ganhos**
- Contrato entre BFF e WebApi verificado pelo compilador.
- Enum, regra de posse e formatação escritos uma vez.

**Custos**
- O filtro de posse, o transformer do OpenAPI e as opções do JWT dependem de ASP.NET Core e
  ficaram duplicados no BFF e na WebApi (dívida D5 do [SAD-App](../01-sad-app.md#11-dívida-técnica-conhecida)).
- **Versão publicada é permanente.** Toda mudança no contrato pede subir o `Version` no
  `.csproj` antes do push, e o `.Common` vai antes do `.Messages`, que depende dele.
- Os enums viajam como texto no JSON (`"Credito"`), mas no banco são inteiros: renomear um
  membro quebra o JSON; renumerar quebra o banco.

## Onde está no código

- [`Messages/`](../../../../MeusLancamentosDiarios.Integrator.Messages) e o [README do pacote](../../../../MeusLancamentosDiarios.Integrator.Messages/README.md)
- [`Common/`](../../../../MeusLancamentosDiarios.Integrator.Common) e o [README do pacote](../../../../MeusLancamentosDiarios.Integrator.Common/README.md)

## Revisitar quando

Um cliente externo começar a usar os pacotes: aí vale versionar a API (`/v1`) e checar no CI
se uma mudança no `.Messages` quebra o contrato publicado
([04 · Contratos](../04-contratos-internos-e-apis.md#9-versionamento-e-compatibilidade)).
