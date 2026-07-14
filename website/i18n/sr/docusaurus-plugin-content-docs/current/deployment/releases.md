---
description: "GitHub izdanja: verzionisane kontejnerske slike (GHCR), Helm chart i CtraderCliNode binarni fajlovi — kako preuzeti izdanje i pokrenuti aplikaciju iz njega."
---

# Izdanja i pokretanje izdanja

cMind se isporučuje kao verzionisana **GitHub izdanja**. Svako izdanje objavljuje, za jedan SemVer tag:

- **Kontejnerske slike** na GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  označene verzijom (npr. `1.0.0-alpha.1`) i `sha-<commit>`. Potpisane (cosign keyless) sa atestacijama
  porekla build-a i SPDX SBOM.
- **Helm chart** — poslat na `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` i priložen izdanju kao
  `cmind-<version>.tgz`.
- **CtraderCliNode binarni fajlovi** — samostalni ZIP-ovi po platformi (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`) za pokretanje udaljenog agenta čvora bez .NET SDK.
- **`SHA256SUMS.txt`** koji pokriva svaki priloženi artefakt.

> **Alfa.** Za sada je svako izdanje pred-izdanje (`-alpha.N`). Očekujte prelomne izmene između alfa; još
> nema garancije za nadogradnju/migraciju. Zakačite tačnu verziju — nikada `latest`.

## Verzionisanje

SemVer 2.0.0. Oblik taga `vX.Y.Z[-suffix]`. Sufiks (`-alpha.N`, `-beta.N`, `-rc.N`) objavljuje GitHub
**pred-izdanje**; tag slike i verzija Helm chart-a su jednaki verziji bez početnog `v`. Aplikacija koja radi
prikazuje je na `GET /version` i u podnožju UI-a (`Core.VersionInfo`).

## Izbor izdanja

Pregledajte **[Izdanja](https://github.com/amusleh-spotware-com/cmind/releases)** i kopirajte željeni tag
(npr. `v1.0.0-alpha.1`). Verifikujte sliku pre pokretanja:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Pokretanje — Kubernetes (Helm, preporučeno)

`appVersion` chart-a već zakačinje odgovarajući tag slike, pa prosleđujete samo verziju chart-a.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<tajna klastera od 32+ karaktera>'
```

Privatnim GHCR paketima je potreban image pull secret — napravite ga i prosledite:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-sa-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Potpune opcije chart-a, ingress, eksterni Postgres i skaliranje: vidi
**[Kubernetes primena](kubernetes.md)** i **[Skaliranje](scaling.md)**. Verifikacija:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version vraća verziju izdanja
```

## Pokretanje — Docker (jedan host, brz pregled)

Pokrenite Web host direktno iz njegove slike izdanja. Potrebni su mu Postgres i Docker soket (Web host
gradi/pokreće cBot-ove preko lokalnog Docker CLI).

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

Otvorite `http://localhost:8080`. Dodajte MCP server (`cmind-mcp`) i agente čvorova na isti način; za potpunu
višeservisnu topologiju koristite Helm chart. Vidi **[Lokalni razvoj](local.md)** za Aspire `dotnet run`
putanju kada radite iz izvornog koda umesto iz izdanja.

## Pokretanje udaljenog agenta čvora iz binarnog fajla

Udaljeni hostovi koji obezbeđuju kapacitet za pokretanje/backtest mogu pokrenuti `CtraderCliNode` bez
instaliranog .NET. Preuzmite ZIP za platformu iz izdanja, raspakujte i pokrenite — sam se registruje kod Web
hosta i šalje heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<vaš-web-host>' \
NodeAgent__DiscoveryJoinToken='<ista tajna klastera od 32+ karaktera>' \
./CtraderCliNode
```

Na hostu mora da radi Docker (agent pokreće sliku cTrader konzole preko Docker CLI). Vidi
**[Kubernetes primena](kubernetes.md)** za pokretanje agenata čvorova kao privilegovanih podova.

## Pravljenje izdanja (održavaoci)

Izdanja pravi `.github/workflows/release.yml` na svaki gurnuti `v*` tag — proces je opisan u
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** u korenu repozitorijuma.
