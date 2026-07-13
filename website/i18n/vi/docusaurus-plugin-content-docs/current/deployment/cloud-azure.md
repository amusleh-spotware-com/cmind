---
description: "deploy/azure/main.bicep cung cấp tầng không trạng thái trên Azure Container Apps cộng với Postgres Flexible Server + Log Analytics."
---

# Triển khai Azure — từng bước

`deploy/azure/main.bicep` cung cấp tầng không trạng thái trên **Azure Container Apps** cộng với **Postgres Flexible Server** + Log Analytics.

## 1. Điều kiện tiên quyết

- Azure CLI (`az login` đã xong), đăng ký, quyền tạo nhóm tài nguyên.
- Ba hình ảnh được đẩy vào sổ đăng ký mà Azure có thể kéo (ví dụ GHCR công khai hoặc ACR).

## 2. Tạo một nhóm tài nguyên

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Triển khai Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Tạo: Môi trường Container Apps, Web (ingress bên ngoài), MCP (ingress bên ngoài), Postgres Flexible Server + `appdb`, Log Analytics, thành phần **Application Insights dựa trên không gian làm việc**. Khám phá bật cho Web. Chuỗi kết nối được tiêm vào Web + MCP là `APPLICATIONINSIGHTS_CONNECTION_STRING`, vì vậy dấu vết + số liệu xuất khẩu nguyên bản đến App Insights trong khi nhật ký đổ vào cùng một không gian làm việc Log Analytics — không cần trình thu thập. Chuyển `-p otlpEndpoint=...` để cũng chuyển tiếp sang trình thu thập OTLP.

## 4. Nhận các URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Mở `webUrl`, đăng nhập bằng chủ sở hữu (buộc đổi mật khẩu khi đăng nhập lần đầu).

## 5. Thêm các tác nhân nút (riêng biệt)

Container Apps không thể chạy được ưu tiên/DinD, vì vậy hãy chạy các tác nhân ở nơi khác, trỏ vào `webUrl`:

- **AKS** — triển khai biểu đồ Helm ([kubernetes.md](kubernetes.md)) với `nodeAgent.privileged=true`, thu nhỏ Web/MCP thành 0 nếu chỉ muốn tầng tác nhân ở đó.
- **VM / VMSS** — chạy hình ảnh `cmind-node-agent` `--privileged` với `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Các tác nhân tự đăng ký trong một khoảng thời gian nhịp tim — xem [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Xác minh

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # nhật ký JSON compact
curl -s <webUrl>/version
```

## Ghi chú sản xuất

- Front Web với Azure Front Door / App Gateway cho TLS + WAF.
- Lưu trữ bí mật trong Key Vault; chuyển chứng chỉ Bảo vệ Dữ liệu ổn định (`App__DataProtectionCertBase64` / `...Password`) để vòng khóa sống sót trong khởi động lại bản sao.
- App Insights (dấu vết+số liệu) + Log Analytics (nhật ký) được kết nối tự động; tương quan về `trace_id`. Xem [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Đặt tham số `otlpEndpoint` (hoặc `OTEL_EXPORTER_OTLP_ENDPOINT` trên ứng dụng) để cũng chuyển tiếp sang trình thu thập.
- Quy tắc `scale` Container Apps (min/max) được kết nối trong Bicep.

## Tác nhân sao chép + Key Vault (S5)

`deploy/azure/main.bicep` cũng cung cấp **copy-agent** Container App lưu trữ `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) với **không ingress** — worker giữ các ổ cắm cTrader tồn tại lâu dài. Đọc chuỗi kết nối DB từ bí mật **Azure Key Vault** thông qua **danh tính được quản lý được gán bởi người dùng** (vai trò Người dùng Bí mật Key Vault) thay vì bí mật văn bản thuần nội tuyến. `NodeName` mặc định của mỗi bản sao thành tên máy chủ container của nó (duy nhất), vì vậy cho thuê DB các thuộc tính hồ sơ chạy cho mỗi bản sao và hai bản sao không bao giờ tự lưu trữ kép một. Mở rộng quy mô `minReplicas`/`maxReplicas` để thêm dung lượng sao chép; vòng khóa DataProtection dùng chung thông qua Postgres, vì vậy bất kỳ bản sao nào cũng có thể giải mã các mã thông báo Open API được lưu trữ. Đầu ra: `copyAgentName`, `keyVaultName`.
