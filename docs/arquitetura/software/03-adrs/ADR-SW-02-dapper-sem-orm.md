# ADR-SW-02 — Dapper com SQL explícito, sem EF Core

- **Status:** Aceita
- **Escopo:** camada de dados (`.Events`), usuários (WebApi)

## Contexto

As garantias do fluxo moram no SQL: a sequência por conta com `UPDLOCK, HOLDLOCK`, a reserva
com `UPDLOCK, READPAST` e `OUTPUT`, e o estágio esperado no `WHERE` de cada transição
([ADR-SW-03](ADR-SW-03-outbox-na-propria-linha.md), [ADR-SW-04](ADR-SW-04-concorrencia-no-banco.md)).
Com um ORM, essas consultas seriam SQL cru do mesmo jeito, e o resto do modelo é pequeno:
quatro tabelas.

A POC começou em SQLite. Lá, `Guid`, `DateOnly` e `DateTimeOffset` exigiam *type handlers*
do Dapper, e cada um produziu um bug antes de acertar o formato. Com SQL Server os três tipos
fazem o caminho de ida e volta sozinhos.

## Decisão

- **Dapper** sobre `Microsoft.Data.SqlClient`, com `CommandDefinition` carregando timeout
  (`Banco:TimeoutComandoSegundos`) e `CancellationToken`.
- **Uma conexão por operação**, aberta e fechada no método, com o pool do ADO.NET.
- **Transação explícita só onde há mais de um comando que precisa ser atômico**: inserção
  avulsa, inserção em lote e consolidação.
- **Esquema garantido na subida** por scripts T-SQL idempotentes (`IF OBJECT_ID … IS NULL`,
  `IF NOT EXISTS (SELECT … FROM sys.indexes …)`), em `DatabaseBootstrapper` e
  `AuthBootstrapper`. Mudança não aditiva vira um bloco condicional no script, como a troca
  da coluna `Papel` por `Role` com `sp_rename`.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| EF Core com migrations | As consultas críticas seriam SQL cru; o ORM pagaria a complexidade sem ajudar onde importa |
| EF Core no CRUD e SQL cru no fluxo | Dois modelos de acesso para quatro tabelas |
| Stored procedures | A regra ficaria fora do repositório de código versionado com a aplicação, e o deploy teria um passo a mais |
| Ferramenta de migração (DbUp, Flyway) | Não havia evolução de esquema que pedisse histórico. Proposta para quando houver: [dados macro e migração](../../solucao/05-dados-macro-e-migracao.md#5-estratégia-de-migração) |

## Consequências

**Ganhos**
- Cada trava e cada dica de consulta está à vista no código.
- Sem *type handlers*: o mapeamento é direto.

**Custos**
- **O Dapper envia `string` como `nvarchar(4000)`.** Contra a coluna `ContaId varchar(20)`,
  a conversão impede a busca pelo índice e faz as travas por conta cobrirem a tabela inteira.
  É o defeito medido em [RNF-01](../../../requisitos-nao-funcionais.md#parte-2--50-consultas-por-segundo-com-até-5-de-perda).
  A correção é tipar o parâmetro (`DbString { IsAnsi = true, Length = 20 }`).
- SQL em string não é verificado na compilação. Só teste de integração contra SQL Server o
  verifica, e ele ainda não existe ([07 · Testes](../07-testes-e-padroes-de-codigo.md)).
- Sem histórico de versões do esquema. Com o script rodando na subida, o usuário da aplicação
  precisa de permissão de DDL sempre que houver objeto a criar.

## Onde está no código

- [`Events/Data/LancamentoRepository.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/Data/LancamentoRepository.cs)
- [`Events/Data/DatabaseBootstrapper.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/Data/DatabaseBootstrapper.cs)
- [`WebApi/Auth/Data/`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Data)

## Revisitar quando

A primeira mudança de esquema que não for "criar se não existe": coluna alterada, dado a
transformar, índice a trocar. Nesse ponto entram as migrações versionadas, num passo de
deploy próprio e com um usuário de DDL separado do usuário da aplicação.
