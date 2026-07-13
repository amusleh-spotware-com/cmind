---
description: "Helm chart: deploy/helm/cmind. MenempatkancMind, MCP, ejen nod pendaftaran diri, Postgres dalam kluster pilihan."
---

# Penempatan Kubernetes — langkah demi langkah

Carta Helm: `deploy/helm/cmind`. MenempatkancMind, MCP, ejen nod pendaftaran diri, Postgres dalam kluster pilihan.

> **Disahkan** dari hujung ke hujung pada kluster `kind` lokal: semua pod mencapai `Ready`, ejen nod mendaftar diri dengan nama DNS headless setiap pod, `/health` + `/version` mengembalikan 200, ejen skala turun ditanda tidak dapat dicapai secara automatik. Aliran di bawah = apa yang diuji.

## 0. Prasyarat

- Kluster Kubernetes (EKS/AKS/GKE yang diurus, atau `kind`/`k3d`/`minikube` lokal).
- `kubectl` (ditunjuk pada konteks sasaran) dan `helm` 3.
- Daftar kontena kluster boleh tarik dari (langkau untuk `kind` lokal — muat gambar sebaliknya).

## 1. Bina tiga imej

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Tolak (`docker push <registry>/cmind-web:1.0.0`, dll.), **atau** untuk kluster `kind` lokal muat langsung:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Pilih rahasia

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; rahsia kluster bersama untuk penemuan nod otomatis
```

## 3. Pasang carta

Berasaskan daftar (kluster yang diurus):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

`kind` lokal (imej dimuat, tiada Postgres luaran, ejen tanpa keistimewaan):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Pada `kind`/containerd tiada soket Docker hos, jadi `web.dockerSocket.enabled=false` (pembina dalam apl/Nod Lokal tidak tersedia) dan `nodeAgent.privileged=false` (ejen masih **mendaftar diri**; hanya tidak boleh menjalankan bekas cTrader tanpa DinD). Untuk pelaksanaan beban kerja nyata, jalankan ejen pada kumpulan nod di mana `nodeAgent.privileged=true` dibenarkan.

Tiada binari `helm`? Render dan gunakan:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Tunggu rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Jangkaan: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Penempatan) dan `cmind-node-agent-0` (StatefulSet) semua `Ready`. Kesiapan web (`/health`) lulus hanya setelah DB dimigrasikan (migrasi berjalan pada permulaan).

## 5. Sahkan penemuan otomatis

```bash
# Ejen nod harus muncul dalam DB dengan URL DNS headless setiap pod dan IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Contoh (disahkan):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Kapaiti skala dengan menambah replika — setiap pod baru mendaftar diri dalam satu selang jantung:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Penyelarasan keusangan (disahkan): skala ejen turun, bertukar kepada `IsReachable=f` selepas `discovery.heartbeatTtl`; skala semula, kembali dalam talian.

## 6. Jangkau UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — masuk dengan pemilik ditanam
```

Akses luaran: tetapkan `web.ingress.enabled=true`, `web.ingress.host`, dan TLS.

## Mengapa ejen nod adalah StatefulSet

Nod utama menghantar kerja ke ejen **spesifik** mengikut URL, jadi setiap ejen memerlukan nama DNS yang stabil dan boleh ditangani secara individu. Carta menggunakan StatefulSet + Perkhidmatan headless; setiap pod mengiklankan `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` dan mendaftar diri di bawah nama pod. Mekanisme penemuan yang sama digunakan nod cTrader CLI telanjang — lihat [../operations/node-discovery.md](../operations/node-discovery.md).

## Skala web (Backplane SignalR, S6)

Apl web = Blazor Server + SignalR (papan pemuka langsung, hub log). Untuk menjalankan **lebih daripada satu replika web**, tetapkan rentetan sambungan `signalr` kepada titik akhir Redis — apl kemudian mendaftar **backplane Redis SignalR** (`AddStackExchangeRedis`) jadi mesej hab dan rundingan litar peminat merentas replika dan sambung semula mendarat di pod yang berbeza kekal langsung. Tiada rentetan sambungan `signalr` = replika tunggal dalam memori (tidak berubah). Pasangkan dengan pertalian sesi di ingress untuk litar Blazor Server yang paling lancar.

## Autoscaling ejen salin & ketahanan

Hos ejen salin soket perdagangan hidup panjang, jadi skala berdasarkan **kerja, bukan CPU**. Dengan `copyAgent.keda.enabled=true` carta memasang KEDA `ScaledObject` yang menanya Postgres untuk bilangan profil salin yang berjalan dan skala replika supaya setiap pod hos kira-kira `copyAgent.keda.profilesPerPod` (lalai 25), antara `minReplicas`/`maxReplicas`. KEDA membaca DB melalui `TriggerAuthentication` terikat kepada kunci rahsia `copyAgent.keda.connectionSecretKey`. Apabila `copyAgent.replicas > 1` (atau KEDA skala melepasi 1) carta juga menambah `topologySpreadConstraints` (tersebar merentas nod) dan `PodDisruptionBudget` (`minAvailable: 1`); skala ke bawah / pembaruan bergulir setiap pod melepaskan pajakan pada `SIGTERM` (`terminationGracePeriodSeconds`, lalai 30) jadi penyelamat menuntut semula segera — lihat [scaling.md](scaling.md).

## Nilai kunci

| Nilai | Tujuan |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Koordinat imej (`local` + `Never` untuk kind). |
| `secrets.existingSecret` | Gunakan Rahsia luaran/tertutup sebaliknya nilai yang diurus carta. |
| `postgres.enabled` | `true` = Postgres dalam kluster (dev). `false` + `externalDatabase.connectionString` untuk DB yang diurus (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA pada CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Bilangan ejen, keistimewaan DinD, mod, kapaiti. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` untuk pembina web/Nod Lokal (nod runtime Docker sahaja). |
| `observability.otlpEndpoint` | Hantar log+surih+metrik kepada pengumpul OTLP. |

