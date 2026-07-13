---
description: "Helm chart: deploy/helm/cmind. Wdraża Web, MCP, samorejestrujące się agenty węzłów, opcjonalny Postgres w klastrze."
---

# Wdrażanie Kubernetes — krok po kroku

Helm chart: `deploy/helm/cmind`. Wdraża Web, MCP, samorejestrujące się agenty węzłów, opcjonalny
Postgres w klastrze.

> **Zweryfikowane** end-to-end na lokalnym klastrze `kind`: wszystkie pody osiągają stan `Ready`, 
> agent węzła samorejestruje się z DNS-em headless dla każdego poda, `/health` + `/version` 
> zwracają 200, agent skalowany w dół automatycznie oznaczany jako nieosiągalny. Przepływ poniżej = 
> co zostało przetestowane.

## 0. Wymagania wstępne

- Klaster Kubernetes (zarządzany EKS/AKS/GKE, lub lokalny `kind`/`k3d`/`minikube`).
- `kubectl` (wskazany na docelowy kontekst) i `helm` 3.
- Rejestr kontenerów, z którego klaster może pobierać (pomiń dla lokalnego `kind` — zamiast tego załaduj obrazy).

## 1. Zbuduj trzy obrazy

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Wyślij (`docker push <registry>/cmind-web:1.0.0`, itd.), **lub** dla lokalnego klastra `kind` załaduj bezpośrednio:

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
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 znaki; wspólny sekret klastra do autodiscovery węzłów
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

Lokalny `kind` (załadowane obrazy, brak zewnętrznego Postgres, agenty bez uprawnień):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Na `kind`/containerd brak gniazda Docker na hoście, więc `web.dockerSocket.enabled=false`
> (konstruktor w aplikacji/LocalNode niedostępny) i `nodeAgent.privileged=false` (agent nadal
> **samorejestruje się**; po prostu nie może uruchamiać kontenerów cTrader bez DinD). Dla rzeczywistego
> wykonywania obciążenia uruchamiaj agenty na puli węzłów, gdzie `nodeAgent.privileged=true` jest dozwolone.

Brak binarki `helm`? Renderuj i aplikuj:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Czekaj na rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Oczekuj: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) i `cmind-node-agent-0`
(StatefulSet) wszystkie `Ready`. Gotowość Web (`/health`) przechodzi tylko po migracji bazy danych (migracje
uruchamiane przy starcie).

## 5. Weryfikuj autodiscovery

```bash
# Agent węzła powinien pojawić się w bazie danych z per-pod headless DNS BaseUrl i IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Przykład (zweryfikowany):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Skaluj pojemność dodając repliki — każdy nowy pod samorejestruje się w ciągu jednego interwału heartbeatu:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Reconciliation świeżości (zweryfikowana): zmniejsz agenta, przełącza się na `IsReachable=f` po
`discovery.heartbeatTtl`; skaluj w górę, wraca do stanu online.

## 6. Dostęp do UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — zaloguj się właścicielem seed'owanym
```

Dostęp zewnętrzny: ustaw `web.ingress.enabled=true`, `web.ingress.host` i TLS.

## Dlaczego agenty węzłów są StatefulSet

Główny węzeł wysyła prace do **konkretnego** agenta po adresie URL, więc każdy agent potrzebuje stabilnego,
indywidualnie adresowalnego DNS. Chart używa StatefulSet + headless Service; każdy pod
reklamuje `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` i samorejestruje się pod nazwą poda.
Ten sam mechanizm autodiscovery który używają bare cTrader CLI nodes —
zobacz [../operations/node-discovery.md](../operations/node-discovery.md).

## Skalowanie Web (backplane SignalR, S6)

Aplikacja Web = Blazor Server + SignalR (dashboard na żywo, logs hub). Aby uruchomić **więcej niż jedną replikę Web**,
ustaw connection string `signalr` na punkt końcowy Redis — aplikacja następnie rejestruje **SignalR Redis
backplane** (`AddStackExchangeRedis`) tak hub wiadomości i negotiation circuits fanuą się między replikami i
reconnect lądujący na innym podzie pozostaje na żywo. Brak connection stringu `signalr` = pojedyncza replika
in-memory (bez zmian). Połącz z session affinity na ingress dla gładkiego Blazor Server circuits.

## Autoskalowanie kopii-agenta i odporność

