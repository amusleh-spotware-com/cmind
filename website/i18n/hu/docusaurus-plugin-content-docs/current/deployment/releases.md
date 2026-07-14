---
description: "GitHub kiadások: verziózott konténerképek (GHCR), Helm chart és CtraderCliNode binárisok — hogyan szerezz be egy kiadást, és futtasd belőle az alkalmazást."
---

# Kiadások és egy kiadás futtatása

A cMind verziózott **GitHub kiadásokként** kerül kiszállításra. Minden kiadás egy SemVer címkéhez a következőket teszi közzé:

- **Konténerképek** a GHCR-en — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  a verzióval (pl. `1.0.0-alpha.1`) és `sha-<commit>` címkével. Aláírva (cosign keyless), build-eredet
  igazolásokkal és SPDX SBOM-mal.
- **Helm chart** — feltöltve az `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` címre, és a
  kiadáshoz csatolva `cmind-<version>.tgz` néven.
- **CtraderCliNode binárisok** — platformonkénti önálló ZIP-ek (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) egy távoli csomópont-ügynök .NET SDK nélküli futtatásához.
- **`SHA256SUMS.txt`**, amely minden csatolt terméket lefed.

> **Alfa.** Egyelőre minden kiadás előzetes kiadás (`-alpha.N`). Az alfák között törő változásokra
> számíts; még nincs frissítési/migrációs garancia. Rögzíts pontos verziót — soha ne `latest`-et.

## Verziózás

SemVer 2.0.0. A címke formája `vX.Y.Z[-suffix]`. Egy utótag (`-alpha.N`, `-beta.N`, `-rc.N`) GitHub
**előzetes kiadást** tesz közzé; a képcímke és a Helm chart verziója is a kezdő `v` nélküli verzióval
egyenlő. A futó alkalmazás ezt a `GET /version` végponton és a felület láblécében (`Core.VersionInfo`)
jeleníti meg.

## Kiadás kiválasztása

Böngészd a **[Kiadásokat](https://github.com/amusleh-spotware-com/cmind/releases)**, és másold ki a kívánt
címkét (pl. `v1.0.0-alpha.1`). Ellenőrizz egy képet futtatás előtt:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Futtatás — Kubernetes (Helm, ajánlott)

A chart `appVersion` értéke már rögzíti a megfelelő képcímkét, így csak a chart verzióját add meg.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<legalább 32 karakteres fürt-titok>'
```

A privát GHCR csomagokhoz image pull secret kell — hozz létre egyet, és add át:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<read:packages jogú PAT>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Teljes chart-beállítások, ingress, külső Postgres és skálázás: lásd
**[Kubernetes telepítés](kubernetes.md)** és **[Skálázás](scaling.md)**. Ellenőrzés:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; a GET /version visszaadja a kiadás verzióját
```

## Futtatás — Docker (egyetlen gazdagép, gyors áttekintés)

Futtasd a Web gazdagépet közvetlenül a kiadási képéből. Postgres és Docker socket kell hozzá (a Web
gazdagép a helyi Docker CLI-n keresztül építi/futtatja a cBotokat).

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

Nyisd meg a `http://localhost:8080` címet. Add hozzá az MCP-kiszolgálót (`cmind-mcp`) és a csomópont-
ügynököket ugyanígy; a teljes többszolgáltatásos topológiához használd a Helm chartot. Lásd
**[Helyi fejlesztés](local.md)** az Aspire `dotnet run` útvonalhoz, amikor forrásból dolgozol, nem
kiadásból.

## Távoli csomópont-ügynök futtatása binárisból

A run/backtest kapacitást biztosító távoli gazdagépek .NET telepítése nélkül futtathatják a
`CtraderCliNode`-ot. Töltsd le a platform ZIP-jét a kiadásból, csomagold ki, és futtasd — automatikusan
regisztrál a Web gazdagépnél, és heartbeatet küld.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<a-te-web-gazdagéped>' \
NodeAgent__DiscoveryJoinToken='<ugyanaz a legalább 32 karakteres fürt-titok>' \
./CtraderCliNode
```

A gazdagépen Dockernek kell futnia (az ügynök a Docker CLI-n keresztül futtatja a cTrader konzol képet).
Lásd **[Kubernetes telepítés](kubernetes.md)** a csomópont-ügynökök privilegizált podként való futtatásához.

## Kiadás készítése (karbantartók)

A kiadásokat a `.github/workflows/release.yml` állítja elő minden feltolt `v*` címkénél — a folyamat a
tároló gyökerében lévő
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** fájlban található.
