---
description: "Εκδόσεις GitHub: εκδόσεις εικόνων container με έκδοση (GHCR), Helm chart και δυαδικά CtraderCliNode — πώς να αποκτήσετε μια έκδοση και να εκτελέσετε την εφαρμογή από αυτήν."
---

# Εκδόσεις & εκτέλεση μιας έκδοσης

Το cMind διανέμεται ως **Εκδόσεις GitHub** με σημείο έκδοσης. Κάθε έκδοση δημοσιεύει, για ένα tag SemVer:

- **Εικόνες container** στο GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  με tag την έκδοση (π.χ. `1.0.0-alpha.1`) και `sha-<commit>`. Υπογεγραμμένες (cosign keyless) με βεβαιώσεις
  προέλευσης build και SBOM τύπου SPDX.
- **Helm chart** — προωθείται στο `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` και επισυνάπτεται
  στην έκδοση ως `cmind-<version>.tgz`.
- **Δυαδικά CtraderCliNode** — αυτόνομα ZIP ανά πλατφόρμα (`linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`)
  για την εκτέλεση ενός απομακρυσμένου agent κόμβου χωρίς το .NET SDK.
- **`SHA256SUMS.txt`** που καλύπτει κάθε επισυναπτόμενο τεχνούργημα.

> **Alpha.** Προς το παρόν κάθε έκδοση είναι προέκδοση (`-alpha.N`). Αναμένετε breaking changes μεταξύ των
> alpha· δεν υπάρχει ακόμη εγγύηση αναβάθμισης/μετεγκατάστασης. Καρφιτσώστε μια ακριβή έκδοση — ποτέ `latest`.

## Έκδοση (versioning)

SemVer 2.0.0. Μορφή tag `vX.Y.Z[-suffix]`. Ένα επίθημα (`-alpha.N`, `-beta.N`, `-rc.N`) δημοσιεύει μια
**προέκδοση** GitHub· το tag της εικόνας και η έκδοση του Helm chart ισούνται και τα δύο με την έκδοση χωρίς
το αρχικό `v`. Η εφαρμογή που εκτελείται την εμφανίζει στο `GET /version` και στο υποσέλιδο του UI
(`Core.VersionInfo`).

## Επιλογή έκδοσης

Περιηγηθείτε στις **[Εκδόσεις](https://github.com/amusleh-spotware-com/cmind/releases)** και αντιγράψτε το
tag που θέλετε (π.χ. `v1.0.0-alpha.1`). Επαληθεύστε μια εικόνα πριν την εκτελέσετε:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Εκτέλεση — Kubernetes (Helm, συνιστάται)

Το `appVersion` του chart καρφιτσώνει ήδη το αντίστοιχο tag εικόνας, οπότε περνάτε μόνο την έκδοση του chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<μυστικό cluster 32+ χαρακτήρων>'
```

Τα ιδιωτικά πακέτα GHCR χρειάζονται ένα image pull secret — δημιουργήστε ένα και περάστε το:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-με-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Πλήρεις επιλογές chart, ingress, εξωτερικό Postgres και κλιμάκωση: δείτε
**[Ανάπτυξη Kubernetes](kubernetes.md)** και **[Κλιμάκωση](scaling.md)**. Επαλήθευση:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; το GET /version επιστρέφει την έκδοση της έκδοσης
```

## Εκτέλεση — Docker (μεμονωμένος host, γρήγορη ματιά)

Εκτελέστε τον Web host απευθείας από την εικόνα της έκδοσής του. Χρειάζεται Postgres και το socket του Docker
(ο Web host κατασκευάζει/εκτελεί cBots μέσω του τοπικού Docker CLI).

```bash
VERSION=1.0.0-alpha.1
docker network create cmind

docker run -d --name cmind-pg --network cmind \
  -e POSTGRES_PASSWORD=change-me -e POSTGRES_DB=cmind postgres:17

docker run -d --name cmind-web --network cmind -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e ConnectionStrings__Default='Host=cmind-pg;Database=cmind;Username=postgres;Password=change-me' \
  -e App__Owner__Email='owner@example.com' \
  -e App__Owner__Password='Change-Me-Str0ng!' \
  ghcr.io/amusleh-spotware-com/cmind-web:$VERSION
```

Ανοίξτε το `http://localhost:8080`. Προσθέστε τον διακομιστή MCP (`cmind-mcp`) και τους agents κόμβων με τον
ίδιο τρόπο· για την πλήρη τοπολογία πολλαπλών υπηρεσιών χρησιμοποιήστε το Helm chart. Δείτε
**[Τοπική ανάπτυξη](local.md)** για τη διαδρομή Aspire `dotnet run` όταν εργάζεστε από τον πηγαίο κώδικα αντί
για μια έκδοση.

## Εκτέλεση απομακρυσμένου agent κόμβου από δυαδικό

Οι απομακρυσμένοι hosts που παρέχουν χωρητικότητα run/backtest μπορούν να εκτελέσουν το `CtraderCliNode`
χωρίς εγκατεστημένο .NET. Κατεβάστε το ZIP της πλατφόρμας από την έκδοση, αποσυμπιέστε το και εκτελέστε το —
εγγράφεται αυτόματα στον Web host και στέλνει heartbeats.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<ο-web-host-σας>' \
NodeAgent__DiscoveryJoinToken='<το ίδιο μυστικό cluster 32+ χαρακτήρων>' \
./CtraderCliNode
```

Ο host πρέπει να εκτελεί Docker (ο agent εκτελεί την εικόνα κονσόλας cTrader μέσω του Docker CLI). Δείτε
**[Ανάπτυξη Kubernetes](kubernetes.md)** για να εκτελέσετε agents κόμβων ως προνομιούχα pods.

## Δημιουργία έκδοσης (συντηρητές)

Οι εκδόσεις παράγονται από το `.github/workflows/release.yml` σε κάθε tag `v*` που προωθείται — η διαδικασία
βρίσκεται στο **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** στη
ρίζα του αποθετηρίου.
