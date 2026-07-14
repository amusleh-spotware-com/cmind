---
description: "Wydania GitHub: wersjonowane obrazy kontenerów (GHCR), chart Helm i pliki binarne CtraderCliNode — jak pobrać wydanie i uruchomić z niego aplikację."
---

# Wydania i uruchamianie wydania

cMind jest dostarczany jako wersjonowane **Wydania GitHub**. Każde wydanie publikuje dla jednego tagu SemVer:

- **Obrazy kontenerów** w GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  oznaczone wersją (np. `1.0.0-alpha.1`) oraz `sha-<commit>`. Podpisane (cosign keyless) z poświadczeniami
  pochodzenia kompilacji i SBOM w formacie SPDX.
- **Chart Helm** — wypchnięty do `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` i dołączony do
  wydania jako `cmind-<version>.tgz`.
- **Pliki binarne CtraderCliNode** — samodzielne archiwa ZIP dla każdej platformy (`linux-x64`,
  `linux-arm64`, `win-x64`, `osx-arm64`) do uruchomienia zdalnego agenta węzła bez .NET SDK.
- **`SHA256SUMS.txt`** obejmujący każdy dołączony artefakt.

> **Alfa.** Na razie każde wydanie jest wydaniem wstępnym (`-alpha.N`). Między alfami spodziewaj się zmian
> łamiących zgodność; nie ma jeszcze gwarancji aktualizacji/migracji. Przypnij dokładną wersję — nigdy `latest`.

## Wersjonowanie

SemVer 2.0.0. Forma tagu `vX.Y.Z[-suffix]`. Sufiks (`-alpha.N`, `-beta.N`, `-rc.N`) publikuje **wydanie
wstępne** GitHub; tag obrazu i wersja chartu Helm są równe wersji bez wiodącego `v`. Działająca aplikacja
udostępnia ją pod `GET /version` oraz w stopce UI (`Core.VersionInfo`).

## Wybór wydania

Przejrzyj **[Wydania](https://github.com/amusleh-spotware-com/cmind/releases)** i skopiuj żądany tag (np.
`v1.0.0-alpha.1`). Zweryfikuj obraz przed uruchomieniem:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Uruchamianie — Kubernetes (Helm, zalecane)

`appVersion` chartu przypina już odpowiedni tag obrazu, więc przekazujesz tylko wersję chartu.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<sekret klastra min. 32 znaki>'
```

Prywatne pakiety GHCR wymagają sekretu pobierania obrazu — utwórz go i przekaż:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-z-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Pełne opcje chartu, ingress, zewnętrzny Postgres i skalowanie: zobacz
**[Wdrożenie Kubernetes](kubernetes.md)** i **[Skalowanie](scaling.md)**. Weryfikacja:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version zwraca wersję wydania
```

## Uruchamianie — Docker (pojedynczy host, szybki podgląd)

Uruchom host Web bezpośrednio z jego obrazu wydania. Potrzebuje Postgresa i gniazda Docker (host Web
buduje/uruchamia cBoty przez lokalne Docker CLI).

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

Otwórz `http://localhost:8080`. Dodaj serwer MCP (`cmind-mcp`) i agentów węzłów w ten sam sposób; dla pełnej
topologii wielousługowej użyj chartu Helm. Zobacz **[Programowanie lokalne](local.md)** dla ścieżki Aspire
`dotnet run` przy pracy ze źródeł zamiast z wydania.

## Uruchamianie zdalnego agenta węzła z pliku binarnego

Zdalne hosty zapewniające moc uruchamiania/backtestów mogą uruchomić `CtraderCliNode` bez zainstalowanego
.NET. Pobierz ZIP platformy z wydania, rozpakuj i uruchom — sam rejestruje się w hoście Web i wysyła
heartbeaty.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<twój-host-web>' \
NodeAgent__DiscoveryJoinToken='<ten sam sekret klastra min. 32 znaki>' \
./CtraderCliNode
```

Host musi mieć uruchomiony Docker (agent uruchamia obraz konsoli cTrader przez Docker CLI). Zobacz
**[Wdrożenie Kubernetes](kubernetes.md)**, aby uruchamiać agentów węzłów jako uprzywilejowane pody.

## Tworzenie wydania (opiekunowie)

Wydania są tworzone przez `.github/workflows/release.yml` przy każdym wypchniętym tagu `v*` — proces opisano
w **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** w katalogu głównym
repozytorium.
