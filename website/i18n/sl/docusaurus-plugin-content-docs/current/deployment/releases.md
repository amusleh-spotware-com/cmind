---
description: "Izdaje GitHub: verzionirane vsebniške slike (GHCR), Helm chart in binarne datoteke CtraderCliNode — kako pridobiti izdajo in iz nje zagnati aplikacijo."
---

# Izdaje in zagon izdaje

cMind se dostavlja kot verzionirane **izdaje GitHub**. Vsaka izdaja za eno oznako SemVer objavi:

- **Vsebniške slike** na GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  označene z različico (npr. `1.0.0-alpha.1`) in `sha-<commit>`. Podpisane (cosign keyless) s potrdili o
  izvoru gradnje in SBOM v obliki SPDX.
- **Helm chart** — potisnjen v `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` in priložen izdaji
  kot `cmind-<version>.tgz`.
- **Binarne datoteke CtraderCliNode** — samostojni ZIP-i po platformah (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`) za zagon oddaljenega agenta vozlišča brez .NET SDK.
- **`SHA256SUMS.txt`**, ki pokriva vsak priloženi artefakt.

> **Alfa.** Za zdaj je vsaka izdaja predizdaja (`-alpha.N`). Med alfami pričakujte prelomne spremembe;
> jamstva za nadgradnjo/migracijo še ni. Pripnite točno različico — nikoli `latest`.

## Verzioniranje

SemVer 2.0.0. Oblika oznake `vX.Y.Z[-suffix]`. Pripona (`-alpha.N`, `-beta.N`, `-rc.N`) objavi **predizdajo**
GitHub; oznaka slike in različica Helm charta sta enaki različici brez začetnega `v`. Delujoča aplikacija jo
prikazuje na `GET /version` in v nogi uporabniškega vmesnika (`Core.VersionInfo`).

## Izbira izdaje

Prebrskajte **[Izdaje](https://github.com/amusleh-spotware-com/cmind/releases)** in kopirajte želeno oznako
(npr. `v1.0.0-alpha.1`). Pred zagonom preverite sliko:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Zagon — Kubernetes (Helm, priporočeno)

`appVersion` charta že pripne ustrezno oznako slike, zato posredujete samo različico charta.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<skrivnost gruče najmanj 32 znakov>'
```

Zasebni paketi GHCR potrebujejo image pull secret — ustvarite ga in posredujte:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-z-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Celotne možnosti charta, ingress, zunanji Postgres in skaliranje: glejte
**[Namestitev Kubernetes](kubernetes.md)** in **[Skaliranje](scaling.md)**. Preverjanje:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version vrne različico izdaje
```

## Zagon — Docker (en gostitelj, hiter pregled)

Zaženite gostitelja Web neposredno iz njegove slike izdaje. Potrebuje Postgres in vtičnico Docker (gostitelj
Web gradi/zaganja cBote prek lokalnega Docker CLI).

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

Odprite `http://localhost:8080`. Strežnik MCP (`cmind-mcp`) in agente vozlišč dodajte na enak način; za
celotno večstoritveno topologijo uporabite Helm chart. Glejte **[Lokalni razvoj](local.md)** za pot Aspire
`dotnet run`, ko delate iz izvorne kode namesto iz izdaje.

## Zagon oddaljenega agenta vozlišča iz binarne datoteke

Oddaljeni gostitelji, ki zagotavljajo zmogljivost za zagon/backtest, lahko zaženejo `CtraderCliNode` brez
nameščenega .NET. Prenesite ZIP za platformo iz izdaje, ga razširite in zaženite — sam se registrira pri
gostitelju Web in pošilja heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<vaš-web-gostitelj>' \
NodeAgent__DiscoveryJoinToken='<enaka skrivnost gruče najmanj 32 znakov>' \
./CtraderCliNode
```

Na gostitelju mora teči Docker (agent zažene sliko konzole cTrader prek Docker CLI). Glejte
**[Namestitev Kubernetes](kubernetes.md)** za zagon agentov vozlišč kot privilegiranih podov.

## Ustvarjanje izdaje (vzdrževalci)

Izdaje ustvari `.github/workflows/release.yml` ob vsaki potisnjeni oznaki `v*` — postopek je v
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** v korenu repozitorija.
