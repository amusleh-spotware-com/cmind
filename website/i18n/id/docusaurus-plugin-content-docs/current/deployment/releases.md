---
description: "Rilis GitHub: image kontainer berversi (GHCR), chart Helm, dan biner CtraderCliNode — cara mengambil rilis dan menjalankan aplikasi darinya."
---

# Rilis & menjalankan sebuah rilis

cMind dikirim sebagai **Rilis GitHub** berversi. Setiap rilis mempublikasikan, untuk satu tag SemVer:

- **Image kontainer** di GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  ditandai dengan versi (mis. `1.0.0-alpha.1`) dan `sha-<commit>`. Ditandatangani (cosign keyless) dengan
  atestasi asal build dan SBOM SPDX.
- **Chart Helm** — didorong ke `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` dan dilampirkan ke
  rilis sebagai `cmind-<version>.tgz`.
- **Biner CtraderCliNode** — ZIP mandiri per platform (`linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`)
  untuk menjalankan agen node jarak jauh tanpa .NET SDK.
- **`SHA256SUMS.txt`** yang mencakup setiap artefak yang dilampirkan.

> **Alpha.** Untuk saat ini setiap rilis adalah pra-rilis (`-alpha.N`). Harapkan perubahan yang merusak di
> antara alpha; belum ada jaminan upgrade/migrasi. Sematkan versi yang tepat — jangan pernah `latest`.

## Pemberian versi

SemVer 2.0.0. Bentuk tag `vX.Y.Z[-suffix]`. Sufiks (`-alpha.N`, `-beta.N`, `-rc.N`) mempublikasikan
**pra-rilis** GitHub; tag image dan versi chart Helm keduanya sama dengan versi tanpa `v` di depan.
Aplikasi yang berjalan menampilkannya di `GET /version` dan di footer UI (`Core.VersionInfo`).

## Memilih rilis

Jelajahi **[Rilis](https://github.com/amusleh-spotware-com/cmind/releases)** dan salin tag yang diinginkan
(mis. `v1.0.0-alpha.1`). Verifikasi sebuah image sebelum menjalankannya:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Menjalankan — Kubernetes (Helm, direkomendasikan)

`appVersion` chart sudah menyematkan tag image yang cocok, jadi Anda hanya meneruskan versi chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<rahasia klaster minimal 32 karakter>'
```

Paket GHCR privat memerlukan image pull secret — buat satu dan teruskan:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-dengan-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Opsi chart lengkap, ingress, Postgres eksternal, dan penskalaan: lihat
**[Deployment Kubernetes](kubernetes.md)** dan **[Penskalaan](scaling.md)**. Verifikasi:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version mengembalikan versi rilis
```

## Menjalankan — Docker (host tunggal, lihat cepat)

Jalankan host Web langsung dari image rilisnya. Ia memerlukan Postgres dan soket Docker (host Web
membangun/menjalankan cBot melalui Docker CLI lokal).

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

Buka `http://localhost:8080`. Tambahkan server MCP (`cmind-mcp`) dan agen node dengan cara yang sama;
untuk topologi multi-layanan penuh gunakan chart Helm. Lihat **[Pengembangan lokal](local.md)** untuk jalur
Aspire `dotnet run` saat bekerja dari sumber alih-alih dari rilis.

## Menjalankan agen node jarak jauh dari biner

Host jarak jauh yang menyediakan kapasitas run/backtest dapat menjalankan `CtraderCliNode` tanpa .NET
terpasang. Unduh ZIP platform dari rilis, ekstrak, dan jalankan — ia mendaftar sendiri ke host Web dan
mengirim heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<host-web-anda>' \
NodeAgent__DiscoveryJoinToken='<rahasia klaster minimal 32 karakter yang sama>' \
./CtraderCliNode
```

Host harus menjalankan Docker (agen mengeksekusi image konsol cTrader melalui Docker CLI). Lihat
**[Deployment Kubernetes](kubernetes.md)** untuk menjalankan agen node sebagai pod istimewa.

## Membuat sebuah rilis (pemelihara)

Rilis diproduksi oleh `.github/workflows/release.yml` pada setiap tag `v*` yang didorong — prosesnya ada di
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** di root repositori.
