# Documentação de arquitetura

Os documentos mínimos do arquiteto de software e do arquiteto de solução para a POC
Lançamentos Diários. Retrato do código no commit `45bcd17`, em 07/10/2026.

Eles se apoiam nos documentos que já existiam e não os repetem:
[arquitetura](<../Arquitetura dos Lançamentos Diários.md>),
[requisitos funcionais](../requisitos-funcionais.md),
[requisitos não funcionais](../requisitos-nao-funcionais.md) (com as medições),
[capacidade do relay](../capacidade-do-relay.md),
[cobertura dos testes](../cobertura-de-testes.md) e o [README](../../README.md).

## Arquiteto de software

Foco: o aplicativo, seus componentes, o código, os padrões e a qualidade técnica.

| # | Documento | Conteúdo |
|---|---|---|
| 1 | [SAD-App](software/01-sad-app.md) | Módulos, camadas, componentes transversais, padrões, restrições, dívida técnica |
| 2 | [C4 níveis 3 e 4](software/02-c4-componentes-e-codigo.md) | Componentes de cada container, classes que carregam regra, oito sequências críticas |
| 3 | [ADRs de software](software/03-adrs/README.md) | 14 decisões: CQRS, Dapper, outbox na linha, concorrência, dinheiro, contratos, configuração, testes, sessão |
| 4 | [Contratos internos e APIs](software/04-contratos-internos-e-apis.md) | As duas APIs rota a rota, DTOs, token, evento, interfaces entre camadas, versionamento |
| 5 | [Modelo de dados](software/05-modelo-de-dados.md) | Modelo lógico, máquina de estados com as guardas, DDL, índices, concorrência, volume |
| 6 | [NFRs do aplicativo](software/06-nfrs-do-aplicativo.md) | Desempenho medido, concorrência, resiliência, timeouts, retentativas, cache, limites |
| 7 | [Testes e padrões de código](software/07-testes-e-padroes-de-codigo.md) | Camadas de teste, regras de arquitetura como teste, integração, contrato, padrões |
| 8 | [Build e deploy](software/08-build-e-deploy.md) | Artefatos, pipeline, versionamento, ordem de deploy, configuração por ambiente |

## Arquiteto de solução

Foco: a solução completa, a integração entre sistemas, a infraestrutura, a segurança, o
negócio e a transição.

| # | Documento | Conteúdo |
|---|---|---|
| 1 | [SAD-Sol](solucao/01-sad-sol.md) | Contexto, escopo, premissas, restrições, visão ponta a ponta, atributos de qualidade, justificativas |
| 2 | [C4 níveis 1 e 2](solucao/02-c4-contexto-e-containers.md) | Contexto, containers, integrações, do local ao GCP |
| 3 | [ADRs de solução](solucao/03-adrs/README.md) | 13 decisões: nuvem, banco, mensageria, publicação, BFF, identidade, entrada, segredos, IaC |
| 4 | [Integração e contratos](solucao/04-integracao-e-contratos.md) | Catálogo de integrações, padrões, mensageria e garantias, sistemas externos, governança |
| 5 | [Dados macro e migração](solucao/05-dados-macro-e-migracao.md) | Onde vive cada dado, dados mestres, consistência, migração, reconciliação, retenção |
| 6 | [Segurança e threat model](solucao/06-seguranca-e-threat-model.md) | Autenticação, autorização, criptografia, segredos, rede, LGPD, STRIDE, plano de ação |
| 7 | [Implantação e infraestrutura](solucao/07-implantacao-e-infraestrutura.md) | Ambientes, topologia local e no GCP, rede, IAM, Terraform de referência |
| 8 | [NFRs de solução](solucao/08-nfrs-de-solucao.md) | SLA e SLOs, disponibilidade, escalabilidade, capacidade, DR, custo |
| 9 | [Transição, roadmap e riscos](solucao/09-transicao-roadmap-e-riscos.md) | Fases, rollout, handover, dependências, matriz de riscos |
| 10 | [Observabilidade e operação](solucao/10-observabilidade-e-operacao.md) | Logs, métricas, tracing, SLOs e alertas, health checks, runbooks |

## Contratos

| Arquivo | O quê |
|---|---|
| [`contratos/openapi-bff.v1.json`](contratos/openapi-bff.v1.json) | OpenAPI do BFF, exportado da stack local |
| [`contratos/openapi-webapi.v1.json`](contratos/openapi-webapi.v1.json) | OpenAPI da WebApi, exportado da stack local |
| [`contratos/asyncapi-lancamentos.v1.yaml`](contratos/asyncapi-lancamentos.v1.yaml) | AsyncAPI 3.0 do evento de lançamento no Pub/Sub |

## Por onde começar

| Quem | Caminho |
|---|---|
| Quem avalia a arquitetura | SAD-Sol → C4 1 e 2 → ADRs de solução → NFRs de solução → riscos |
| Quem vai desenvolver | SAD-App → C4 3 e 4 → modelo de dados → contratos → testes e padrões |
| Quem vai operar | Implantação → observabilidade e runbooks → NFRs de solução (DR) |
| Segurança e privacidade | Segurança e threat model → dados macro (retenção) |

## Convenções

