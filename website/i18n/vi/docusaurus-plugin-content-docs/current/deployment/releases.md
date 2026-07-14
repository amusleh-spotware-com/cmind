---
description: "GitHub Releases: image container có phiên bản (GHCR), Helm chart và các tệp nhị phân CtraderCliNode — cách lấy một bản phát hành và chạy ứng dụng từ đó."
---

# Bản phát hành & chạy một bản phát hành

cMind được phân phối dưới dạng **GitHub Releases** có phiên bản. Mỗi bản phát hành xuất bản, cho một thẻ SemVer:

- **Image container** trên GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  được gắn thẻ bằng phiên bản (ví dụ `1.0.0-alpha.1`) và `sha-<commit>`. Được ký (cosign keyless) với chứng
  thực nguồn gốc build và SBOM dạng SPDX.
- **Helm chart** — đẩy lên `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` và đính kèm vào bản phát
  hành dưới dạng `cmind-<version>.tgz`.
- **Tệp nhị phân CtraderCliNode** — các ZIP tự chứa theo nền tảng (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) để chạy một node agent từ xa mà không cần .NET SDK.
- **`SHA256SUMS.txt`** bao phủ mọi artifact đính kèm.

> **Alpha.** Hiện tại mỗi bản phát hành là bản phát hành trước (`-alpha.N`). Hãy dự kiến các thay đổi phá vỡ
> giữa các alpha; chưa có bảo đảm nâng cấp/di trú. Ghim một phiên bản chính xác — không bao giờ dùng `latest`.

## Đánh phiên bản

SemVer 2.0.0. Dạng thẻ `vX.Y.Z[-suffix]`. Một hậu tố (`-alpha.N`, `-beta.N`, `-rc.N`) xuất bản một **bản phát
hành trước** của GitHub; thẻ image và phiên bản Helm chart đều bằng phiên bản không có `v` đầu. Ứng dụng đang
chạy hiển thị nó tại `GET /version` và ở chân trang UI (`Core.VersionInfo`).

## Chọn một bản phát hành

Duyệt **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** và sao chép thẻ mong muốn (ví dụ
`v1.0.0-alpha.1`). Xác minh một image trước khi chạy:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Chạy — Kubernetes (Helm, khuyến nghị)

`appVersion` của chart đã ghim thẻ image tương ứng, nên bạn chỉ cần truyền phiên bản chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<bí mật cụm 32+ ký tự>'
```

Các gói GHCR riêng tư cần một image pull secret — tạo một cái và truyền vào:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-với-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Toàn bộ tùy chọn chart, ingress, Postgres bên ngoài và mở rộng quy mô: xem
**[Triển khai Kubernetes](kubernetes.md)** và **[Mở rộng quy mô](scaling.md)**. Xác minh:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version trả về phiên bản của bản phát hành
```

## Chạy — Docker (một host, xem nhanh)

Chạy host Web trực tiếp từ image bản phát hành của nó. Nó cần Postgres và Docker socket (host Web build/chạy
cBot qua Docker CLI cục bộ).

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

Mở `http://localhost:8080`. Thêm máy chủ MCP (`cmind-mcp`) và các node agent theo cùng cách; để có cấu trúc
đa dịch vụ đầy đủ hãy dùng Helm chart. Xem **[Phát triển cục bộ](local.md)** cho đường dẫn Aspire `dotnet run`
khi làm việc từ mã nguồn thay vì từ một bản phát hành.

## Chạy một node agent từ xa từ tệp nhị phân

Các host từ xa cung cấp năng lực chạy/backtest có thể chạy `CtraderCliNode` mà không cần cài .NET. Tải ZIP
nền tảng từ bản phát hành, giải nén và chạy — nó tự đăng ký với host Web và gửi heartbeat.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<host-web-của-bạn>' \
NodeAgent__DiscoveryJoinToken='<cùng bí mật cụm 32+ ký tự>' \
./CtraderCliNode
```

Host phải chạy Docker (agent thực thi image console cTrader qua Docker CLI). Xem
**[Triển khai Kubernetes](kubernetes.md)** để chạy các node agent như các pod đặc quyền.

## Tạo một bản phát hành (người bảo trì)

Các bản phát hành được tạo bởi `.github/workflows/release.yml` trên bất kỳ thẻ `v*` nào được đẩy — quy trình
nằm trong **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** ở gốc kho.
