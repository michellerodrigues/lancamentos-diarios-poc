# ADR-SOL-08 — Load Balancer HTTPS como entrada única

- **Status:** Proposta

## Contexto

O PWA só instala com HTTPS, e o login do Google exige uma origem autorizada com domínio.
Front e API no mesmo domínio dispensam CORS. A WebApi deve continuar fora da internet. E não
há hoje limite de requisições nem bloqueio de força bruta (RNF-24).

## Decisão

- **Load Balancer de aplicação externo e global**, com certificado gerenciado pelo Google e
  redirecionamento de HTTP para HTTPS.
- **Mapa de URLs**: `/api/*` para o BFF, o resto para o front, cada um por um NEG serverless.
- Front e BFF com ingress `internal-and-cloud-load-balancing`: da internet, só o Load
  Balancer os alcança.
- **Cloud Armor** na frente do BFF, com limite de requisições por IP em `/api/auth/*` e um
  teto geral em `/api/*`.
- O front passa a chamar `/api` relativo; o `Cors:Origens` do BFF só serve no local.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| API Gateway do GCP | Cobre só a metade da API; não valida HS256 na borda; o front precisaria de outro caminho |
| Firebase Hosting com rewrites para o Cloud Run | Simples e barato; menos controle, sem Cloud Armor na frente |
| Mapeamento de domínio do Cloud Run | Sem Cloud Armor e sem um domínio só para os dois serviços |
| Apigee | Desproporcional para este volume |

Na AWS, o equivalente é o ALB com regra por caminho, ou CloudFront com S3 para o front e API
Gateway com VPC Link para o BFF ([README](../../../../README.md#indo-para-o-gcp)).

## Consequências

**Ganhos**
- Uma origem só, HTTPS gerenciado, sem CORS em produção.
- Limite de requisições e bloqueio na borda, sem código.

**Custos**
- Custo fixo por hora da regra de encaminhamento, mesmo sem tráfego.
- Um domínio precisa ser escolhido; o Client ID do Google e o link do e-mail de redefinição
  dependem dele.

## Revisitar quando

Houver exigência de WAF gerenciado completo ou de CDN para o front: Cloud CDN liga no mesmo
Load Balancer.