## Siasatan

liveness `/alive`, kesiapan `/health` (Web) · `/version` (MCP) · `/health` (ejen) — dipetakan dalam semua persekitaran.

## Rangkaian ujian dalam kluster

Jalankan rangkaian perdagangan salin sebagai Kubernetes `Job` terhadap apl yang digunakan, jadi regresi ditangkap dalam kluster sama seperti tempatan. Ujian salin memerlukan hanya Web + Postgres + cache token — **tiada** ejen nod yang berkeistimewaan.

Sekali, boleh direproduksi (kind up → bina+muat imej → gunakan → jalankan Job → tegaskan keluar 0 → robohkan):

```bash
scripts/k8s-e2e.sh                                   # rangkaian salin deterministik (tiada rahsia)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # guna semula konteks kube semasa
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # langsung
```

Manual / Wayaran CI — **deterministik (lalai, tiada rahsia):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # imej pelari (SDK + projek ujian terbina)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Rangkaian langsung** perlulah cache token tambahan. Token segar **cTrader tunggal-guna**, jadi cache mestilah **boleh ditulis**: Kerja menyalin Rahsia ke emptyDir di `/app/secrets` melalui init-bekas.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # tidak pernah dipanggang ke dalam imej
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Nilai | Tujuan |
|-------|---------|
| `tests.enabled` | Render ujian `Job` (lalai `false`). |
| `tests.project` / `tests.filter` | Projek mana + `dotnet test --filter` untuk dijalankan (lalai: deterministik). |
| `tests.copySecret` | Rahsia pilihan dengan `openapi-*.local.json` yang diabaikan; disalin ke emptyDir **boleh ditulis** di `/app/secrets` untuk rangkaian langsung. Kosong ⇒ tiada pemasangan rahsia. |
| `tests.backoffLimit` | Kiraan percubaan semula Kerja (lalai `0`). |

`LiveCopySecrets` berjalan dari `/app` untuk mencari `secrets/`; ujian langsung langkau dengan bersih apabila cache tiada. `Dockerfile.tests` berasaskan SDK jadi menjalankan penegasan yang sama seperti `dotnet test` lokal — kedua-dua rangkaian deterministik (`101 passed`) dan rangkaian langsung penuh (`8 passed`) disahkan berjalan dalam imej ini tempatan terhadap Docker sebelum penghantar.

## Robohkan

```bash
helm -n cmind uninstall cmind        # atau: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # lokal sahaja
```

## Menjalankan rangkaian dalam kluster merentas platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` bebas OS. Menukar laluan repo kepada bentuk asli (`cygpath -m`) jadi Docker, helm dan kubectl menyelesaikannya pada **Windows/git-bash** serta Linux/macOS — disahkan dari hujung ke hujung pada Windows (kluster kind naik → imej terbina+dimuat → carta diguna → Kerja ujian dalam kluster hijau → robohkan).

| Persekitaran | Perintah |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **atau** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (dikutamakan)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Kahani WSL pada Windows.** Berlari dalam WSL menggunakan laluan Linux asli dan integrasi WSL Docker Desktop, mengelakkan semua kes tepi terjemahan laluan — pilihan paling kukuh. Perlukan `docker`, `kind`, `helm`, `kubectl` dan .NET SDK pada WSL PATH (Docker Desktop menyediakan `docker`; pasang baki dalam distro, cth. `go install sigs.k8s.io/kind@latest`, binari keluaran helm/kubectl). Pembungkus `scripts/k8s-e2e.ps1` memilih WSL dengan `-Wsl`, kembali ke git-bash sebaliknya.

`kind` + `helm` boleh dipasang sendiri jika tiada (binari keluaran atau `choco install kind kubernetes-helm`); jangan anggap sebagai tidak tersedia. Lihat juga [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
