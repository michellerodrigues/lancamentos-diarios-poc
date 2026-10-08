# ADR-SOL-10 — Segredos no Secret Manager e uma imagem para todos os ambientes

- **Status:** Aceita para o relay (no script de deploy); proposta para os demais containers

## Contexto

O repositório é público. A mesma imagem deve ir do local à produção sem rebuild, mudando só
configuração ([ADR-SW-08](../../software/03-adrs/ADR-SW-08-configuracao-sem-padrao-no-codigo.md)).
Senha de banco, chave do JWT e senha do SMTP não podem estar na imagem nem na linha de
comando.

## Decisão

- **Segredos no Secret Manager**, um segredo por valor: connection string, chave do JWT,
  senha do SMTP.
- **Injetados pelo Cloud Run como variável de ambiente** (`--set-secrets`), já no formato da
  configuração do .NET (`Banco__ConnectionString`, `Auth__Jwt__Chave`).
- **A service account de cada container** recebe `roles/secretmanager.secretAccessor` só nos
  segredos que usa.
- **A imagem é a mesma em todos os ambientes**; o que muda vem de variável de ambiente. A
  imagem traz os padrões de produção.
- O `appsettings.json` base não deve ter segredo nem valor que pareça segredo.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Segredo montado como arquivo | Também suportado pelo Cloud Run; a variável é mais simples e casa com a configuração do .NET |
| Ler o Secret Manager pela API na subida | Código a mais e uma dependência na partida |
| Segredo no `appsettings` de produção dentro da imagem | Inaceitável num repositório público e numa imagem que circula |

## Consequências

**Ganhos**
- Nenhum segredo na imagem, no repositório ou no histórico de comandos.
- Promoção de ambiente sem rebuild.

**Custos**
- Segredo como variável é resolvido quando a instância sobe: trocar o valor pede uma revisão
  nova ou a reciclagem das instâncias.
- **Lacunas encontradas:**
  - o README lista os papéis da service account do relay sem `roles/secretmanager.secretAccessor`,
    que o `--set-secrets` do script exige;
  - o `appsettings.json` base da WebApi, do relay e do Consumer traz a connection string com a
    senha do `sa` do ambiente local, e vai dentro das imagens;
  - desde o commit `45bcd17`, o `appsettings.json` base da WebApi e do BFF traz
    `"Chave": "MinhaChaveCompartilhada"`. Com 23 bytes, não passa na validação de 32 e a
    aplicação não sobe sem a chave do ambiente, o que é seguro; mas contradiz o README, que
    diz que a chave não fica no `appsettings.json`.

## Revisitar quando

Antes do primeiro ambiente na nuvem: limpar o `appsettings.json` base e acrescentar o papel
de acesso aos segredos em todas as service accounts
([implantação](../07-implantacao-e-infraestrutura.md#6-identidades-e-iam)).
