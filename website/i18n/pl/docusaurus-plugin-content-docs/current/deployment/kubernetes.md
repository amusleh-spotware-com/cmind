---
description: "Helm chart: deploy/helm/cmind. Wdrażaj Web, MCP, samodzielnie rejestrujących agentów węzłów, opcjonalny Postgres wewnątrz klastra."
---

# Wdrażanie Kubernetes — krok po kroku

Helm chart: `deploy/helm/cmind`. Wdrażaj Web, MCP, samodzielnie rejestrujących agentów węzłów, opcjonalny Postgres wewnątrz klastra.

> **Zweryfikowano** od końca do końca na lokalnym klastrze `kind`: wszystkie pods osiągają `Ready`, agent węzła samodzielnie rejestruje się z nazwą DNS headless na pod, `/health` + `/version` zwracają 200, skalowany w dół agent automatycznie oznaczony nieosiągalny. Przepływ poniżej = co zostało przetestowane.

## 0. Wymagania wstępne

- Klaster Kubernetes (zarządzany EKS/AKS/GKE, lub lokalny `kind`/`k3d`/`minikube`).
- `kubectl` (wskazujący na cel kontekstu) i `helm` 3.
- Rejestr kontenerów, który klaster może pobrać (pomiń dla lokalnego `kind` — zamiast tego załaduj obrazy).

## 1. Kompiluj trzy obrazy

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Popchnij (`docker push <registry>/cmind-web:1.0.0`, itp.), **lub** dla lokalnego klastra `kind` załaduj bezpośrednio:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Wybierz sekrety

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 znaki; współdzielony sekret klastra do auto-odkrycia węzła
```

## 3. Zainstaluj chart

Na podstawie rejestru (klaster zarządzany):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Lokalny `kind` (załadowane obrazy, brak zewnętrznego Postgres, agenci bez uprzywilejów):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Na `kind`/containerd brak gniazda Docker hosta, więc `web.dockerSocket.enabled=false` (builder w aplikacji/LocalNode niedostępny) i `nodeAgent.privileged=false` (agent nadal **samodzielnie rejestruje się**; po prostu nie może uruchamiać kontenerów cTrader bez DinD). Do rzeczywistego wykonania obciążeń uruchom agentów na puli węzłów, gdzie `nodeAgent.privileged=true` jest dozwolony.

Brak binarnej `helm`? Renderuj i stosuj:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Czekaj na wdrażanie

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Oczekuj: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) i `cmind-node-agent-0` (StatefulSet) wszystko `Ready`. Readiness Web (`/health`) przechodzi tylko po migracji DB (migracje uruchamiają się przy uruchomieniu).

## 5. Zweryfikuj auto-odkrycie

```bash
# Agent węzła powinien pojawić się w DB z nazwą DNS headless na pod i IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Przykład (zweryfikowany):
