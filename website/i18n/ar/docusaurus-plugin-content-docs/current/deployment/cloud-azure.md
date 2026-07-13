---
description: "deploy/azure/main.bicep ينص الطبقة بدون حالة على Azure Container Apps بالإضافة إلى Postgres Flexible Server + Log Analytics."
---

# نشر Azure - خطوة بخطوة

`deploy/azure/main.bicep` ينص الطبقة بدون حالة على **Azure Container Apps** بالإضافة إلى **Postgres Flexible Server** + Log Analytics.

## 1. المتطلبات الأساسية

- Azure CLI (`az login` تم) و subscription والإذن بإنشاء مجموعات resources.
- ثلاث صور مدفوعة إلى registry Azure يمكنها السحب (مثل GHCR public أو ACR).

## 2. إنشاء مجموعة resources

```bash
az group create -n cmind-rg -l westeurope
```

## 3. نشر Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

ينشئ: بيئة Container Apps و Web (external ingress) و MCP (external ingress) و Postgres Flexible Server + `appdb` و Log Analytics و **workspace-based Application Insights** component. اكتشاف لويب. سلسلة الاتصال الخاصة بها تُحقن في الويب + MCP كـ `APPLICATIONINSIGHTS_CONNECTION_STRING`، لذا traces + metrics export أصلي إلى App Insights بينما السجلات الهبوط في نفس workspace Log Analytics — لا المجمع مطلوب. Pass `-p otlpEndpoint=...` إلى *أيضاً* إلى المجمع OTLP.

## 4. الحصول على عناوين URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl و mcpUrl
```

فتح `webUrl` و تسجيل الدخول مع المالك (مُجبر على تغيير كلمة المرور عند أول تسجيل دخول).

## 5. إضافة وكلاء العقدة (منفصل)

Container Apps لا يمكن تشغيل امتيازات/DinD لذا تشغيل الوكلاء في مكان آخر و الإشارة إلى `webUrl`:

- **AKS** — نشر Helm chart ([kubernetes.md](kubernetes.md)) مع `nodeAgent.privileged=true` و تدرج الويب/MCP إلى 0 إذا كان يريد فقط طبقة الوكيل هناك.
- **VM / VMSS** — تشغيل صورة `cmind-node-agent` `--privileged` مع `NodeAgent:MainUrl=<webUrl>` و `NodeAgent:AdvertiseUrl=<vm reachable url>` و `NodeAgent:JwtSecret=<discoveryJoinToken>`.

الوكلاء التسجيل الذاتي خلال فترة نبض واحد - انظر [../operations/node-discovery.md](../operations/node-discovery.md).
