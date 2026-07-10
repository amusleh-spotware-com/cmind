#!/usr/bin/env bash
# Reproducible in-cluster copy-trading test run.
#
# Spins up a local kind cluster (or reuses an existing kube context), builds + loads the app and
# test images, deploys the Helm chart, creates the copy-trading token-cache Secret from ./secrets,
# runs the test Job, streams its logs, and asserts it exits 0. Idempotent and CI-friendly.
#
# Usage:
#   scripts/k8s-e2e.sh                 # kind cluster, live copy suite
#   TEST_FILTER='FullyQualifiedName~CopyTrading' scripts/k8s-e2e.sh   # deterministic suite (no secrets)
#   USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh   # reuse current kube context, skip kind create/delete
#
# Env:
#   CLUSTER_NAME   kind cluster name (default: cmind-e2e)
#   RELEASE        helm release name (default: cmind)
#   NAMESPACE      k8s namespace (default: cmind)
#   IMAGE_REPO     image repo prefix (default: cmind); images: <repo>-web, <repo>-tests, ...
#   IMAGE_TAG      image tag (default: e2e)
#   TEST_FILTER    dotnet test filter (default: FullyQualifiedName~CopyTradingLiveTests)
#   KEEP_CLUSTER   1 to skip teardown on exit
set -euo pipefail

CLUSTER_NAME="${CLUSTER_NAME:-cmind-e2e}"
RELEASE="${RELEASE:-cmind}"
NAMESPACE="${NAMESPACE:-cmind}"
IMAGE_REGISTRY="${IMAGE_REGISTRY:-kind.local}"
IMAGE_REPO="${IMAGE_REPO:-cmind}"
IMAGE_TAG="${IMAGE_TAG:-e2e}"
IMG="${IMAGE_REGISTRY}/${IMAGE_REPO}"
TEST_FILTER="${TEST_FILTER:-FullyQualifiedName~CopyTrading}"   # deterministic by default (no secrets)
USE_EXISTING_CLUSTER="${USE_EXISTING_CLUSTER:-0}"
KEEP_CLUSTER="${KEEP_CLUSTER:-0}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHART="$ROOT/deploy/helm/cmind"
COPY_SECRET="cmind-copy-secrets"

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

cleanup() {
  if [[ "$USE_EXISTING_CLUSTER" != "1" && "$KEEP_CLUSTER" != "1" ]]; then
    log "Deleting kind cluster $CLUSTER_NAME"
    kind delete cluster --name "$CLUSTER_NAME" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

if [[ "$USE_EXISTING_CLUSTER" != "1" ]]; then
  log "Creating kind cluster $CLUSTER_NAME"
  kind get clusters | grep -qx "$CLUSTER_NAME" || kind create cluster --name "$CLUSTER_NAME"
fi

log "Building images (web, tests)"
docker build -f "$ROOT/Dockerfile.web"   -t "${IMG}-web:${IMAGE_TAG}"   "$ROOT"
docker build -f "$ROOT/Dockerfile.tests" -t "${IMG}-tests:${IMAGE_TAG}" "$ROOT"

if [[ "$USE_EXISTING_CLUSTER" != "1" ]]; then
  log "Loading images into kind"
  kind load docker-image "${IMG}-web:${IMAGE_TAG}"   --name "$CLUSTER_NAME"
  kind load docker-image "${IMG}-tests:${IMAGE_TAG}" --name "$CLUSTER_NAME"
fi

kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

if [[ -d "$ROOT/secrets" ]]; then
  log "Creating token-cache secret $COPY_SECRET from ./secrets"
  kubectl -n "$NAMESPACE" create secret generic "$COPY_SECRET" \
    --from-file="$ROOT/secrets" --dry-run=client -o yaml | kubectl apply -f -
else
  log "No ./secrets directory — the live suite will skip; deterministic suite still runs"
fi

log "Deploying chart (Web + Postgres only; node agents not needed for copy tests)"
helm upgrade --install "$RELEASE" "$CHART" -n "$NAMESPACE" \
  --set image.registry="$IMAGE_REGISTRY" --set image.repository="$IMAGE_REPO" \
  --set image.tag="$IMAGE_TAG" --set image.pullPolicy=Never \
  --set nodeAgent.enabled=false --set mcp.replicas=0 --set web.replicas=1 \
  --set web.dockerSocket.enabled=false \
  --wait --timeout 5m

log "Enabling test Job"
helm upgrade "$RELEASE" "$CHART" -n "$NAMESPACE" --reuse-values \
  --set tests.enabled=true --set tests.filter="$TEST_FILTER" --set tests.copySecret="$COPY_SECRET"

JOB="job/${RELEASE}-cmind-tests"
log "Waiting for $JOB"
kubectl -n "$NAMESPACE" wait --for=condition=complete --timeout=15m "$JOB" &
WAIT_OK=$!
kubectl -n "$NAMESPACE" wait --for=condition=failed --timeout=15m "$JOB" &
WAIT_FAIL=$!
wait -n "$WAIT_OK" "$WAIT_FAIL" || true

log "Test Job logs"
kubectl -n "$NAMESPACE" logs "$JOB" --tail=-1 | tee /tmp/cmind-tests.log

if kubectl -n "$NAMESPACE" get "$JOB" -o jsonpath='{.status.succeeded}' | grep -qx 1; then
  grep -Eq 'Passed!|copied=True' /tmp/cmind-tests.log \
    && { log "PASS: copy-trading suite green in-cluster"; exit 0; }
fi

log "FAIL: test Job did not complete successfully"
exit 1
