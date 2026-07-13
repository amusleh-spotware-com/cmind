---
description: "Helm chart: deploy/helm/cmind. Deploy Web, MCP, self-registering node agent, optional in-cluster Postgres."
---

# Deployment Kubernetes — step by step

Helm chart: `deploy/helm/cmind`. Deploy Web, MCP, self-registering node agent, optional in-cluster Postgres.

> **Validated** end-to-end pada local `kind` cluster: semua pod reach `Ready`, node agent self-register dengan per-pod headless DNS name, `/health` + `/version` return 200, scaled-down agent auto-marked unreachable. Alur di bawah = apa yang tested.

## 0. Prerequisites

- Kubernetes cluster (managed EKS/AKS/GKE, atau local `kind`/`k3d`/`minikube`).
- `kubectl` (pointed ke target context) dan `helm` 3.
- Registry container cluster dapat pull dari (skip untuk local `kind` — load image sebagai gantinya).

## 1. Build tiga image

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, etc.), **atau** untuk local `kind` cluster load direct:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Pilih secret

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 char; shared cluster secret untuk node auto-discovery
```

## 3. Install chart

Registry-based (managed cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (loaded image, tidak ada external Postgres, non-privileged agent):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Di `kind`/containerd tidak ada host Docker socket, jadi `web.dockerSocket.enabled=false` (in-app builder/LocalNode unavailable) dan `nodeAgent.privileged=false` (agent masih **self-register**; hanya tidak dapat jalankan container cTrader tanpa DinD). Untuk real workload execution, jalankan agent di node pool di mana `nodeAgent.privileged=true` diizinkan.

Tidak ada `helm` binary? Render dan apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Tunggu rollout
