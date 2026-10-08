# ADR-SOL-05 — BFF como única porta do front

- **Status:** Aceita (o ingress interno da WebApi no GCP é proposta)

## Contexto

A tela de saldo quer um payload pronto: valores em reais formatados, datas em Brasília,
rótulos de estágio e a dica de quando consultar de novo. A WebApi devolve dados crus, e expô-la
à internet aumentaria a superfície: lote, Swagger e detalhes de lançamento.

## Decisão

- O front fala **só com o BFF**, em `/api`.
- O BFF valida o JWT, aplica as mesmas policies e confere a posse da conta antes de ir à
  WebApi, e **repassa o mesmo Bearer** (não troca por um token próprio).
- A WebApi valida tudo de novo: é ela quem guarda o dado.
- **No GCP**, a WebApi fica com ingress `internal`. O BFF chega a ela pela VPC (Direct VPC
  egress com `all-traffic`), para a chamada contar como interna. Para exigir também IAM na
  WebApi, o BFF mandaria um token de identidade em `X-Serverless-Authorization`, porque o
  `Authorization` já leva o JWT do usuário.
- O lote (`POST /lancamentos/lote`) e o Swagger da WebApi ficam acessíveis só de dentro da
  rede. Se o admin precisar do lote em produção, a rota ganha um par no BFF com `SomenteAdmin`.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Front chamando a WebApi | Formatação no cliente, CORS, a WebApi inteira exposta |
| API Gateway na frente da WebApi | Não valida HS256 na borda; não monta o payload da tela |
| BFF com token próprio (token exchange) | A WebApi deixaria de ver o usuário; o BFF viraria um atalho de acesso |

## Consequências

**Ganhos**
- A WebApi não aparece na internet.
- Payload por tela, formatado uma vez no servidor.
- Defesa em profundidade: um BFF com defeito não dá acesso que a WebApi negaria.

**Custos**
- Um salto a mais por requisição.
- O BFF guarda a chave HS256, que serve tanto para validar quanto para emitir token
  ([ADR-SOL-06](ADR-SOL-06-identidade-propria-jwt.md)).
- Código duplicado nos dois lados (filtro de posse, parâmetros do JWT).
- Falha de rede entre os dois vira 500 no BFF em vez de 502 ou 504.

## Revisitar quando

Surgir um segundo front (app nativo): ele pode reutilizar o BFF se as telas forem as mesmas,
ou ganhar um BFF próprio.
