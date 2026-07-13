---
description: "Helm chart: deploy/helm/cmind. Ανάπτυξη Web, MCP, αυτο-εγγραφή πράκτορες κόμβων, προαιρετικό σε-cluster Postgres."
---

# Ανάπτυξη Kubernetes — βήμα προς βήμα

Helm chart: `deploy/helm/cmind`. Ανάπτυξη Web, MCP, αυτο-εγγραφή πράκτορες κόμβων, προαιρετικό
σε-cluster Postgres.

> **Επαληθευμένο** άκρη-άκρη σε τοπικό `kind` cluster: όλα pods φτάσουν `Ready`, πράκτορα κόμβων
> αυτο-εγγράφονται με ανά-pod headless όνομα DNS, `/health` + `/version` επιστροφή 200, κλιμακα-κάτω
> πράκτορα αυτο-μέρκι unreachable. Ροή παρακάτω = τι δοκιμάστηκε.

## 0. Προαπαιτούμενα

- Kubernetes cluster (διαχειριζόμενο EKS/AKS/GKE, ή τοπικό `kind`/`k3d`/`minikube`).
- `kubectl` (που δείχνει κατά στόχο πλαίσιο) και `helm` 3.
- Κατάστημα κοντέινερ cluster μπορεί να τραβήξει από (παράλειψη για τοπικό `kind` — φόρτωση εικόνες αντ 'αυτού).

## 1. Κτίσουν τις τρεις εικόνες

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, κ.λπ.), **ή** για τοπικό `kind` cluster φόρτωση
άμεση:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Επιλέξτε μυστικά

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 χαρακτήρες. διαμοιρασμένο cluster μυστικό για κόμβο αυτο-ανακάλυψη
```

## 3. Εγκατάσταση της γραφής

Βάση κατάστημα (διαχειριζόμενο cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Τοπικό `kind` (φορτωμένες εικόνες, χωρίς εξωτερικό Postgres, μη-προνόμια πράκτορες):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Σε `kind`/containerd δεν υπάρχει κεντρικός υπολογιστής Docker υποδοχή, έτσι `web.dockerSocket.enabled=false`
> (σε-εφαρμογή κατασκευαστή/LocalNode αναφορά) και `nodeAgent.privileged=false` (πράκτορα ακόμα
> **αυτο-εγγράφονται**. απλώς δεν μπορούν να τρέχουν cTrader κοντέινερ χωρίς DinD). Για πραγματική φορτίο
> εκτέλεση, τρέχουν πράκτορες σε κόμβο χώρο όπου `nodeAgent.privileged=true` επιτρέπεται.

Χωρίς `helm` δυαδική; Κάνουν και εφαρμογή:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Περιμένω για ανάπτυξη rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Περιμένω: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) και `cmind-node-agent-0`
(StatefulSet) όλα `Ready`. Web readiness (`/health`) περάσματα μόνο μόλις DB μετανάστευση (μεταναστεύσεις
τρέχουν στο startup).

## 5. Επαλήθευση αυτο-ανακάλυψη

```bash
# Ο πράκτορας κόμβων θα πρέπει να εμφανιστεί στη DB με ένα ανά-pod headless DNS BaseUrl και IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Παράδειγμα (επαληθευμένο):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Κλιμάκωση χωρητικότητας με προσθήκη αντιγράφων — κάθε νέα αecho αυτο-εγγράφονται εντός ενός διαστήματος καρδιακής συχνότητας:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Stale reconciliation (επαληθευμένο): κλιμάκωση πράκτορα κάτω, flips σε `IsReachable=f` μετά
`discovery.heartbeatTtl`. κλιμάκωση πίσω δεν, επιστροφές online.

