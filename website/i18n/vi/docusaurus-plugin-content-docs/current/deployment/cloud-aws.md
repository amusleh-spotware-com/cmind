---
description: "deploy/aws = Mô-đun Terraform: ECS Fargate (Web + MCP) phía sau ALB, RDS Postgres, CloudWatch logs."
---

# Triển khai AWS — từng bước

`deploy/aws` = Mô-đun Terraform: **ECS Fargate** (Web + MCP) phía sau **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. Điều kiện tiên quyết

- Terraform ≥ 1.5 + AWS credentials (`aws configure` / env vars) có quyền tạo VPC-scoped resources, ECS, RDS, ALB, IAM.
- Ba images trong registry mà ECS có thể pull (ECR, hoặc GHCR public).

## 2. Khởi tạo

```bash
cd deploy/aws
terraform init
```

## 3. Áp dụng

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Tạo: RDS Postgres (`appdb`), ECS cluster, Fargate services cho Web + MCP, ALB (Web tại `/`, MCP tại `/mcp`), security groups, CloudWatch log group, **ADOT (AWS Distro for OpenTelemetry) collector sidecar** trong mỗi task. Ứng dụng xuất OTLP tới sidecar, nó gửi traces tới **X-Ray**, metrics tới **CloudWatch** (EMF, namespace `cmind`); logs vẫn trên `awslogs` driver dưới dạng JSON compact. Discovery được bật cho Web. Task role cấp sidecar quyền truy cập X-Ray + CloudWatch — không cần chạy collector riêng.

> Sử dụng **default VPC/subnets** của tài khoản để đơn giản. Đối với production, kết nối VPC riêng, private subnets, HTTPS listener (ACM cert).

## 4. Lấy URLs

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Mở `web_url`, đăng nhập với owner (bắt buộc thay đổi password lần đăng nhập đầu tiên).

## 5. Thêm node agents (riêng biệt)

Fargate không cho phép privileged/DinD, nên chạy agents ở nơi khác trỏ tới `web_url`:

- **ECS on EC2** — capacity provider với các task definitions `privileged = true` chạy `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) với `nodeAgent.privileged=true`.

Đặt `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Agents tự đăng ký — xem [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Xác minh

```bash
aws logs tail /ecs/cmind --since 5m         # compact JSON logs
curl -s "$(terraform output -raw web_url)/version"
```

## Ghi chú production

- Thêm HTTPS listener + ACM certificate; hạn chế security group của ALB.
- Lưu trữ secrets trong AWS Secrets Manager / SSM, inject qua task-definition `secrets` thay vì plaintext `environment`.
- Bật RDS Multi-AZ + backups.
- Traces (X-Ray), metrics (CloudWatch EMF), logs (CloudWatch Logs) được kết nối tự động qua ADOT sidecar; correlate trên `trace_id`. Xem [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Ứng dụng đã trỏ `OTEL_EXPORTER_OTLP_ENDPOINT` tới in-task sidecar; repoint tới external collector nếu bạn thích tập trung hóa.

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` thêm **copy-agent** ECS Fargate service lưu trữ `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) mà không có ALB — worker giữ long-lived cTrader sockets. DB connection string được lưu trữ trong **AWS Secrets Manager**, injected thông qua task's `secrets` block (execution role được cấp `secretsmanager:GetSecretValue` chỉ trên secret đó), không phải plaintext env. Mỗi task's `NodeName` mặc định là hostname của container (unique per Fargate task), nên DB lease attributes chạy profiles per task — hai tasks không bao giờ host kép một. Scale `copy_agent_count` để thêm copy capacity; DataProtection key ring được chia sẻ thông qua Postgres, nên bất kỳ task nào cũng có thể decrypt lưu trữ Open API tokens.