Copy-agent gości długotrwałe handlowe sockety, więc skaluje się na **pracę, nie CPU**. Z
`copyAgent.keda.enabled=true` chart instaluje KEDA `ScaledObject` który odpytuje Postgres o
liczbę uruchomionych profilów kopii i skaluje repliki tak każdy pod hostuje około `copyAgent.keda.profilesPerPod`
(domyślnie 25), między `minReplicas`/`maxReplicas`. KEDA czyta DB przez `TriggerAuthentication` powiązaną z
`copyAgent.keda.connectionSecretKey` kluczem sekretu. Gdy `copyAgent.replicas > 1` (lub KEDA skaluje powyżej 1)
chart również dodaje `topologySpreadConstraints` (rozprzestrzenianie się po węzłach) i `PodDisruptionBudget`
(`minAvailable: 1`); przy zmniejszaniu skali / aktualizacji rolling każdy pod zwalnia leasingi na `SIGTERM`
(`terminationGracePeriodSeconds`, domyślnie 30) tak survivor reklamuje natychmiast — zobacz
[scaling.md](scaling.md).

## Kluczowe wartości

| Wartość | Cel |
|---------|-----|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Współrzędne obrazu (`local` + `Never` dla kind). |
| `secrets.existingSecret` | Używaj zewnętrznego/sealed Secret zamiast wartości zarządzanych przez chart. |
| `postgres.enabled` | `true` = Postgres w klastrze (dev). `false` + `externalDatabase.connectionString` dla zarządzanej bazy (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA na CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Liczba agentów, uprzywilejowanie DinD, tryb, pojemność. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` dla konstruktora Web/LocalNode (tylko węzły Docker-runtime). |
| `observability.otlpEndpoint` | Wyślij logi+traces+metryki do kolektora OTLP. |

## Sondy

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapowane we wszystkich
środowiskach.

## In-cluster test suite

Uruchom test copy-trading jako Kubernetes `Job` dla wdrożonej aplikacji, więc regresja została wykryta
in-cluster tak jak lokalnie. Testy kopii potrzebują tylko Web + Postgres + cache tokenów — **brak**
uprzywilejowanych agentów węzłów.

Jedno-krotnie, powtarzalnie (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (brak sekretów)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # ponownie użyj bieżącego kube kontekstu
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Ręczne / CI wiring — **deterministic (domyślnie, brak sekretów):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** dodatkowo potrzebuje cache tokenów. cTrader **refresh tokens jednokrotne użycie**, więc cache
musi być **zapisywalny**: Job kopie Secret do emptyDir na `/app/secrets` przez init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # nigdy nie paczka w obraz
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Wartość | Cel |
|---------|-----|
| `tests.enabled` | Renderuj test `Job` (domyślnie `false`). |
| `tests.project` / `tests.filter` | Który projekt + `dotnet test --filter` uruchomić (domyślnie: deterministic). |
| `tests.copySecret` | Opcjonalny Secret z gitignored `openapi-*.local.json`; skopiowany do **zapisywalnego** emptyDir na `/app/secrets` dla live suite. Puste ⇒ brak montażu sekretu. |
| `tests.backoffLimit` | Liczba prób Job (domyślnie `0`). |

`LiveCopySecrets` idzie w górę z `/app` aby znaleźć `secrets/`; live testy pomijają czysto gdy cache
jest nieobecny. `Dockerfile.tests` oparty na SDK tak uruchamia te same asercje co lokalny `dotnet test` — zarówno
deterministic (`101 passed`) i pełne live (`8 passed`) suity zweryfikowane uruchamiające się wewnątrz tego
obrazu lokalnie przed wysłaniem.

## Teardown

```bash
helm -n cmind uninstall cmind        # lub: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # tylko lokalnie
```

## Uruchamianie in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` niezależny od OS. Konwertuje ścieżkę repo do formy natywnej (`cygpath -m`) tak Docker,
helm i kubectl rozwiązują ją na **Windows/git-bash** tak jak Linux/macOS — zweryfikowane end to end na Windows
(kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Środowisko | Polecenie |
|------------|----------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **lub** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferowany)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Preferuj WSL na Windows.** Uruchamianie wewnątrz WSL używa natywnych ścieżek Linux i integracji WSL Docker Desktop,
unikając wszystkich Edge cases translacji ścieżek — najsolidniejsza opcja. Potrzebuje `docker`, `kind`, `helm`,
`kubectl` i .NET SDK na WSL PATH (Docker Desktop dostarcza `docker`; zainstaluj resztę w distro,
np. `go install sigs.k8s.io/kind@latest`, helm/kubectl release binarki). `scripts/k8s-e2e.ps1`
wrapper wybiera WSL z `-Wsl`, wraca do git-bash w innym wypadku.

`kind` + `helm` samoinstalogible jeśli nieobecny (release binarki lub `choco install kind kubernetes-helm`);
nie traktuj jako niedostępne. Zobacz również [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
