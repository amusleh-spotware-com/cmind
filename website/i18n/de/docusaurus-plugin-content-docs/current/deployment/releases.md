---
description: "GitHub Releases: versionierte Container-Images (GHCR), Helm-Chart und CtraderCliNode-Binaries — wie man ein Release bezieht und die App daraus startet."
---

# Releases & ein Release ausführen

cMind wird als versionierte **GitHub Releases** ausgeliefert. Jedes Release veröffentlicht für ein SemVer-Tag:

- **Container-Images** auf GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  getaggt mit der Version (z. B. `1.0.0-alpha.1`) und `sha-<commit>`. Signiert (cosign keyless) mit
  Build-Provenance-Attestierungen und einer SPDX-SBOM.
- **Helm-Chart** — nach `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` gepusht und dem Release
  als `cmind-<version>.tgz` beigefügt.
- **CtraderCliNode-Binaries** — self-contained ZIPs pro Plattform (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`), um einen Remote-Node-Agent ohne .NET-SDK auszuführen.
- **`SHA256SUMS.txt`** über jedes beigefügte Artefakt.

> **Alpha.** Jedes Release ist vorerst ein Pre-Release (`-alpha.N`). Zwischen Alphas sind Breaking Changes
> zu erwarten; es gibt noch keine Upgrade-/Migrationsgarantie. Pinne eine exakte Version — niemals `latest`.

## Versionierung

SemVer 2.0.0. Tag-Form `vX.Y.Z[-suffix]`. Ein Suffix (`-alpha.N`, `-beta.N`, `-rc.N`) veröffentlicht ein
GitHub **Pre-Release**; das Image-Tag und die Helm-Chart-Version entsprechen beide der Version ohne das
führende `v`. Die laufende App zeigt sie unter `GET /version` und in der UI-Fußzeile (`Core.VersionInfo`).

## Ein Release auswählen

Durchsuche die **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** und kopiere das
gewünschte Tag (z. B. `v1.0.0-alpha.1`). Verifiziere ein Image, bevor du es ausführst:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Ausführen — Kubernetes (Helm, empfohlen)

Die `appVersion` des Charts pinnt bereits das passende Image-Tag, du übergibst also nur die Chart-Version.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<32+ Zeichen Cluster-Secret>'
```

Private GHCR-Pakete benötigen ein Image-Pull-Secret — erstelle eines und übergib es:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-mit-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Vollständige Chart-Optionen, Ingress, externes Postgres und Skalierung: siehe
**[Kubernetes-Bereitstellung](kubernetes.md)** und **[Skalierung](scaling.md)**. Verifizieren:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version liefert die Release-Version
```

## Ausführen — Docker (Einzelhost, schneller Blick)

Führe den Web-Host direkt aus seinem Release-Image aus. Er benötigt Postgres und den Docker-Socket (der
Web-Host baut/führt cBots über die lokale Docker-CLI aus).

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

Öffne `http://localhost:8080`. Füge den MCP-Server (`cmind-mcp`) und Node-Agents auf die gleiche Weise
hinzu; für die vollständige Multi-Service-Topologie nutze das Helm-Chart. Siehe
**[Lokale Entwicklung](local.md)** für den Aspire-`dotnet run`-Pfad bei der Arbeit aus dem Quellcode
statt aus einem Release.

## Einen Remote-Node-Agent aus einem Binary ausführen

Remote-Hosts, die Run-/Backtest-Kapazität bereitstellen, können `CtraderCliNode` ohne installiertes .NET
ausführen. Lade das Plattform-ZIP aus dem Release herunter, entpacke es und führe es aus — es registriert
sich selbst beim Web-Host und sendet Heartbeats.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<dein-web-host>' \
NodeAgent__DiscoveryJoinToken='<dasselbe 32+ Zeichen Cluster-Secret>' \
./CtraderCliNode
```

Der Host muss Docker ausführen (der Agent führt das cTrader-Console-Image über die Docker-CLI aus). Siehe
**[Kubernetes-Bereitstellung](kubernetes.md)**, um Node-Agents stattdessen als privilegierte Pods
auszuführen.

## Ein Release erstellen (Maintainer)

Releases werden von `.github/workflows/release.yml` bei jedem gepushten `v*`-Tag erzeugt — der Prozess
steht in **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** im
Repo-Root.
