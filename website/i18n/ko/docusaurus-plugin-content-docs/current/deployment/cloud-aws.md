---
description: "deploy/aws = Terraform 모듈: ECS Fargate (Web + MCP)는 ALB 뒤에, RDS Postgres, CloudWatch 로그."
---

# AWS 배포 — 단계별

`deploy/aws` = Terraform 모듈: **ECS Fargate** (Web + MCP)는 **ALB** 뒤에, **RDS Postgres**, CloudWatch 로그.

## 1. 전제 조건

- Terraform ≥ 1.5 + AWS 자격 증명 (`aws configure` / 환경 변수) VPC 범위 리소스, ECS, RDS, ALB, IAM을 만들 권한.
- ECS가 풀 수 있는 세 개의 이미지 (ECR 또는 GHCR public).

## 2. 초기화

```bash
cd deploy/aws
terraform init
```

## 3. 적용

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

생성: RDS Postgres (`appdb`), ECS 클러스터, Web + MCP용 Fargate 서비스, ALB (Web at `/`, MCP at `/mcp`), 보안 그룹, CloudWatch 로그 그룹, 각 작업의 **ADOT (AWS Distro for OpenTelemetry) 컬렉터 사이드카**. 앱은 OTLP를 사이드카로 내보내고, 사이드카는 **X-Ray**로 추적을, **CloudWatch** (EMF, 네임스페이스 `cmind`)로 메트릭을 ship합니다; 로그는 `awslogs` 드라이버의 압축 JSON으로 유지됩니다. Web용 디스커버리 켜기. 작업 역할은 사이드카 X-Ray + CloudWatch 쓰기 권한을 부여합니다 — 컬렉터를 직접 실행할 필요 없습니다.

> 테스트를 위해 **기본 VPC/서브넷**을 사용합니다. 프로덕션의 경우 자체 VPC, 프라이빗 서브넷, HTTPS 리스너(ACM 인증서)를 연결하세요.

## 4. URL 가져오기

```bash
terraform output web_url   # ALB 루트
terraform output mcp_url   # ALB /mcp
```

`web_url`을 열고 소유자로 로그인합니다 (첫 로그인 시 비밀번호 변경 강제).

## 5. 노드 에이전트 추가 (별도)

Fargate는 권한/DinD를 허용하지 않으므로 에이전트를 다른 곳에서 실행하고 `web_url`을 지정합니다:

- **EC2의 ECS** — `privileged = true` 작업 정의를 사용하여 `cmind-node-agent`를 실행하는 용량 공급자.
- **EKS** — `nodeAgent.privileged=true`로 Helm 차트 ([kubernetes.md](kubernetes.md)).

`NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<에이전트 도달 가능 URL>`, `NodeAgent__JwtSecret=<discovery_join_token>`을 설정합니다. 에이전트는 자체 등록됩니다 — [../operations/node-discovery.md](../operations/node-discovery.md) 참조.

## 6. 검증

```bash
aws logs tail /ecs/cmind --since 5m         # 압축 JSON 로그
curl -s "$(terraform output -raw web_url)/version"
```

## 프로덕션 참고

- HTTPS 리스너 + ACM 인증서 추가; ALB 보안 그룹 제한.
- 시크릿을 AWS Secrets Manager / SSM에 저장하고 작업 정의의 `secrets`로 주입하여 평문 `environment` 대신 사용.
- RDS Multi-AZ + 백업 활성화.
- 추적 (X-Ray), 메트릭 (CloudWatch EMF), 로그 (CloudWatch Logs)는 ADOT 사이드카를 통해 자동으로 연결됩니다; `trace_id`에서 상관관계. [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar) 참조.
- 앱은 이미 `OTEL_EXPORTER_OTLP_ENDPOINT`를 인-task 사이드카로 지정합니다; 중앙 집중화를 선호하면 외부 컬렉터로 repotoint.

## 복사 트레이딩 에이전트 + Secrets Manager (S5)

`deploy/aws/copy-agent.tf`는 `CopyEngineSupervisor`를 호스팅하는 **copy-agent** ECS Fargate 서비스를 추가합니다 (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **ALB 없음** — 작업이 긴 cTrader 소켓을 유지하는 워커. DB 연결 문자열은 **AWS Secrets Manager**에 저장되고 작업의 `secrets` 블록을 통해 주입됩니다 (실행 역할에 해당 시크릿에 대한 `secretsmanager:GetSecretValue` 부여), 평문 env가 아닙니다. 각 작업의 `NodeName`는 기본적으로 컨테이너 호스트네임(고유 per Fargate 작업)으로 기본 설정되므로 DB 임대는 실행 중인 프로필 per 작업 속성 — 두 작업이 하나의 프로필을 이중 호스트하지 않습니다. `copy_agent_count`를 확장하여 복사 용량 추가; DataProtection 키 링이 Postgres를 통해 공유되므로 모든 작업이 저장된 Open API 토큰을 해독할 수 있습니다.
