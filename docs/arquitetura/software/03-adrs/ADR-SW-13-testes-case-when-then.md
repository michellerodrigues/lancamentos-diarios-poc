# ADR-SW-13 — Testes de unidade no estilo Case/When/Then

- **Status:** Aceita
- **Escopo:** `tests/`

## Contexto

As regras com mais risco têm muitos contextos: publicação com e sem predecessor, broker fora,
reserva perdida, mensagem repetida, token reutilizado. Um teste longo com várias afirmações
quebra e não diz qual comportamento se perdeu.

## Decisão

- **xUnit, Moq, NBuilder e FluentAssertions**, com `Microsoft.Extensions.TimeProvider.Testing`
  para tempo.
- **Uma classe externa por objeto sob teste; uma classe aninhada por contexto**, com nome de
  frase: `QuandoExistePredecessorPendenteNaConta`.
- **Base `Cenario`** (`IAsyncLifetime`): `Case()` monta o cenário e `When()` executa a ação,
  uma vez antes de cada `[Fact]`.
- **Um Then por `[Fact]`**: `Entao_confirma_sem_reprocessar`.
- **`[Theory]` com classes de equivalência** em vez de um teste por valor.
- **Construtores de cenário** em `Dado`: `Dado.UmLancamento().NaConta("ABC1234").ComSequencia(2).Build()`.
- Mocks estritos onde a ausência de uma chamada importa (`MockBehavior.Strict` no Consumer).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Arrange/Act/Assert num método só | Várias afirmações por teste; o nome não diz qual quebrou |
| AutoFixture | Dados aleatórios escondem o caso; os construtores deixam explícito o que importa |
| NSubstitute, Shouldly | Equivalentes; ficou o que o time já usava |

## Consequências

**Ganhos**
- O nome do teste que quebrou já descreve o comportamento perdido.
- 298 testes rodam em cerca de 3 s.

**Custos**
- Muitas classes pequenas.
- `Case()` e `When()` rodam uma vez por `[Fact]`: barato em unidade, caro se um dia o
  cenário subir banco. Para integração, usar `IClassFixture` e um contexto por classe.
- **Licença do FluentAssertions.** A versão 8 passou a exigir licença comercial para uso
  comercial. O projeto está na 6.12.2 (Apache 2.0). Não atualizar sem decidir; a
  alternativa é o fork comunitário AwesomeAssertions, ou o Shouldly.

## Onde está no código

- [`tests/MeusLancamentosDiarios.Integrator.TestSupport/Cenario.cs`](../../../../tests/MeusLancamentosDiarios.Integrator.TestSupport/Cenario.cs)
- [`tests/MeusLancamentosDiarios.Integrator.TestSupport/Construtores/Dado.cs`](../../../../tests/MeusLancamentosDiarios.Integrator.TestSupport/Construtores/Dado.cs)
- A estratégia completa, com integração, contrato e arquitetura, em [07 · Testes e padrões](../07-testes-e-padroes-de-codigo.md)

## Revisitar quando

Os testes de integração entrarem (Testcontainers): eles precisam de outro ciclo de vida,
com o container compartilhado pela classe.