## 6. Φτάσουν το UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — συνδεθείτε με τον σπόρο ιδιοκτήτη
```

Εξωτερική πρόσβαση: ορίστε `web.ingress.enabled=true`, `web.ingress.host`, και TLS.

## Γιατί πράκτορες κόμβων είναι StatefulSet

Ο κύριος κόμβος στέλνει δουλειά σε **συγκεκριμένο** πράκτορα μέσω URL, έτσι κάθε πράκτορα χρειάζεται σταθερό,
ξεχωριστά-addressable DNS όνομα. Chart χρησιμοποιεί StatefulSet + headless Service. κάθε αecho
διαφημίζει `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` και αυτο-εγγράφονται κάτω όνομα αecho.
Ίδια ανακάλυψη μηχανισμό γυμνό cTrader CLI κόμβοι χρησιμοποιούν —
δείτε [../operations/node-discovery.md](../operations/node-discovery.md).

## Web κλιμάκωση-έξω (SignalR backplane, S6)

Web app = Blazor Server + SignalR (ζωντανό σταθμό, logs hub). Να τρέχει **περισσότερο ένα Web αντίγραφο**,
ορίστε `signalr` συμβολοσειρά σύνδεσης στο Redis endpoint — εφαρμογή στη συνέχεια εγγράφονται **SignalR Redis
backplane** (`AddStackExchangeRedis`) έτσι hub μηνύματα και διαπραγμάτευση κυκλώματος fan σε replicas και ένα
ξανασυνδεθείτε προσγείωση σε διάφορο αecho μένει ζωντανό. Χωρίς `signalr` συμβολοσειρά σύνδεσης = ενιαίας-replicas
σε-μνήμη (αμετάβλητο). Ζεύγος με session affinity κατά ingress για ομαλότερα Blazor Server κυκλώματα.

## Αντιγραφή-πράκτορα autoscaling & ανθεκτικότητα

Αντιγραφή-πράκτορα κεντρικός υπολογιστής μακροχρόνιες διαπραγμάτευση sockets, έτσι κλιμάκες σε **εργασία, όχι CPU**. Με
`copyAgent.keda.enabled=true` chart εγκαθιστά KEDA `ScaledObject` που ερωτήσεις Postgres για
τρέχουν αντιγραφή-profile count και κλιμάκες replicas έτσι κάθε αecho κεντρικός υπολογιστής άλλα `copyAgent.keda.profilesPerPod`
(προεπιλογή 25), μεταξύ `minReplicas`/`maxReplicas`. KEDA διαβάζει DB μέσω `TriggerAuthentication` δεμένο σε
`copyAgent.keda.connectionSecretKey` μυστικό κλειδί. Όταν `copyAgent.replicas > 1` (ή KEDA κλιμάκες πολύ 1)
chart επίσης προσθέτει `topologySpreadConstraints` (έξω κατά κόμβοι) και `PodDisruptionBudget`
(`minAvailable: 1`). σε κλιμάκα-μέσα / έλασης ανανέωση κάθε αecho απελευθερώνει leases σε `SIGTERM`
(`terminationGracePeriodSeconds`, προεπιλογή 30) έτσι επιζών ανακτά αμέσως — δείτε
[scaling.md](scaling.md).

## Κλειδί τιμές

| Τιμή | Σκοπός |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Εικόνα συντεταγμένες (`local` + `Never` για kind). |
| `secrets.existingSecret` | Χρησιμοποιήστε εξωτερικό/sealed Secret αντί chart-διαχειριζόμενες τιμές. |
| `postgres.enabled` | `true` = σε-cluster Postgres (dev). `false` + `externalDatabase.connectionString` για διαχειριζόμενη DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA σε CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Πράκτορα μέτρημα, DinD προνόμια, τρόπος, χωρητικότητα. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` για Web κατασκευαστή/LocalNode (Docker-runtime κόμβοι μόνο). |
| `observability.otlpEndpoint` | Πλοία logs+traces+metrics στο OTLP συλλέκτη. |

## Δοκιμές

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (πράκτορα) — χαρτογραφημένο σε όλα
περιβάλλοντα.

## Σε-cluster δοκιμή σουίτα

Τρέξουν αντιγραφή-διαπραγμάτευση σουίτα ως Kubernetes `Job` κατά ανάπτυξη εφαρμογή, έτσι regression πιασμένη
σε-cluster ίδιο ως τοπικά. Αντιγραφή δοκιμές χρειάζονται μόνο Web + Postgres + token cache — **χωρίς**
προνόμια κόμβος πράκτορες.

