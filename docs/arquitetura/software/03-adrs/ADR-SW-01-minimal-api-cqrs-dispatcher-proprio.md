# ADR-SW-01 — Minimal API com CQRS e dispatcher próprio

- **Status:** Aceita
- **Escopo:** WebApi; contratos no `.Messages` e no `.Common`

## Contexto

A WebApi tem 14 rotas e dois grupos de casos de uso: lançamento e saldo, e autenticação.
Escrita e leitura têm formas diferentes: a escrita grava e publica; a leitura junta o read
model com a soma dos pendentes. Toda escrita precisa de validação antes de qualquer acesso
ao banco, e o erro de validação tem de chegar ao front com a mensagem de cada campo.

O MediatR, a escolha usual para despachar comandos em .NET, mudou de licenciamento.

## Decisão

- **Minimal API**, com rotas agrupadas por área (`MapGroup`) e a policy no grupo.
- **Comandos e consultas como `record`**, que implementam `ICommand<T>` ou `IQuery<T>` (no
  `.Common`). Os comandos moram no `.Messages`, porque são o próprio corpo que o BFF envia;
  as consultas moram na WebApi, porque são montadas da rota e do token.
- **Um handler por caso de uso**, em `Features/<Área>/Commands|Queries/<Caso>/`.
- **Dispatcher próprio** (`Cqrs/Dispatcher.cs`, cerca de 60 linhas): resolve o handler pelo
  tipo da mensagem, roda antes o validator FluentValidation da mensagem, se houver, e
  desembrulha a `TargetInvocationException` para o middleware ver a exceção real.
- **Registro por varredura do assembly**: handler e validator novos funcionam só por existir.
- **Erros como `ProblemDetails`**, por dois `IExceptionHandler`: validação vira 400 com os
  erros por campo; `ProblemaException` leva o status escolhido pelo handler (401, 409, 400).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Controllers MVC | Mais cerimônia para 14 rotas, sem ganho |
| MediatR | Licença nova; os *pipeline behaviors* não eram necessários |
| Endpoint chamando o handler direto | A validação ficaria repetida em cada endpoint |
| Validação por DataAnnotations | Regras como "data não futura no fuso de São Paulo" e o erro apontando `Itens[3].Valor` ficam naturais no FluentValidation |

## Consequências

**Ganhos**
- O endpoint só declara: rota, policy, filtro de posse e o que devolve.
- Nenhuma dependência externa para despachar.
- O dispatcher é testado isoladamente (`DispatcherTests`, 7 testes).

**Custos**
- `MakeGenericType` e `MethodInfo.Invoke` a cada chamada. O custo é pequeno perto de uma ida
  ao banco, mas existe.
- Handler esquecido só aparece em execução (`InvalidOperationException`), não na compilação.
- Sem *pipeline behaviors*: auditoria, métricas ou transação por handler pediriam um
  decorator no dispatcher.

## Onde está no código

- [`WebApi/Cqrs/`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Cqrs)
- [`Common/Cqrs/Contracts/`](../../../../MeusLancamentosDiarios.Integrator.Common/Cqrs/Contracts)
- [`WebApi/Program.cs`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Program.cs): `AddCqrs` e os `AddExceptionHandler`

## Revisitar quando

Preocupações transversais por handler (auditoria de quem lançou, métricas por caso de uso,
transação) passarem a se repetir. A saída é um decorator no `Dispatcher`, antes de adotar
uma biblioteca.
