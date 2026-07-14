---
description: "Release GitHub: immagini container versionate (GHCR), chart Helm e binari CtraderCliNode — come ottenere una release ed eseguire l'app a partire da essa."
---

# Release ed esecuzione di una release

cMind viene distribuito come **Release GitHub** versionate. Ogni release pubblica, per un tag SemVer:

- **Immagini container** su GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  taggate con la versione (es. `1.0.0-alpha.1`) e `sha-<commit>`. Firmate (cosign keyless) con
  attestazioni di provenienza della build e una SBOM SPDX.
- **Chart Helm** — inviato a `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` e allegato alla
  release come `cmind-<version>.tgz`.
- **Binari CtraderCliNode** — ZIP self-contained per piattaforma (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) per eseguire un agente di nodo remoto senza l'SDK .NET.
- **`SHA256SUMS.txt`** che copre ogni artefatto allegato.

> **Alpha.** Per ora ogni release è una pre-release (`-alpha.N`). Attendi modifiche incompatibili tra le
> alpha; non esiste ancora una garanzia di upgrade/migrazione. Fissa una versione esatta — mai `latest`.

## Versionamento

SemVer 2.0.0. Forma del tag `vX.Y.Z[-suffix]`. Un suffisso (`-alpha.N`, `-beta.N`, `-rc.N`) pubblica una
**pre-release** GitHub; il tag dell'immagine e la versione del chart Helm sono entrambi uguali alla
versione senza la `v` iniziale. L'app in esecuzione la espone su `GET /version` e nel footer della UI
(`Core.VersionInfo`).

## Scegliere una release

Sfoglia le **[Release](https://github.com/amusleh-spotware-com/cmind/releases)** e copia il tag desiderato
(es. `v1.0.0-alpha.1`). Verifica un'immagine prima di eseguirla:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Eseguire — Kubernetes (Helm, consigliato)

L'`appVersion` del chart fissa già il tag immagine corrispondente, quindi passi solo la versione del
chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<secret del cluster di 32+ caratteri>'
```

I pacchetti GHCR privati richiedono un secret di pull dell'immagine — creane uno e passalo:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-con-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Opzioni complete del chart, ingress, Postgres esterno e scaling: vedi
**[Deploy su Kubernetes](kubernetes.md)** e **[Scaling](scaling.md)**. Verifica:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version restituisce la versione della release
```

## Eseguire — Docker (host singolo, sguardata veloce)

Esegui l'host Web direttamente dalla sua immagine di release. Ha bisogno di Postgres e del socket Docker
(l'host Web costruisce/esegue i cBot tramite la CLI Docker locale).

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

Apri `http://localhost:8080`. Aggiungi il server MCP (`cmind-mcp`) e gli agenti di nodo allo stesso modo;
per la topologia multi-servizio completa usa il chart Helm. Vedi **[Sviluppo locale](local.md)** per il
percorso Aspire `dotnet run` quando lavori dai sorgenti invece che da una release.

## Eseguire un agente di nodo remoto da un binario

Gli host remoti che forniscono capacità di run/backtest possono eseguire `CtraderCliNode` senza .NET
installato. Scarica lo ZIP della piattaforma dalla release, estrailo ed eseguilo — si registra
automaticamente con l'host Web e invia heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<il-tuo-host-web>' \
NodeAgent__DiscoveryJoinToken='<lo stesso secret del cluster di 32+ caratteri>' \
./CtraderCliNode
```

L'host deve eseguire Docker (l'agente avvia l'immagine della console cTrader tramite la CLI Docker). Vedi
**[Deploy su Kubernetes](kubernetes.md)** per eseguire gli agenti di nodo come pod privilegiati.

## Creare una release (manutentori)

Le release sono prodotte da `.github/workflows/release.yml` su qualsiasi tag `v*` inviato — il processo è
in **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** nella radice
del repository.
