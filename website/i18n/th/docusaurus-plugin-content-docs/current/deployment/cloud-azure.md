---
description: "deploy/azure/main.bicep provisions stateless tier on Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# การปรับใช้บน Azure — ทีละขั้นตอน

`deploy/azure/main.bicep` มี stateless tier บน **Azure Container Apps** บวก **Postgres Flexible Server** + Log Analytics

## 1. ข้อกำหนดเบื้องต้น

- Azure CLI (`az login` เสร็จแล้ว) subscription สิทธิในการสร้าง resource groups
- สามอิมเมจ push ไปยัง registry ที่ Azure สามารถดึงได้ (เช่น GHCR public หรือ ACR)

## 2. สร้าง resource group

```bash
az group create -n cmind-rg -l westeurope
```

## 3. ปรับใช้ Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

สร้าง: Container Apps environment Web (external ingress) MCP (external ingress) Postgres Flexible Server + `appdb` Log Analytics **workspace-based Application Insights** component Discovery เปิดสำหรับ Web connection string ของมันถูกฉีดเข้า Web + MCP เป็น `APPLICATIONINSIGHTS_CONNECTION_STRING` ดังนั้น traces + metrics export natively ไปยัง App Insights ขณะที่ logs ลงใน Log Analytics workspace เดียวกัน — ไม่มี collector ที่จำเป็น ผ่าน `-p otlpEndpoint=...` เพื่อ *ยัง* forward ไปยัง OTLP collector

## 4. รับ URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

เปิด `webUrl` ลงชื่อเข้าใช้ด้วย owner (บังคับให้เปลี่ยนรหัสผ่านเมื่อเข้าสู่ระบบครั้งแรก)

## 5. เพิ่ม node agents (แยกต่างหาก)

Container Apps ไม่สามารถเรียกใช้ privileged/DinD ได้ ดังนั้นให้เรียกใช้ agents ที่อื่น ชี้ไปที่ `webUrl`:

- **AKS** — ปรับใช้ Helm chart ([kubernetes.md](kubernetes.md)) พร้อมคำจำกัด `nodeAgent.privileged=true` ปรับขนาด Web/MCP เป็น 0 หากต้องการเพียงแค่ agent tier ที่นั่น
- **VM / VMSS** — เรียกใช้ `cmind-node-agent` image `--privileged` พร้อมคำจำกัด `NodeAgent:MainUrl=<webUrl>` `NodeAgent:AdvertiseUrl=<vm reachable url>` `NodeAgent:JwtSecret=<discoveryJoinToken>`

Agents ลงทะเบียนด้วยตนเองภายในช่วงเวลา heartbeat หนึ่ง — ดู [../operations/node-discovery.md](../operations/node-discovery.md)

## 6. ยืนยัน

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # compact JSON logs
curl -s <webUrl>/version
```

## หมายเหตุ Production

- Front Web ด้วย Azure Front Door / App Gateway สำหรับ TLS + WAF
- เก็บ secrets ใน Key Vault; ผ่าน stable Data Protection cert (`App__DataProtectionCertBase64` / `...Password`) ดังนั้น key ring ยังคงอยู่หลังจาก replica restarts
- App Insights (traces+metrics) + Log Analytics (logs) เชื่อมต่ออัตโนมัติ; ตรวจสอบ `trace_id` ดู [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics)
- ตั้ง `otlpEndpoint` param (หรือ `OTEL_EXPORTER_OTLP_ENDPOINT` บน apps) เพื่อ *ยัง* forward ไปยัง collector
- Container Apps `scale` rules (min/max) เชื่อมต่อใน Bicep

## Copy-trading agent + Key Vault (S5)

`deploy/azure/main.bicep` ยังมี **copy-agent** Container App ที่โฮสต์ `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) ไม่มี ingress — worker ที่ถือ long-lived cTrader sockets อ่าน DB connection string จาก **Azure Key Vault** secret ผ่าน **user-assigned managed identity** (Key Vault Secrets User role) แทนที่จะเป็น inline plaintext secret แต่ละ replica's `NodeName` เป็นค่าเริ่มต้นสำหรับ container hostname ของมัน (ไม่ซ้ำกัน) ดังนั้น DB lease attributes รันโปรไฟล์ต่อ replica และสองreplicas ไม่ได้ double-host ใดคนหนึ่ง ปรับขนาด `minReplicas`/`maxReplicas` เพื่อเพิ่มคุณสมบัติ copy; DataProtection key ring ใช้ร่วมกันผ่าน Postgres ดังนั้น replica ใดคนหนึ่ง สามารถถอดรหัส stored Open API tokens Outputs: `copyAgentName`, `keyVaultName`
