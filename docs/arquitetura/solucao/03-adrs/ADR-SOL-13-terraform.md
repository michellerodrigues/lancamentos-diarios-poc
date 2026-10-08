# ADR-SOL-13 — Infraestrutura como código com Terraform

- **Status:** Proposta. O README e o código já assumem Terraform (`PubSub:CriarRecursos=false`
  em produção), mas nenhum arquivo existe no repositório.

## Contexto

Hoje a única peça com deploy automatizado é o relay, por um script `gcloud`. Tópico,
subscription, banco, rede, Load Balancer e IAM não têm definição versionada. A aplicação
deliberadamente não cria tópico nem banco em produção.

## Decisão

- **Terraform** para todos os recursos do GCP: rede, Cloud SQL, Pub/Sub (tópico, subscription
  com ordenação, *dead-letter*), Secret Manager (os segredos, não os valores), Artifact
  Registry, Cloud Run (services, job e worker pool), Cloud Scheduler, Load Balancer, Cloud
  Armor, service accounts e IAM.
- **Estado remoto** num bucket do GCS com versionamento, um estado por ambiente.
- **A imagem não é gerenciada pelo Terraform**: o pipeline publica a revisão
  ([build e deploy](../../software/08-build-e-deploy.md#5-deploy)), e o Terraform ignora a
  mudança de imagem para não brigar com o deploy.
- **Valores de segredo fora do estado**: o Terraform cria o segredo; a versão com o valor é
  adicionada por fora.

O código de referência está em [implantação e infraestrutura](../07-implantacao-e-infraestrutura.md#7-iac-de-referência).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Scripts `gcloud`, como o do relay | Imperativos; não mostram a diferença entre o desejado e o existente |
| Pulumi | Infraestrutura em C#, o que agradaria ao time; ecossistema menor nos exemplos do GCP |
| Config Connector | Exige um cluster Kubernetes |

## Consequências

**Ganhos**
- Ambientes reproduzíveis; mudança de infraestrutura revisada em PR.
- O que a aplicação deixa de criar (`CriarRecursos=false`, `CriarBanco=false`) passa a ter
  dono.

**Custos**
- Estado a proteger e a travar entre execuções.
- Duas ferramentas no deploy: Terraform para a infraestrutura, `gcloud` para as revisões.

## Revisitar quando

O time adotar outra ferramenta corporativa de IaC.
