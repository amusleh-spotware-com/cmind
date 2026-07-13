---
description: "Helm chart: deploy/helm/cmind. Web, MCP, kendi kendine kaydolan node ajanlarını ve isteğe bağlı küme içi Postgres'i dağıtır."
---

# Kubernetes dağıtımı — adım adım

Helm chart: `deploy/helm/cmind`. Web, MCP, kendi kendine kaydolan node ajanlarını ve isteğe bağlı
küme içi Postgres'i dağıtır.

> **Doğrulandı** yerel `kind` kümesinde uçtan uca: tüm pod'lar `Ready` durumuna ulaşır, node ajanı
> pod başına headless DNS adıyla kendi kendine kaydolur, `/health` + `/version` 200 döner, küçültülen
> ajan otomatik olarak ulaşılamaz işaretlenir. Aşağıdaki akış = test edilenler.

## 0. Ön koşullar

- Kubernetes kümesi (yönetilen EKS/AKS/GKE veya yerel `kind`/`k3d`/`minikube`).
- `kubectl` (hedef bağlama yönlendirilmiş) ve `helm` 3.
- Kümenin çekebileceği bir konteyner kayıt defteri (yerel `kind` için atlayın — bunun yerine imajları yükleyin).

## 1. Üç imajı derleyin

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

İtin (`docker push <registry>/cmind-web:1.0.0`, vb.), **veya** yerel `kind` kümesi için
doğrudan yükleyin:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Sırları seçin

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret for node auto-discovery
```

## 3. Chart'ı kurun

Kayıt defteri tabanlı (yönetilen küme):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Yerel `kind` (yüklenen imajlar, harici Postgres yok, ayrıcalıksız ajanlar):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> `kind`/containerd üzerinde host Docker soketi yoktur, bu yüzden `web.dockerSocket.enabled=false`
> (uygulama içi builder/LocalNode kullanılamaz) ve `nodeAgent.privileged=false` (ajan yine de
> **kendi kendine kaydolur**; yalnızca DinD olmadan cTrader konteynerlerini çalıştıramaz). Gerçek iş yükü
> yürütmesi için, `nodeAgent.privileged=true`'nun izinli olduğu bir node havuzunda ajanları çalıştırın.

`helm` ikili dosyası yok mu? Render edip uygulayın:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Yayınlamanın tamamlanmasını bekleyin

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Beklenen: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployment'lar) ve `cmind-node-agent-0`
(StatefulSet) hepsi `Ready`. Web hazırlığı (`/health`) yalnızca DB taşındıktan sonra geçer (taşımalar
başlangıçta çalışır).

## 5. Otomatik keşfi doğrulayın

```bash
# Node agent should appear in the DB with a per-pod headless DNS BaseUrl and IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Örnek (doğrulandı):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Replika ekleyerek kapasiteyi ölçeklendirin — her yeni pod bir kalp atışı aralığı içinde kendi kendine kaydolur:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Bayatlık uzlaştırması (doğrulandı): ajanı küçültün, `discovery.heartbeatTtl`'den sonra `IsReachable=f`'ye
döner; tekrar büyütün, çevrimiçi döner.

## 6. UI'ye ulaşın

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — sign in with the seeded owner
```

Harici erişim: `web.ingress.enabled=true`, `web.ingress.host` ve TLS ayarlayın.

## Node ajanları neden bir StatefulSet

Ana node işi **belirli** ajana URL ile gönderir, bu yüzden her ajanın kararlı, ayrı ayrı
adreslenebilir bir DNS adına ihtiyacı vardır. Chart, StatefulSet + headless Service kullanır; her pod
`http://<pod>.<svc>.<ns>.svc.cluster.local:8080` yayınlar ve pod adı altında kendi kendine kaydolur.
Çıplak cTrader CLI node'larının kullandığı aynı keşif mekanizması —
bkz. [../operations/node-discovery.md](../operations/node-discovery.md).

## Web ölçek genişletme (SignalR backplane, S6)

