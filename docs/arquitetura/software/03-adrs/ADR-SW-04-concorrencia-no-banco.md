# ADR-SW-04 — Concorrência resolvida no banco

- **Status:** Aceita
- **Escopo:** `LancamentoRepository`, `UsuarioRepository`

## Contexto

Três processos escrevem na mesma tabela: a WebApi insere e publica, o relay reserva e
publica, o Consumer consolida. A WebApi e o relay podem alcançar a mesma linha ao mesmo
tempo, duas requisições podem inserir na mesma conta ao mesmo tempo, e o Consumer processa
várias contas em paralelo. Tudo isso sem serviço de trava externo.

## Decisão

| Ponto de disputa | Mecanismo | Garantia |
|---|---|---|
| Próxima sequência da conta | `SELECT MAX(Sequencia)+1 … WITH (UPDLOCK, HOLDLOCK)` na transação do `INSERT`, índice único `(ContaId, Sequencia)` e até 5 novas tentativas em erro 2601/2627 | Sequência única e sem buraco por conta |
| Lote | Contas travadas sempre em ordem ordinal; o lote inteiro numa transação | Sem deadlock entre lotes; tudo ou nada |
| WebApi × relay na mesma linha | `ReservarUmAsync`: `UPDATE … SET Stage = Lido WHERE Id = @Id AND Stage = Cadastrado` | Só um dos dois publica |
| Instâncias do relay | `WITH candidatos AS (SELECT TOP (@Limite) … WITH (UPDLOCK, READPAST, ROWLOCK) …) UPDATE candidatos … OUTPUT inserted.*` | Selecionar e reservar sem janela; uma instância pula o que a outra travou |
| Saldo da conta | `UPDATE SaldoConsolidado WITH (UPDLOCK, SERIALIZABLE) …` seguido de `IF @@ROWCOUNT = 0 INSERT`, no lugar de `MERGE` | Duas consolidações da mesma conta não se perdem |
| Token de uso único | `UPDATE TokensDeUsuario SET ConsumidoEm = @Agora OUTPUT inserted.UsuarioId WHERE … ConsumidoEm IS NULL` | Dois pedidos com o mesmo token não consomem os dois |
| Cadastro com conta sorteada | `INSERT … SELECT … WHERE NOT EXISTS` em `LancamentosDiarios` e índices únicos de e-mail, conta e Google | Conta nova nunca herda histórico; corrida vira erro tratado |

O nível de isolamento é o padrão do SQL Server, `READ COMMITTED` com travas; as dicas de
consulta fazem o resto.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| `IDENTITY` ou `SEQUENCE` | São globais: não dão numeração contínua por conta |
| `sp_getapplock` por conta | Trava fora dos dados, que precisa ser liberada à mão |
| Concorrência otimista com `rowversion` | Exige reler e repetir em cada conflito; para a sequência, a colisão é a regra sob carga |
| `MERGE` | Histórico de problemas de concorrência no SQL Server |
| Trava distribuída (Redis) | Infraestrutura a mais, e a trava não estaria na mesma transação do dado |

## Consequências

**Ganhos**
- Correção sem dependência externa: a trava vive na mesma transação do dado.
- Duas instâncias do relay não inverteriam a ordem, porque a reserva não traz sucessor de
  lançamento pendente. Ainda assim o Job roda com uma tarefa e timeout menor que o
  intervalo, para não duplicar trabalho e mensagens.

**Custos**
- Inserções da mesma conta entram em fila, de propósito. **Hoje a fila é a tabela inteira**,
  por causa do parâmetro `nvarchar` ([ADR-SW-02](ADR-SW-02-dapper-sem-orm.md)): a faixa
  travada pelo `HOLDLOCK` e pelo `SERIALIZABLE` deixa de ser a da conta.
- Sem `READ_COMMITTED_SNAPSHOT`, toda leitura espera escrita em andamento, de qualquer conta.

## Onde está no código

[`Events/Data/LancamentoRepository.cs`](../../../../MeusLancamentosDiarios.Integrator.Events/Data/LancamentoRepository.cs)
(`InserirAsync`, `InserirLoteAsync`, `ReservarUmAsync`, `ReservarParaPublicacaoAsync`,
`ConsolidarAsync`) e
[`WebApi/Auth/Data/UsuarioRepository.cs`](../../../../MeusLancamentosDiarios.Integrator.WebApi/Auth/Data/UsuarioRepository.cs).

## Revisitar quando

- Ao corrigir o tipo do parâmetro e ligar o `READ_COMMITTED_SNAPSHOT`: as dicas `UPDLOCK` e
  `HOLDLOCK` continuam travando, mas a combinação de `READPAST` com leitura por versão deve
  ser confirmada num teste de integração antes de ir para produção.
- Se uma conta concentrar volume a ponto de a fila da sequência aparecer na latência.
