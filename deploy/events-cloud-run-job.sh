#!/usr/bin/env bash
#
# Publica o relay (.Events) como Cloud Run Job e cria o Cloud Scheduler que o dispara.
#
# Equivale, na AWS, a empacotar a Lambda e criar a regra do EventBridge Scheduler.
#
# CONEXAO COM O BANCO
#
# Nao existe Cloud SQL Connector para .NET — a biblioteca oficial cobre Go, Java,
# Python e Node. E o socket Unix de --add-cloudsql-instances atende MySQL e
# PostgreSQL, nao SQL Server.
#
# Sobra o caminho direto, que e tambem o mais simples: IP privado da instancia na
# porta 1433, com Direct VPC egress dando ao Job acesso a VPC. A connection string
# inteira vive no Secret Manager e e injetada como variavel no runtime, entao a
# senha nunca entra na imagem nem na linha de comando.
#
# Uso:
#   PROJECT_ID=meu-projeto \
#   VPC_NETWORK=default VPC_SUBNET=default \
#   CONNECTION_STRING_SECRET=lancamentos-connection-string \
#   ./deploy/events-cloud-run-job.sh
#
set -euo pipefail

PROJECT_ID="${PROJECT_ID:?defina PROJECT_ID}"
REGION="${REGION:-southamerica-east1}"
REPO="${REPO:-lancamentos}"
JOB="${JOB:-lancamentos-relay}"
SCHEDULER="${SCHEDULER:-${JOB}-trigger}"

# Cloud Scheduler nao aceita granularidade menor que 1 minuto. Com a publicacao
# imediata da WebApi cobrindo o caminho feliz, este Job e apenas a rede de
# protecao — entao 1 minuto e folgado.
SCHEDULE="${SCHEDULE:-* * * * *}"

SA="${SA:-lancamentos-relay@${PROJECT_ID}.iam.gserviceaccount.com}"
IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO}/events:$(date +%Y%m%d-%H%M%S)"

# Rede e sub-rede para o Direct VPC egress — precisam alcancar o IP privado do
# Cloud SQL.
VPC_NETWORK="${VPC_NETWORK:?defina VPC_NETWORK}"
VPC_SUBNET="${VPC_SUBNET:?defina VPC_SUBNET}"

# Secret do Secret Manager com a connection string completa. Exemplo do conteudo:
#   Server=10.20.0.5,1433;Database=Lancamentos;User Id=lancamentos-app;Password=...;Encrypt=True;TrustServerCertificate=False;
CONNECTION_STRING_SECRET="${CONNECTION_STRING_SECRET:?defina CONNECTION_STRING_SECRET}"

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$raiz"

echo "==> 1/4  build da imagem"
docker build \
  -f MeusLancamentosDiarios.Integrator.Events/Dockerfile \
  -t "$IMAGE" \
  .

echo "==> 2/4  push para o Artifact Registry"
gcloud auth configure-docker "${REGION}-docker.pkg.dev" --quiet
docker push "$IMAGE"

echo "==> 3/4  Cloud Run Job"
# --max-retries: o relay e idempotente (o WHERE por estagio protege), entao
#   reexecutar e seguro.
#
# --task-timeout MENOR que o intervalo do Scheduler (50s para um agendamento de
#   1 minuto): o Scheduler nao espera a execucao anterior terminar, e duas
#   instancias simultaneas poderiam pegar lancamentos da mesma conta e publica-los
#   fora de ordem. O padrao do Cloud Run Job e 10 minutos — sobreporia dez vezes.
#
# --tasks/--parallelism 1: paralelizar aqui tambem quebraria a ordem.
#
# Banco__CriarBanco=false: em producao o banco vem do provisionamento, e a
#   aplicacao nao deve ter permissao para cria-lo.
gcloud run jobs deploy "$JOB" \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --image "$IMAGE" \
  --service-account "$SA" \
  --network "$VPC_NETWORK" \
  --subnet "$VPC_SUBNET" \
  --vpc-egress private-ranges-only \
  --set-secrets "Banco__ConnectionString=${CONNECTION_STRING_SECRET}:latest" \
  --set-env-vars "PubSub__ProjectId=${PROJECT_ID},Relay__LoopContinuo=false,PubSub__CriarRecursos=false,Banco__CriarBanco=false" \
  --max-retries 3 \
  --task-timeout 50s \
  --tasks 1 \
  --parallelism 1

echo "==> 4/4  Cloud Scheduler"
# O Scheduler chama a API do Cloud Run para executar o Job, autenticando com OAuth
# pela service account. E preciso que ela tenha roles/run.invoker sobre o Job.
URI="https://run.googleapis.com/v2/projects/${PROJECT_ID}/locations/${REGION}/jobs/${JOB}:run"

if gcloud scheduler jobs describe "$SCHEDULER" --location "$REGION" --project "$PROJECT_ID" >/dev/null 2>&1; then
  acao=update
else
  acao=create
fi

gcloud scheduler jobs "$acao" http "$SCHEDULER" \
  --project "$PROJECT_ID" \
  --location "$REGION" \
  --schedule "$SCHEDULE" \
  --time-zone "America/Sao_Paulo" \
  --uri "$URI" \
  --http-method POST \
  --oauth-service-account-email "$SA"

echo
echo "pronto: $SCHEDULER dispara $JOB em '$SCHEDULE'"
echo "imagem: $IMAGE"
echo
echo "executar uma vez, na mao:"
echo "  gcloud run jobs execute $JOB --region $REGION --project $PROJECT_ID"
