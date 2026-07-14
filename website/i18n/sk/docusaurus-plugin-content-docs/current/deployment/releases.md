---
description: "Vydania na GitHube: verzované kontajnerové obrazy (GHCR), Helm chart a binárky CtraderCliNode — ako získať vydanie a spustiť z neho aplikáciu."
---

# Vydania a spustenie vydania

cMind sa dodáva ako verzované **vydania na GitHube**. Každé vydanie publikuje pre jeden SemVer tag:

- **Kontajnerové obrazy** na GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  označené verziou (napr. `1.0.0-alpha.1`) a `sha-<commit>`. Podpísané (cosign keyless) s atestáciami pôvodu
  buildu a SBOM vo formáte SPDX.
- **Helm chart** — nahraný do `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` a pripojený k vydaniu
  ako `cmind-<version>.tgz`.
- **Binárky CtraderCliNode** — samostatné ZIP archívy pre každú platformu (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`) na spustenie vzdialeného agenta uzla bez .NET SDK.
- **`SHA256SUMS.txt`** pokrývajúci každý pripojený artefakt.

> **Alfa.** Zatiaľ je každé vydanie predbežné (`-alpha.N`). Medzi alfami očakávajte nekompatibilné zmeny;
> zatiaľ neexistuje záruka upgradu/migrácie. Pripnite presnú verziu — nikdy `latest`.

## Verzovanie

SemVer 2.0.0. Tvar tagu `vX.Y.Z[-suffix]`. Prípona (`-alpha.N`, `-beta.N`, `-rc.N`) publikuje **predbežné
vydanie** GitHubu; tag obrazu aj verzia Helm chartu sa rovnajú verzii bez úvodného `v`. Bežiaca aplikácia ju
sprístupňuje cez `GET /version` a v pätičke UI (`Core.VersionInfo`).

## Výber vydania

Prezrite si **[Vydania](https://github.com/amusleh-spotware-com/cmind/releases)** a skopírujte požadovaný
tag (napr. `v1.0.0-alpha.1`). Pred spustením obraz overte:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Spustenie — Kubernetes (Helm, odporúčané)

`appVersion` chartu už pripína zodpovedajúci tag obrazu, takže odovzdávate len verziu chartu.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<tajomstvo klastra min. 32 znakov>'
```

Súkromné balíky GHCR vyžadujú image pull secret — vytvorte ho a odovzdajte:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-s-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Kompletné možnosti chartu, ingress, externý Postgres a škálovanie: pozri
**[Nasadenie na Kubernetes](kubernetes.md)** a **[Škálovanie](scaling.md)**. Overenie:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version vracia verziu vydania
```

## Spustenie — Docker (jeden hostiteľ, rýchly náhľad)

Spustite Web hostiteľa priamo z jeho obrazu vydania. Potrebuje Postgres a Docker socket (Web hostiteľ
zostavuje/spúšťa cBoty cez lokálne Docker CLI).

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

Otvorte `http://localhost:8080`. Server MCP (`cmind-mcp`) a agentov uzlov pridajte rovnakým spôsobom; pre
kompletnú viacslužbovú topológiu použite Helm chart. Pozri **[Lokálny vývoj](local.md)** pre cestu Aspire
`dotnet run` pri práci zo zdrojov namiesto z vydania.

## Spustenie vzdialeného agenta uzla z binárky

Vzdialení hostitelia poskytujúci kapacitu na beh/backtest môžu spustiť `CtraderCliNode` bez nainštalovaného
.NET. Stiahnite ZIP pre platformu z vydania, rozbaľte a spustite — sám sa zaregistruje u Web hostiteľa a
posiela heartbeaty.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<váš-web-hostiteľ>' \
NodeAgent__DiscoveryJoinToken='<rovnaké tajomstvo klastra min. 32 znakov>' \
./CtraderCliNode
```

Na hostiteľovi musí bežať Docker (agent spúšťa obraz konzoly cTrader cez Docker CLI). Pozri
**[Nasadenie na Kubernetes](kubernetes.md)** pre spustenie agentov uzlov ako privilegovaných podov.

## Vytvorenie vydania (správcovia)

Vydania vytvára `.github/workflows/release.yml` pri každom pushnutom tagu `v*` — proces je opísaný v
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** v koreni repozitára.
