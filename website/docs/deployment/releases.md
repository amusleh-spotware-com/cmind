---
description: "GitHub Releases: versioned container images (GHCR), Helm chart, and CtraderCliNode binaries — how to pull a release and run the app from it."
---

# Releases & running a release

cMind ships as versioned **GitHub Releases**. Each release publishes, for one SemVer tag:

- **Container images** on GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  tagged with the version (e.g. `1.0.0-alpha.1`) and `sha-<commit>`. Signed (cosign keyless) with
  build-provenance attestations and an SPDX SBOM.
- **Helm chart** — pushed to `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` and attached to
  the release as `cmind-<version>.tgz`.
- **CtraderCliNode binaries** — self-contained zips per platform (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`) for running a remote node agent without the .NET SDK.
- **`SHA256SUMS.txt`** covering every attached asset.

> **Alpha.** Every release for now is a pre-release (`-alpha.N`). Expect breaking changes between
> alphas; there is no upgrade/migration guarantee yet. Pin an exact version — never `latest`.

## Versioning

SemVer 2.0.0. Tag form `vX.Y.Z[-suffix]`. A suffix (`-alpha.N`, `-beta.N`, `-rc.N`) publishes a
GitHub **pre-release**; the image tag and Helm chart version both equal the version without the
leading `v`. The running app surfaces it at `GET /version` and in the UI footer (`Core.VersionInfo`).

## Pick a release

Browse **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** and copy the tag you
want (e.g. `v1.0.0-alpha.1`). Verify an image before you run it:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Run it — Kubernetes (Helm, recommended)

The chart's `appVersion` already pins the matching image tag, so you only pass the chart version.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<32+ char cluster secret>'
```

Private GHCR packages need an image pull secret — create one and pass it:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-with-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Full chart options, ingress, external Postgres and scaling: see
**[Kubernetes deployment](kubernetes.md)** and **[Scaling](scaling.md)**. Verify:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version returns the release version
```

## Run it — Docker (single host, quick look)

Run the Web host directly from its released image. It needs Postgres and the Docker socket (the Web
host builds/runs cBots via the local Docker CLI).

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

Open `http://localhost:8080`. Add the MCP server (`cmind-mcp`) and node agents the same way; for the
full multi-service topology use the Helm chart. See **[Local development](local.md)** for the Aspire
`dotnet run` path when working from source instead of a release.

## Run a remote node agent from a binary

Remote hosts that provide run/backtest capacity can run `CtraderCliNode` without .NET installed.
Download the platform zip from the release, unpack, and run — it self-registers with the Web host and
heartbeats.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<your-web-host>' \
NodeAgent__DiscoveryJoinToken='<same 32+ char cluster secret>' \
./CtraderCliNode
```

The host must run Docker (the agent execs the cTrader console image via the Docker CLI). See
**[Kubernetes deployment](kubernetes.md)** for running node agents as privileged pods instead.

## Cutting a release (maintainers)

Releases are produced by `.github/workflows/release.yml` on any pushed `v*` tag — the process is in
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** at the repo
root.
