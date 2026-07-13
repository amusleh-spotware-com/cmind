---
description: "deploy/aws = Terraform module: ECS Fargate (Web + MCP) behind ALB, RDS Postgres, CloudWatch logs."
---

# การปรับใช้บน AWS — ทีละขั้นตอน

`deploy/aws` = Terraform module: **ECS Fargate** (Web + MCP) อยู่หลัง **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. ข้อกำหนดเบื้องต้น

- Terraform ≥ 1.5 + AWS credentials (`aws configure` / env vars) พร้อมสิทธิในการสร้าง
  resources ที่เกี่ยวกับ VPC, ECS, RDS, ALB, IAM.
- สามอิมเมจในระบบ registry ที่ ECS สามารถดึงได้ (ECR หรือ GHCR public).

## 2. เริ่มต้น

```bash
cd deploy/aws
terraform init
```

## 3. นำไปใช้

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

สร้าง: RDS Postgres (`appdb`), ECS cluster, Fargate services สำหรับ Web + MCP, ALB (Web ที่ `/`,
MCP ที่ `/mcp`), security groups, CloudWatch log group, **ADOT (AWS Distro for
OpenTelemetry) collector sidecar** ในแต่ละ task แอปส่ง OTLP ไปยัง sidecar ซึ่งส่ง
traces ไปยัง **X-Ray** เมตริกส์ไปยัง **CloudWatch** (EMF, namespace `cmind`); logs อยู่บน
`awslogs` driver เป็น compact JSON Discovery เปิดสำหรับ Web Task role ให้สิทธิ sidecar
X-Ray + CloudWatch write access — ไม่มี collector ให้เรียกใช้เอง

> ใช้ **default VPC/subnets** ของบัญชีเพื่อความสั้น สำหรับ production ให้เชื่อมต่อ VPC เป็นของคุณเอง
> private subnets HTTPS listener (ACM cert)

## 4. รับ URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

เปิด `web_url` ลงชื่อเข้าใช้ด้วย owner (บังคับให้เปลี่ยนรหัสผ่านเมื่อเข้าสู่ระบบครั้งแรก)

## 5. เพิ่ม node agents (แยกต่างหาก)

Fargate ไม่อนุญาต privileged/DinD ดังนั้นให้เรียกใช้ agents ที่อื่นที่ชี้ไปที่ `web_url`:

- **ECS on EC2** — capacity provider พร้อมคำจำกัด `privileged = true` task definitions ที่เรียกใช้
  `cmind-node-agent`
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) พร้อมคำจำกัด `nodeAgent.privileged=true`

ตั้ง `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>` Agents ลงทะเบียนด้วยตนเอง — ดู
[../operations/node-discovery.md](../operations/node-discovery.md)

## 6. ยืนยัน

```bash
aws logs tail /ecs/cmind --since 5m         # compact JSON logs
curl -s "$(terraform output -raw web_url)/version"
```

## หมายเหตุ Production

- เพิ่ม HTTPS listener + ACM certificate; จำกัด ALB security group
- เก็บ secrets ใน AWS Secrets Manager / SSM ฉีดผ่าน task-definition `secrets` แทนที่
  plaintext `environment`
- เปิดใช้ RDS Multi-AZ + backups
- Traces (X-Ray) เมตริกส์ (CloudWatch EMF) logs (CloudWatch Logs) เชื่อมต่ออัตโนมัติผ่าน
  ADOT sidecar; ตรวจสอบ `trace_id` ดู
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar)
- แอปชี้ `OTEL_EXPORTER_OTLP_ENDPOINT` ที่ in-task sidecar; ชี้ไปยัง external
  collector หากต้องการรวมศูนย์

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` เพิ่ม **copy-agent** ECS Fargate service ที่โฮสต์ `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) ไม่มี ALB — worker ที่ถือ long-lived
cTrader sockets DB connection string เก็บไว้ใน **AWS Secrets Manager** ฉีดผ่าน
task's `secrets` block (execution role ให้สิทธิ `secretsmanager:GetSecretValue` บน secret นั้นเท่านั้น)
ไม่ใช่ plaintext env แต่ละ task's `NodeName` เป็นค่าเริ่มต้นสำหรับ container hostname ของมัน (ไม่ซ้ำกันต่อ Fargate task)
ดังนั้น DB lease attributes รันโปรไฟล์ต่อ task — สองงาน ไม่ดำเนินการ double-host ใดคนหนึ่ง ปรับขนาด
`copy_agent_count` เพื่อเพิ่มคุณสมบัติ copy; DataProtection key ring ใช้ร่วมกันผ่าน Postgres ดังนั้นงาน ใดคนหนึ่ง
สามารถถอดรหัส stored Open API tokens
