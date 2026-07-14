---
description: "GitHub Releases: версионированные образы контейнеров (GHCR), Helm-чарт и бинарники CtraderCliNode — как получить релиз и запустить приложение из него."
---

# Релизы и запуск релиза

cMind поставляется как версионированные **GitHub Releases**. Каждый релиз публикует для одного тега SemVer:

- **Образы контейнеров** в GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  с тегами версии (например, `1.0.0-alpha.1`) и `sha-<commit>`. Подписаны (cosign keyless) с аттестациями
  происхождения сборки и SBOM в формате SPDX.
- **Helm-чарт** — отправлен в `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` и приложен к релизу
  как `cmind-<version>.tgz`.
- **Бинарники CtraderCliNode** — самодостаточные ZIP по платформам (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) для запуска удалённого агента узла без .NET SDK.
- **`SHA256SUMS.txt`** для каждого приложенного артефакта.

> **Alpha.** Пока каждый релиз — это предварительный выпуск (`-alpha.N`). Между alpha-версиями возможны
> ломающие изменения; гарантий обновления/миграции пока нет. Фиксируйте точную версию — никогда `latest`.

## Версионирование

SemVer 2.0.0. Форма тега `vX.Y.Z[-suffix]`. Суффикс (`-alpha.N`, `-beta.N`, `-rc.N`) публикует
**предварительный выпуск** GitHub; тег образа и версия Helm-чарта равны версии без ведущей `v`. Запущенное
приложение отображает её в `GET /version` и в футере UI (`Core.VersionInfo`).

## Выбор релиза

Просмотрите **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** и скопируйте нужный тег
(например, `v1.0.0-alpha.1`). Проверьте образ перед запуском:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Запуск — Kubernetes (Helm, рекомендуется)

`appVersion` чарта уже фиксирует соответствующий тег образа, поэтому вы передаёте только версию чарта.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<секрет кластера от 32 символов>'
```

Приватным пакетам GHCR нужен image pull secret — создайте его и передайте:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-с-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Полные опции чарта, ingress, внешний Postgres и масштабирование: см.
**[Развёртывание в Kubernetes](kubernetes.md)** и **[Масштабирование](scaling.md)**. Проверка:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version возвращает версию релиза
```

## Запуск — Docker (один хост, быстрый просмотр)

Запустите Web-хост прямо из его образа релиза. Ему нужны Postgres и сокет Docker (Web-хост собирает и
запускает cBot через локальный Docker CLI).

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

Откройте `http://localhost:8080`. Добавьте MCP-сервер (`cmind-mcp`) и агентов узлов таким же образом; для
полной многосервисной топологии используйте Helm-чарт. См. **[Локальная разработка](local.md)** для пути
Aspire `dotnet run` при работе из исходников, а не из релиза.

## Запуск удалённого агента узла из бинарника

Удалённые хосты, предоставляющие мощности для запуска/бэктеста, могут запускать `CtraderCliNode` без
установленного .NET. Скачайте ZIP для платформы из релиза, распакуйте и запустите — он сам
регистрируется у Web-хоста и отправляет heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<ваш-web-хост>' \
NodeAgent__DiscoveryJoinToken='<тот же секрет кластера от 32 символов>' \
./CtraderCliNode
```

На хосте должен работать Docker (агент запускает образ консоли cTrader через Docker CLI). См.
**[Развёртывание в Kubernetes](kubernetes.md)**, чтобы запускать агентов узлов как привилегированные поды.

## Создание релиза (мейнтейнеры)

Релизы создаются `.github/workflows/release.yml` по любому отправленному тегу `v*` — процесс описан в
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** в корне
репозитория.
