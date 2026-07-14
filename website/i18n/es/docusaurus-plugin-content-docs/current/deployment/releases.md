---
description: "Releases de GitHub: imágenes de contenedor versionadas (GHCR), chart de Helm y binarios de CtraderCliNode — cómo obtener una release y ejecutar la app a partir de ella."
---

# Releases y ejecutar una release

cMind se distribuye como **Releases de GitHub** versionadas. Cada release publica, para un tag SemVer:

- **Imágenes de contenedor** en GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  etiquetadas con la versión (p. ej. `1.0.0-alpha.1`) y `sha-<commit>`. Firmadas (cosign keyless) con
  atestaciones de procedencia de build y una SBOM SPDX.
- **Chart de Helm** — enviado a `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` y adjunto a la
  release como `cmind-<version>.tgz`.
- **Binarios de CtraderCliNode** — ZIP autónomos por plataforma (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) para ejecutar un agente de nodo remoto sin el SDK de .NET.
- **`SHA256SUMS.txt`** que cubre cada artefacto adjunto.

> **Alpha.** Cada release es por ahora una pre-release (`-alpha.N`). Espera cambios incompatibles entre
> alphas; aún no hay garantía de actualización/migración. Fija una versión exacta — nunca `latest`.

## Versionado

SemVer 2.0.0. Forma del tag `vX.Y.Z[-suffix]`. Un sufijo (`-alpha.N`, `-beta.N`, `-rc.N`) publica una
**pre-release** de GitHub; el tag de la imagen y la versión del chart de Helm equivalen ambos a la versión
sin la `v` inicial. La app en ejecución la expone en `GET /version` y en el pie de página de la UI
(`Core.VersionInfo`).

## Elegir una release

Explora las **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** y copia el tag deseado
(p. ej. `v1.0.0-alpha.1`). Verifica una imagen antes de ejecutarla:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Ejecutar — Kubernetes (Helm, recomendado)

El `appVersion` del chart ya fija el tag de imagen correspondiente, así que solo pasas la versión del
chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<secreto de clúster de 32+ caracteres>'
```

Los paquetes privados de GHCR necesitan un secret de pull de imagen — crea uno y pásalo:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-con-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Opciones completas del chart, ingress, Postgres externo y escalado: consulta
**[Despliegue en Kubernetes](kubernetes.md)** y **[Escalado](scaling.md)**. Verificar:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version devuelve la versión de la release
```

## Ejecutar — Docker (host único, vistazo rápido)

Ejecuta el host Web directamente desde su imagen de release. Necesita Postgres y el socket de Docker (el
host Web construye/ejecuta cBots mediante la CLI de Docker local).

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

Abre `http://localhost:8080`. Añade el servidor MCP (`cmind-mcp`) y los agentes de nodo de la misma
forma; para la topología multiservicio completa usa el chart de Helm. Consulta
**[Desarrollo local](local.md)** para el flujo Aspire `dotnet run` cuando trabajes desde el código fuente
en lugar de una release.

## Ejecutar un agente de nodo remoto desde un binario

Los hosts remotos que aportan capacidad de ejecución/backtest pueden ejecutar `CtraderCliNode` sin .NET
instalado. Descarga el ZIP de la plataforma desde la release, descomprímelo y ejecútalo — se registra
automáticamente con el host Web y envía heartbeats.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<tu-host-web>' \
NodeAgent__DiscoveryJoinToken='<el mismo secreto de clúster de 32+ caracteres>' \
./CtraderCliNode
```

El host debe ejecutar Docker (el agente lanza la imagen de la consola de cTrader mediante la CLI de
Docker). Consulta **[Despliegue en Kubernetes](kubernetes.md)** para ejecutar agentes de nodo como pods
privilegiados.

## Publicar una release (mantenedores)

Las releases las produce `.github/workflows/release.yml` con cualquier tag `v*` enviado — el proceso está
en **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** en la raíz del
repositorio.
