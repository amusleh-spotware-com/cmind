---
description: "Keluaran GitHub: imej kontena berversi (GHCR), carta Helm, dan binari CtraderCliNode — cara mendapatkan keluaran dan menjalankan aplikasi daripadanya."
---

# Keluaran & menjalankan sesuatu keluaran

cMind dihantar sebagai **Keluaran GitHub** berversi. Setiap keluaran menerbitkan, bagi satu tag SemVer:

- **Imej kontena** di GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  ditag dengan versi (cth. `1.0.0-alpha.1`) dan `sha-<commit>`. Ditandatangani (cosign keyless) dengan
  pengesahan asal binaan dan SBOM SPDX.
- **Carta Helm** — ditolak ke `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` dan dilampirkan pada
  keluaran sebagai `cmind-<version>.tgz`.
- **Binari CtraderCliNode** — ZIP kendiri-lengkap mengikut platform (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) untuk menjalankan ejen nod jauh tanpa .NET SDK.
- **`SHA256SUMS.txt`** yang meliputi setiap artifak yang dilampirkan.

> **Alpha.** Buat masa ini setiap keluaran ialah pra-keluaran (`-alpha.N`). Jangkakan perubahan yang memecah
> antara alpha; belum ada jaminan naik taraf/migrasi. Sematkan versi yang tepat — jangan sekali-kali `latest`.

## Perversian

SemVer 2.0.0. Bentuk tag `vX.Y.Z[-suffix]`. Akhiran (`-alpha.N`, `-beta.N`, `-rc.N`) menerbitkan
**pra-keluaran** GitHub; tag imej dan versi carta Helm kedua-duanya sama dengan versi tanpa `v` di hadapan.
Aplikasi yang berjalan memaparkannya di `GET /version` dan di pengaki UI (`Core.VersionInfo`).

## Memilih keluaran

Layari **[Keluaran](https://github.com/amusleh-spotware-com/cmind/releases)** dan salin tag yang dikehendaki
(cth. `v1.0.0-alpha.1`). Sahkan sesuatu imej sebelum menjalankannya:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Menjalankan — Kubernetes (Helm, disyorkan)

`appVersion` carta sudah menyematkan tag imej yang sepadan, jadi anda hanya menghantar versi carta.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<rahsia kluster 32+ aksara>'
```

Pakej GHCR peribadi memerlukan image pull secret — cipta satu dan hantarkannya:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-dengan-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Pilihan carta penuh, ingress, Postgres luaran dan penskalaan: lihat
**[Penggunaan Kubernetes](kubernetes.md)** dan **[Penskalaan](scaling.md)**. Sahkan:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version memulangkan versi keluaran
```

## Menjalankan — Docker (hos tunggal, tinjauan pantas)

Jalankan hos Web terus daripada imej keluarannya. Ia memerlukan Postgres dan soket Docker (hos Web
membina/menjalankan cBot melalui Docker CLI setempat).

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

Buka `http://localhost:8080`. Tambah pelayan MCP (`cmind-mcp`) dan ejen nod dengan cara yang sama; untuk
topologi berbilang perkhidmatan penuh, gunakan carta Helm. Lihat **[Pembangunan setempat](local.md)** untuk
laluan Aspire `dotnet run` apabila bekerja daripada sumber dan bukannya keluaran.

## Menjalankan ejen nod jauh daripada binari

Hos jauh yang menyediakan kapasiti run/backtest boleh menjalankan `CtraderCliNode` tanpa .NET dipasang.
Muat turun ZIP platform daripada keluaran, nyahzip, dan jalankan — ia mendaftar sendiri dengan hos Web dan
menghantar heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<hos-web-anda>' \
NodeAgent__DiscoveryJoinToken='<rahsia kluster 32+ aksara yang sama>' \
./CtraderCliNode
```

Hos mesti menjalankan Docker (ejen melaksanakan imej konsol cTrader melalui Docker CLI). Lihat
**[Penggunaan Kubernetes](kubernetes.md)** untuk menjalankan ejen nod sebagai pod istimewa.

## Membuat keluaran (penyelenggara)

Keluaran dihasilkan oleh `.github/workflows/release.yml` pada mana-mana tag `v*` yang ditolak — prosesnya ada
dalam **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** di akar repo.
