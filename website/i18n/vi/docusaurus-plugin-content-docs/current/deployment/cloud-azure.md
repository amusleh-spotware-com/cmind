---
description: "deploy/azure/main.bicep cấp phát stateless tier trên Azure Container Apps cộng với Postgres Flexible Server + Log Analytics."
---

# Triển khai Azure — từng bước

`deploy/azure/main.bicep` cấp phát stateless tier trên **Azure Container Apps** cộng với **Postgres Flexible Server** + Log Analytics.

## 1. Điều kiện tiên quyết

- Azure CLI (`az login` done), subscription, permission tạo resource groups.
- Ba images pushed tới registry mà Azure có thể pull (ví dụ GHCR public, hoặc ACR).

## 2. Tạo resource group

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

Tạo: Container Apps environment, Web (external ingress), MCP (external ingress), Postgres Flexible Server + `appdb`, Log Analytics, **workspace-based Application Insights** component. Discovery bật cho Web. Connection string của nó injected vào Web + MCP như `APPLICATIONINSIGHTS_CONNECTION_STRING`, nên traces + metrics export natively tới App Insights trong khi logs land trong same Log Analytics workspace — không cần collector. Truyền `-p otlpEndpoint=...` để *cũng* forward tới OTLP collector.

## 4. Lấy URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Mở `webUrl`, đăng nhập với owner (bắt buộc password change lần đăng nhập đầu tiên).

## 5. Thêm node agents (riêng biệt)

Container Apps không thể chạy privileged/DinD, nên chạy agents ở nơi khác, trỏ tới `webUrl`:

- **AKS** — triển khai Helm chart ([kubernetes.md](kubernetes.md)) với `nodeAgent.privileged=true`, scale Web/MCP tới 0 nếu chỉ muốn agent tier ở đó.
- **VM / VMSS** — chạy `cmind-node-agent` image `--privileged` với `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agents tự đăng ký trong vòng một heartbeat interval — xem [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Xác minh

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # compact JSON logs
curl -s <webUrl>/version
```

## Ghi chú production

- Front Web với Azure Front Door / App Gateway cho TLS + WAF.
- Lưu trữ secrets trong Key Vault; truyền stable Data Protection cert (`App__DataProtectionCertBase64` / `...Password`) nên key ring sống sót qua replica restarts.
- App Insights (traces+metrics) + Log Analytics (logs) kết nối tự động; correlate trên `trace_id`. Xem [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Đặt `otlpEndpoint` param (hoặc `OTEL_EXPORTER_OTLP_ENDPOINT` trên apps) để *cũng* forward tới collector.
- Container Apps `scale` rules (min/max) wired trong Bicep.

## Copy-trading agent + Key Vault (S5)

`deploy/azure/main.bicep` cũng cấp phát **copy-agent** Container App lưu trữ `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) mà không có ingress — worker giữ long-lived cTrader sockets. Reads DB connection string từ **Azure Key Vault** secret qua **user-assigned managed identity** (Key Vault Secrets User role) thay vì inline plaintext secret. Mỗi replica's `NodeName` mặc định là container hostname của nó (unique), nên DB lease attributes chạy profiles per replica và hai replicas không bao giờ double-host một. Scale `minReplicas`/`maxReplicas` để thêm copy capacity; DataProtection key ring chia sẻ qua Postgres, nên bất kỳ replica nào có thể decrypt lưu trữ Open API tokens. Outputs: `copyAgentName`, `keyVaultName`.
