---
description: "deploy/aws = Terraform module: ECS Fargate (Web + MCP) خلف ALB و RDS Postgres و CloudWatch logs."
---

# النشر على AWS — خطوة بخطوة

`deploy/aws` = وحدة Terraform: **ECS Fargate** (Web + MCP) خلف **ALB** و **RDS Postgres** و CloudWatch logs.

## 1. المتطلبات الأساسية

- Terraform ≥ 1.5 + بيانات اعتماد AWS (`aws configure` / متغيرات env) بحقوق إنشاء موارد بنطاق VPC و
  ECS و RDS و ALB و IAM.
- ثلاث صور في سجل يمكن لـ ECS سحبها (ECR أو GHCR عام).

## 2. التهيئة

```bash
cd deploy/aws
terraform init
```

## 3. التطبيق

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

يصنع: RDS Postgres (`appdb`)، عنقود ECS، خدمات Fargate للويب + MCP و ALB (Web في `/`،
MCP في `/mcp`) ومجموعات الأمان وسجل CloudWatch log و **ADOT (AWS Distro for
OpenTelemetry) collector sidecar** في كل مهمة. التطبيق تصدير OTLP إلى sidecar، الذي ينقل
آثار إلى **X-Ray** والقياسات إلى **CloudWatch** (EMF، namespace `cmind`)؛ السجلات البقاء على
`awslogs` driver كـ JSON مضغوط. الاكتشاف على للويب. دور المهمة منح sidecar
X-Ray + CloudWatch كتابة access — بدون collector للتشغيل بنفسك.

> يستخدم حساب **default VPC/subnets** للإيجاز. للإنتاج، سلك VPC الخاص بك وخصوص
> subnets ومستمع HTTPS (ACM cert).

## 4. احصل على عناوين URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

فتح `web_url`، تسجيل دخول مع المالك (تغيير كلمة المرور الإجباري على تسجيل الدخول الأول).

## 5. إضافة عملاء العقدة (منفصل)

Fargate يحظر امتياز/DinD، لذلك قم بتشغيل الوكلاء في مكان آخر يشير إلى `web_url`:

- **ECS على EC2** — مزود السعة مع تعريفات المهام `privileged = true`
  `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) مع `nodeAgent.privileged=true`.

اضبط `NodeAgent__MainUrl=<web_url>` و`NodeAgent__AdvertiseUrl=<agent reachable url>` و
`NodeAgent__JwtSecret=<discovery_join_token>`. الوكلاء يسجل ذاتي — انظر
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. التحقق

```bash
aws logs tail /ecs/cmind --since 5m         # سجلات JSON مضغوطة
curl -s "$(terraform output -raw web_url)/version"
```

## ملاحظات الإنتاج

- أضف مستمع HTTPS + شهادة ACM؛ تقييد مجموعة الأمان ALB.
- تخزين الأسرار في AWS Secrets Manager / SSM والحقن عبر task-definition `secrets` بدلاً من
  `environment` النص العادي.
- تمكين RDS Multi-AZ + backups.
- آثار (X-Ray) والقياسات (CloudWatch EMF) والسجلات (CloudWatch Logs) موصولة تلقائيًا عبر
  sidecar ADOT؛ ربط على `trace_id`. انظر
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- يشير التطبيق بالفعل `OTEL_EXPORTER_OTLP_ENDPOINT` في sidecar in-task؛ أعد الإشارة إلى خارجي
  المجمع إذا كنت تفضل المركزية.

## عامل النسخ التجاري + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` يضيف **copy-agent** ECS Fargate service استضافة `CopyEngineSupervisor`
(`App:Copy:Enabled=true` و`App:Features:CopyTrading=true`) مع **no ALB** — عامل يحمل long-lived
cTrader sockets. سلسلة الاتصال DB المخزنة في **AWS Secrets Manager** والمحقونة من خلال
task's `secrets` block (دور التنفيذ منح `secretsmanager:GetSecretValue` على هذا السر فقط) و
ليس env النص العادي. يفترض `NodeName` لكل مهمة اسم الحاوية الخاص بها (فريد لكل مهمة Fargate)، حتى
DB lease سمات تشغيل profiles لكل مهمة — لا تضيف مهمتان مرتين واحدة. مقياس
`copy_agent_count` لإضافة سعة النسخ؛ DataProtection key ring مشترك عبر Postgres، لذلك أي مهمة
يمكن فك تشفير رموز Open API المخزنة.
