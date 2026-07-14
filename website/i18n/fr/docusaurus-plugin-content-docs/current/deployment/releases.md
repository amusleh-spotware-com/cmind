---
description: "Releases GitHub : images de conteneurs versionnés (GHCR), chart Helm et binaires CtraderCliNode — comment récupérer une release et exécuter l'application à partir d'elle."
---

# Releases & exécuter une release

cMind est livré sous forme de **Releases GitHub** versionnées. Chaque release publie, pour un tag SemVer :

- **Images de conteneurs** sur GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  taguées avec la version (p. ex. `1.0.0-alpha.1`) et `sha-<commit>`. Signées (cosign keyless) avec des
  attestations de provenance de build et une SBOM SPDX.
- **Chart Helm** — poussé vers `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` et joint à la
  release sous `cmind-<version>.tgz`.
- **Binaires CtraderCliNode** — archives ZIP autonomes par plateforme (`linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`) pour exécuter un agent de nœud distant sans le SDK .NET.
- **`SHA256SUMS.txt`** couvrant chaque artefact joint.

> **Alpha.** Chaque release est pour l'instant une pré-release (`-alpha.N`). Attendez-vous à des
> changements incompatibles entre les alphas ; il n'y a pas encore de garantie de mise à niveau/migration.
> Épinglez une version exacte — jamais `latest`.

## Versionnement

SemVer 2.0.0. Forme du tag `vX.Y.Z[-suffix]`. Un suffixe (`-alpha.N`, `-beta.N`, `-rc.N`) publie une
**pré-release** GitHub ; le tag de l'image et la version du chart Helm sont tous deux égaux à la version
sans le `v` initial. L'application en cours d'exécution l'expose via `GET /version` et dans le pied de
page de l'UI (`Core.VersionInfo`).

## Choisir une release

Parcourez les **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** et copiez le tag
souhaité (p. ex. `v1.0.0-alpha.1`). Vérifiez une image avant de l'exécuter :

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Exécuter — Kubernetes (Helm, recommandé)

L'`appVersion` du chart épingle déjà le tag d'image correspondant, vous ne passez donc que la version du
chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<secret de cluster de 32+ caractères>'
```

Les paquets GHCR privés nécessitent un secret de pull d'image — créez-en un et passez-le :

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-avec-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Options complètes du chart, ingress, Postgres externe et mise à l'échelle : voir
**[Déploiement Kubernetes](kubernetes.md)** et **[Mise à l'échelle](scaling.md)**. Vérifier :

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version renvoie la version de la release
```

## Exécuter — Docker (hôte unique, aperçu rapide)

Exécutez l'hôte Web directement à partir de son image de release. Il a besoin de Postgres et du socket
Docker (l'hôte Web construit/exécute les cBots via la CLI Docker locale).

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

Ouvrez `http://localhost:8080`. Ajoutez le serveur MCP (`cmind-mcp`) et les agents de nœud de la même
manière ; pour la topologie multi-services complète, utilisez le chart Helm. Voir
**[Développement local](local.md)** pour le chemin Aspire `dotnet run` lorsque vous travaillez à partir
des sources plutôt que d'une release.

## Exécuter un agent de nœud distant à partir d'un binaire

Les hôtes distants qui fournissent de la capacité d'exécution/backtest peuvent exécuter `CtraderCliNode`
sans .NET installé. Téléchargez le ZIP de la plateforme depuis la release, décompressez-le et exécutez-le
— il s'enregistre automatiquement auprès de l'hôte Web et envoie des heartbeats.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<votre-hote-web>' \
NodeAgent__DiscoveryJoinToken='<le même secret de cluster de 32+ caractères>' \
./CtraderCliNode
```

L'hôte doit exécuter Docker (l'agent lance l'image de la console cTrader via la CLI Docker). Voir
**[Déploiement Kubernetes](kubernetes.md)** pour exécuter les agents de nœud en tant que pods privilégiés.

## Créer une release (mainteneurs)

Les releases sont produites par `.github/workflows/release.yml` sur tout tag `v*` poussé — le processus
est décrit dans **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** à
la racine du dépôt.