Μία-βολή, επαναλήψιμο (kind up → κτίσουν+φόρτωση εικόνες → ανάπτυξη → τρέξουν Job → έκθεση έξοδος 0 → σκίασμα κάτω):

```bash
scripts/k8s-e2e.sh                                   # ντετερμινιστικό αντιγραφή σουίτα (χωρίς μυστικά)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # αναχρησιμοποίηση τρέχων kube πλαίσιο
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # ζωντανό
```

Χειρόμηνο / CI καλώδια — **ντετερμινιστικό (προεπιλογή, χωρίς μυστικά):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # τρέχοντας εικόνα (SDK + κατασκευασμένο δοκιμή έργα)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Ζωντανή σουίτα** επιπλέον χρειάζεται token cache. cTrader **ανανέωση tokens ενιαίας χρησιμοποίησης**, έτσι cache
πρέπει να είναι **writable**: Job αντίγραφα Secret σε emptyDir κατά `/app/secrets` μέσω init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # ποτέ ψήφισμα σε εικόνα
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Τιμή | Σκοπός |
|-------|---------|
| `tests.enabled` | Κάνουν δοκιμή `Job` (προεπιλογή `false`). |
| `tests.project` / `tests.filter` | Ποιο έργο + `dotnet test --filter` να τρέχουν (προεπιλογή: ντετερμινιστικό). |
| `tests.copySecret` | Προαιρετικό Secret με gitignored `openapi-*.local.json`. αντίγραφα σε **writable** emptyDir κατά `/app/secrets` για ζωντανή σουίτα. Κενό ⇒ χωρίς μυστικό προσάρτημα. |
| `tests.backoffLimit` | Job retry μέτρημα (προεπιλογή `0`). |

`LiveCopySecrets` περπατά έως από `/app` για εύρεση `secrets/`. ζωντανή δοκιμές παράλειψη καθαρά όταν cache
απόν. `Dockerfile.tests` SDK-βάση έτσι τρέχουν ίδια έκθεση ως τοπικό `dotnet test` — και τα δύο
ντετερμινιστικό (`101 περάσε`) και πλήρης ζωντανό (`8 περάσε`) σουίτες επαληθευμένο τρέχοντας μέσα αυτό
εικόνα τοπικά κατά Docker πριν αποστολή.

## Σκίασμα κάτω

```bash
helm -n cmind uninstall cmind        # ή: kubectl delete -f <αποδίδεται>.yaml
kind delete cluster --name cmind     # τοπικό μόνο
```

## Τρέχουν τη σε-cluster σουίτα σταυρό-πλατφόρμας (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-ανεξάρτητο. Μετατρέπει αποθετήριο διαδρομή σε ντόπι φόρμα (`cygpath -m`) έτσι Docker,
helm και kubectl λύσουν το σε **Windows/git-bash** καθώς και Linux/macOS — επαληθευμένο άκρη-άκρη σε Windows
(kind cluster up → εικόνες κατασκευάστηκαν+φορτώθηκαν → chart ανάπτυξη → σε-cluster δοκιμή Job πράσινο → σκίασμα κάτω).

| Περιβάλλον | Εντολή |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **ή** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (προτιμώμενο)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Προτιμώμενο WSL σε Windows.** Τρέχοντας μέσα WSL χρησιμοποιεί ντόπι Linux διαδρομές και Docker Desktop's WSL ενσωμάτωση,
αποφεύγοντας όλη διαδρομή-μετάφραση ακμή περιπτώσεις — πιο ισχυρή δραστηριότητα. Χρειάζεται `docker`, `kind`, `helm`,
`kubectl` και .NET SDK σε WSL PATH (Docker Desktop παρέχει `docker`. εγκατάστηση υπόλοιπη σε distro,
π.χ. `go install sigs.k8s.io/kind@latest`, το helm/kubectl απελευθέρωση δυαδικά). `scripts/k8s-e2e.ps1`
περικοπή επιλέγει WSL με `-Wsl`, fallback σε git-bash διαφορετικά.

`kind` + `helm` αυτο-εγκαταστάσιμη εάν απόν (απελευθέρωση δυαδικά ή `choco install kind kubernetes-helm`).
μη αντιμετωπίσουν ως αναφορά. Δείτε επίσης [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