Web uygulaması = Blazor Server + SignalR (canlı gösterge paneli, günlük hub'ı). **Birden fazla Web replikası**
çalıştırmak için, `signalr` bağlantı dizesini bir Redis uç noktasına ayarlayın — uygulama daha sonra
**SignalR Redis backplane**'ini (`AddStackExchangeRedis`) kaydeder, böylece hub mesajları ve devre
müzakeresi replikalar arasında dağılır ve farklı bir pod'a inen bir yeniden bağlantı canlı kalır.
`signalr` bağlantı dizesi yok = tek replika bellek içi (değişmedi). En sorunsuz Blazor Server devreleri
için ingress'te oturum benzeşimi ile eşleştirin.

## Copy-agent otomatik ölçekleme ve dayanıklılık

Copy-agent uzun ömürlü ticaret soketlerini barındırır, bu yüzden **CPU'ya değil işe** göre ölçeklenir.
`copyAgent.keda.enabled=true` ile chart, çalışan copy-profil sayısını için Postgres'i sorgulayan ve her
pod'un yaklaşık `copyAgent.keda.profilesPerPod` (varsayılan 25) barındıracak şekilde replikaları
`minReplicas`/`maxReplicas` arasında ölçeklendiren bir KEDA `ScaledObject` kurar. KEDA, DB'yi
`copyAgent.keda.connectionSecretKey` sır anahtarına bağlı `TriggerAuthentication` aracılığıyla okur.
`copyAgent.replicas > 1` olduğunda (veya KEDA 1'in üzerine ölçeklendiğinde) chart ayrıca
`topologySpreadConstraints` (node'lar arasında dağıtım) ve `PodDisruptionBudget` (`minAvailable: 1`)
ekler; ölçek içe / yuvarlanan güncelleme sırasında her pod `SIGTERM`'de kiralamaları serbest bırakır
(`terminationGracePeriodSeconds`, varsayılan 30), böylece hayatta kalan hemen geri alır — bkz.
[scaling.md](scaling.md).

## Ana değerler

| Değer | Amaç |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | İmaj koordinatları (kind için `local` + `Never`). |
| `secrets.existingSecret` | Chart tarafından yönetilen değerler yerine harici/mühürlü Secret kullanın. |
| `postgres.enabled` | `true` = küme içi Postgres (dev). Yönetilen DB (prod) için `false` + `externalDatabase.connectionString`. |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, CPU üzerinde HPA. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Ajan sayısı, DinD ayrıcalığı, mod, kapasite. |
| `web.dockerSocket.enabled` | Web builder/LocalNode için hostPath `/var/run/docker.sock` (yalnızca Docker-runtime node'ları). |
| `observability.otlpEndpoint` | Günlükleri+izleri+metrikleri OTLP toplayıcısına gönderin. |

## Sondalar

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (ajan) — tüm
ortamlarda eşlenir.

## Küme içi test paketi

Copy-trading paketini dağıtılan uygulamaya karşı bir Kubernetes `Job` olarak çalıştırın, böylece
regresyon yerelde olduğu gibi küme içinde de yakalanır. Copy testlerine yalnızca Web + Postgres + token
önbelleği gerekir — ayrıcalıklı node ajanları **gerekmez**.

Tek seferlik, tekrarlanabilir (kind up → imajları derle+yükle → dağıt → Job çalıştır → çıkış 0'ı doğrula → yıkım):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (no secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manuel / CI bağlantısı — **deterministik (varsayılan, sır yok):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Canlı paket** ek olarak token önbelleğine ihtiyaç duyar. cTrader **yenileme token'ları tek kullanımlıktır**,
bu yüzden önbelleğin **yazılabilir** olması gerekir: Job, bir init-konteyner aracılığıyla Secret'ı
`/app/secrets`'teki emptyDir'e kopyalar.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # never baked into the image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Değer | Amaç |
|-------|---------|
| `tests.enabled` | Test `Job`'unu render et (varsayılan `false`). |
| `tests.project` / `tests.filter` | Hangi proje + çalıştırılacak `dotnet test --filter` (varsayılan: deterministik). |
| `tests.copySecret` | gitignore'lu `openapi-*.local.json` içeren isteğe bağlı Secret; canlı paket için `/app/secrets`'teki **yazılabilir** emptyDir'e kopyalanır. Boş ⇒ sır bağlaması yok. |
| `tests.backoffLimit` | Job yeniden deneme sayısı (varsayılan `0`). |

`LiveCopySecrets`, `secrets/`'i bulmak için `/app`'ten yukarı doğru yürür; canlı testler önbellek yoksa
temiz atlar. `Dockerfile.tests` SDK tabanlıdır, bu yüzden yerel `dotnet test` ile aynı doğrulamaları
çalıştırır — hem deterministik (`101 passed`) hem de tam canlı (`8 passed`) paketleri, gönderilmeden önce
Docker'a karşı bu imajın içinde yerel olarak çalışırken doğrulandı.

## Yıkım

```bash
helm -n cmind uninstall cmind        # or: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local only
```

## Küme içi paketi platformlar arası çalıştırma (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` işletim sisteminden bağımsızdır. Repo yolunu yerel forma dönüştürür (`cygpath -m`),
böylece Docker, helm ve kubectl bunu Linux/macOS'un yanı sıra **Windows/git-bash**'te de çözer — Windows'ta
uçtan uca doğrulandı (kind kümesi kurulur → imajlar derlenir+yüklenir → chart dağıtılır → küme içi test
Job yeşil → yıkım).

| Ortam | Komut |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **veya** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (tercih edilen)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Windows'ta WSL'yi tercih edin.** WSL içinde çalıştırmak yerel Linux yollarını ve Docker Desktop'ın WSL
entegrasyonunu kullanır, tüm yol-çevirme uç durumlarından kaçınır — en sağlam seçenek. WSL PATH'inde
`docker`, `kind`, `helm`, `kubectl` ve .NET SDK gerektirir (Docker Desktop `docker`'ı sağlar; gerisini
dağıtımda kurun, örn. `go install sigs.k8s.io/kind@latest`, helm/kubectl sürüm ikili dosyaları).
`scripts/k8s-e2e.ps1` sarmalayıcısı `-Wsl` ile WSL'yi seçer, aksi halde git-bash'e geri döner.

`kind` + `helm` yoksa kendi kendine kurulabilir (sürüm ikili dosyaları veya `choco install kind kubernetes-helm`);
kullanılamaz olarak değerlendirmeyin. Ayrıca bkz. [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