- **Medido, código, inferido, proposta.** Quando importa, o texto diz de onde vem a
  afirmação: de uma medição (com a fonte), da leitura do código, de uma inferência, ou se é
  uma recomendação ainda não implementada.
- **Status dos ADRs:** Aceita, Aceita e não executada, Proposta, Substituída.
- **Diagramas em Mermaid.** O C4 é desenhado em `flowchart`, porque o tipo C4 nativo do
  Mermaid ainda é experimental. Cores do C4: azul-escuro para pessoas, azul para o sistema e
  seus containers, azul-claro para componentes, cinza para o que é externo.
- **Nomes do código** aparecem como estão no código, sem tradução: `LancamentosDiarios`,
  `Stage`, `Cadastrado`.
- **Referências a código** apontam arquivo e classe, não número de linha, que muda.

## Glossário

| Termo | Significado |
|---|---|
| Lançamento | Um crédito ou débito numa conta, com valor, data de competência e observação |
| Conta (`ContaId`) | A unidade de saldo e de ordem. Cada usuário tem uma |
| Sequência | Número do lançamento dentro da conta, contínuo a partir de 1 |
| Estágio (`Stage`) | Onde o lançamento está: `Cadastrado`, `Lido`, `Enfileirado`, `EmProcessamento`, `Consolidado` ou `Erro` |
| Pendente | Lançamento nos quatro primeiros estágios: gravado, ainda fora do saldo |
| Saldo consolidado | A soma do que o Consumer já aplicou (`SaldoConsolidado`) |
| Saldo projetado | Consolidado mais créditos pendentes menos débitos pendentes |
| "Consolidado até o nº N" | `UltimaSequencia`: até onde o saldo está em dia |
| Outbox | O padrão de gravar o que publicar junto com o dado. Aqui, a própria linha, pela coluna `Stage` |
| Publicação imediata | A WebApi publica logo depois do commit, se a conta está em dia |
| Relay | O processo (projeto `.Events`) que publica o que ficou para trás |
| Reserva | Passar a linha de `Cadastrado` para `Lido` antes de publicar, para só um publicar |
| Reserva vencida | Linha em `Lido` há mais de 1 minuto; volta a ser candidata |
| Ordering key | Chave de ordenação do Pub/Sub. Aqui, a conta |
| Consumer | O processo que recebe o evento e soma ao saldo |
| Read model | Tabela feita para leitura, derivada da fonte da verdade |
| BFF | *Backend for frontend*: a API só da tela |
| Posse da conta | A regra de que o Cliente só alcança a própria conta |
| Policy | Conjunto de roles exigido por uma rota: `ClienteOuAdmin`, `SomenteAdmin` |
| Token de renovação | Segredo opaco de uso único que troca a sessão por uma nova |
| Lote | `POST /lancamentos/lote`: até 1.000 lançamentos de uma vez, só para Admin |
| Ciclo, rodada | Uma execução do relay: a cada 2 s no local, a cada minuto no GCP |
| *Dead-letter* (DLQ) | Tópico para onde vai a mensagem que falhou repetidamente |
| PITR | Recuperação pontual do banco num instante passado |
| RPO, RTO | Quanto dado se pode perder; quanto tempo até voltar |
| SLI, SLO | O indicador medido; a meta para ele |

## Divergências encontradas ao escrever estes documentos

Somam-se às que [requisitos-nao-funcionais.md](../requisitos-nao-funcionais.md#documentação-que-contradiz-o-código)
já lista.

| Onde | O que diz | O que o código faz |
|---|---|---|
| [`docs/fluxo_auth.mermeid`](../fluxo_auth.mermeid) e `lancamentos_diarios_segurança.jpg` | O BFF gera o JWT | Só a WebApi emite; o BFF valida e repassa. A extensão `.mermeid` também impede o GitHub de desenhar o diagrama: `.mmd`, ou um bloco `mermaid` num `.md` |
| `appsettings.json` da WebApi e do BFF | `"Chave": "MinhaChaveCompartilhada"`, desde o commit `45bcd17` | O README diz que a chave não fica no `appsettings.json`. Com 23 bytes a aplicação não sobe, então não é brecha; vale voltar a vazio |
| README, IAM da service account do relay | Três papéis | O `--set-secrets` do script também exige `secretmanager.secretAccessor`; o `cloudsql.client` não é necessário com IP privado direto |
| Comentário do script do relay | Duas execuções simultâneas poderiam publicar fora de ordem | A reserva não traz sucessor de lançamento em `Lido`, então a ordem se mantém. Duas execuções duplicariam trabalho e mensagens; manter uma tarefa e o timeout menor que o intervalo continua certo |
| OpenAPI gerado da WebApi | `/auth/sair` responde 200; `/auth/esqueci-senha` e `/auth/redefinir-senha` sem o status de sucesso | 204, 202 e 204 |
| Comentários em `LancamentosServiceCollectionExtensions` e `BancoOptions` | "Mesmo arquivo SQLite"; conexão "através do Auth Proxy" | SQL Server; IP privado direto |
| `ILancamentoRepository.MarcarErroAsync` | — | Ninguém chama |
| `LancamentoRepository.ConsolidarAsync` | — | Grava `UltimaSequencia` sem conferir se é maior: um reprocessamento fora de ordem faz o número voltar |
