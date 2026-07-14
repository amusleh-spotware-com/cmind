---
description: "Vydání na GitHubu: verzované kontejnerové image (GHCR), Helm chart a binárky CtraderCliNode — jak získat vydání a spustit z něj aplikaci."
---

# Vydání a spuštění vydání

cMind se dodává jako verzovaná **vydání na GitHubu**. Každé vydání publikuje pro jeden SemVer tag:

- **Kontejnerové image** na GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  označené verzí (např. `1.0.0-alpha.1`) a `sha-<commit>`. Podepsané (cosign keyless) s atestacemi původu
  buildu a SBOM ve formátu SPDX.
- **Helm chart** — nahraný do `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` a připojený k vydání
  jako `cmind-<version>.tgz`.
- **Binárky CtraderCliNode** — samostatné ZIP archivy pro každou platformu (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`) pro spuštění vzdáleného agenta uzlu bez .NET SDK.
- **`SHA256SUMS.txt`** pokrývající každý připojený artefakt.

> **Alfa.** Prozatím je každé vydání předběžné (`-alpha.N`). Mezi alfami očekávejte nekompatibilní změny;
> zatím není žádná záruka upgradu/migrace. Připněte přesnou verzi — nikdy `latest`.

## Verzování

SemVer 2.0.0. Tvar tagu `vX.Y.Z[-suffix]`. Přípona (`-alpha.N`, `-beta.N`, `-rc.N`) publikuje **předběžné
vydání** GitHubu; tag image i verze Helm chartu se rovnají verzi bez úvodního `v`. Běžící aplikace ji
zpřístupňuje přes `GET /version` a v patičce UI (`Core.VersionInfo`).

## Výběr vydání

Projděte **[Vydání](https://github.com/amusleh-spotware-com/cmind/releases)** a zkopírujte požadovaný tag
(např. `v1.0.0-alpha.1`). Před spuštěním image ověřte:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Spuštění — Kubernetes (Helm, doporučeno)

`appVersion` chartu už připíná odpovídající tag image, takže předáváte pouze verzi chartu.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<tajemství clusteru min. 32 znaků>'
```

Soukromé balíčky GHCR vyžadují image pull secret — vytvořte jej a předejte:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-s-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Kompletní volby chartu, ingress, externí Postgres a škálování: viz **[Nasazení na Kubernetes](kubernetes.md)**
a **[Škálování](scaling.md)**. Ověření:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version vrací verzi vydání
```

## Spuštění — Docker (jeden host, rychlý náhled)

Spusťte Web host přímo z jeho image vydání. Potřebuje Postgres a Docker socket (Web host sestavuje/spouští
cBoty přes lokální Docker CLI).

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

Otevřete `http://localhost:8080`. Server MCP (`cmind-mcp`) a agenty uzlů přidejte stejným způsobem; pro
kompletní víceslužbovou topologii použijte Helm chart. Viz **[Lokální vývoj](local.md)** pro cestu Aspire
`dotnet run` při práci ze zdrojů namísto z vydání.

## Spuštění vzdáleného agenta uzlu z binárky

Vzdálené hosty poskytující kapacitu pro běh/backtest mohou spustit `CtraderCliNode` bez nainstalovaného
.NET. Stáhněte ZIP pro platformu z vydání, rozbalte a spusťte — sám se zaregistruje u Web hostu a posílá
heartbeaty.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<váš-web-host>' \
NodeAgent__DiscoveryJoinToken='<stejné tajemství clusteru min. 32 znaků>' \
./CtraderCliNode
```

Na hostu musí běžet Docker (agent spouští image konzole cTrader přes Docker CLI). Viz
**[Nasazení na Kubernetes](kubernetes.md)** pro spuštění agentů uzlů jako privilegovaných podů.

## Vytvoření vydání (správci)

Vydání vytváří `.github/workflows/release.yml` při každém pushnutém tagu `v*` — proces je popsán v
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** v kořeni repozitáře.
