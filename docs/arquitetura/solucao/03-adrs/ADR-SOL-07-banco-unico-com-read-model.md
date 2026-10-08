# ADR-SOL-07 — Um banco para os processos do serviço, com o read model no mesmo banco

- **Status:** Aceita

## Contexto

WebApi, relay e Consumer são unidades de implantação de um mesmo serviço, o de lançamentos;
não são serviços independentes com donos diferentes. A consolidação precisa marcar o
lançamento como consolidado e somar o valor ao saldo de forma atômica, senão uma falha entre
as duas coisas deixa o saldo errado.

## Decisão

- Um banco, `Lancamentos`, com quatro tabelas: `LancamentosDiarios` (registro e outbox),
  `SaldoConsolidado` (read model), `Usuarios` e `TokensDeUsuario`.
- O read model mora no mesmo banco: estágio e saldo mudam na mesma transação local.
- Sem chave estrangeira entre as tabelas do fluxo, nem delas para `Usuarios`: o saldo só
  nasce na primeira consolidação, e a conta pode ter lançamentos antes de ter dono.
- As tabelas de usuário são criadas só pela WebApi. O relay e o Consumer não sabem que login
  existe.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Banco separado para o saldo | Sem transação local entre estágio e saldo; exigiria consistência distribuída |
| Um banco por processo | Quebraria a transição atômica; três esquemas para um só serviço |
| Réplica de leitura para a consulta | Não é alternativa, é evolução: isola a leitura da escrita sem mudar o modelo |

## Consequências

**Ganhos**
- Estágio e saldo mudam juntos, numa transação local.
- Um banco a operar, um backup, uma restauração.

**Custos**
- **Leitura e escrita disputam o mesmo recurso.** A consulta do saldo roda no mesmo processo,
  no mesmo pool e no mesmo banco do lançamento: se a leitura saturar, o lançamento degrada
  junto (RNF-01, parte 1).
- Três processos acoplados ao mesmo esquema, mitigado pelo repositório compartilhado
  ([ADR-SW-06](../../software/03-adrs/ADR-SW-06-camada-de-dados-compartilhada-no-events.md)).
- Usuários e lançamentos no mesmo banco: o mesmo usuário de banco alcança dados de
  autenticação e de negócio. Separar por esquema (`auth`, `lancamentos`) e por usuário de
  banco reduz o alcance de uma credencial vazada.

## Revisitar quando

A leitura ameaçar a escrita: primeiro um pool e uma connection string só para consulta, com
limite de concorrência; depois uma réplica de leitura; por fim um serviço de consulta à
parte.
