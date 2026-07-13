---
description: "deploy/aws = moduł Terraform: ECS Fargate (Web + MCP) za ALB, RDS Postgres, dzienniki CloudWatch."
---

# Wdrażanie AWS — krok po kroku

`deploy/aws` = moduł Terraform: **ECS Fargate** (Web + MCP) za **ALB**, **RDS Postgres**, dzienniki CloudWatch.

## 1. Wymagania wstępne

- Terraform ≥ 1.5 + poświadczenia AWS (`aws configure` / zmienne env) z prawami do zasobów VPC-scoped, ECS, RDS, ALB, IAM.
- Trzy obrazy w rejestrze, który ECS może pobrać (ECR, lub GHCR public).

## 2. Zainicjalizuj

```bash
cd deploy/aws
terraform init
```

## 3. Zastosuj

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Robi: RDS Postgres (`appdb`), klaster ECS, usługi Fargate dla Web + MCP, ALB (Web na `/`, MCP na `/mcp`), grupy bezpieczeństwa, grupę dzienników CloudWatch, **sidecar kolektora ADOT (AWS Distro for OpenTelemetry)** w każdym zadaniu. Aplikacja eksportuje OTLP do sidecara, który wysyła ślady do **X-Ray**, metryki do **CloudWatch** (EMF, przestrzeń nazw `cmind`); dzienniki pozostają na sterowniku `awslogs` jako kompaktowy JSON. Odkrycie włączone dla Web. Rola zadania przyznaje sidecarowi dostęp do zapisu X-Ray + CloudWatch — nie ma kolektora do uruchomienia sam.

> Używa domyślnych **VPC konta/podsieci** dla zwięzłości. Dla produkcji, przewód własny VPC, prywatne podsieci, słuchacz HTTPS (cert ACM).

## 4. Uzyskaj adresy URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Otwórz `web_url`, zaloguj się właścicielem (zmuszony zmiana hasła przy pierwszym logowaniu).

## 5. Dodaj agentów węzłów (oddzielnie)

Fargate zabrania uprzywilejowanych/DinD, więc uruchom agentów indziej wskazując na `web_url`:

- **ECS on EC2** — dostawca pojemności z `privileged = true` definicjami zadań uruchamiającymi `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) z `nodeAgent.privileged=true`.

Ustaw `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Agenci samodzielnie rejestrują się — patrz [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Zweryfikuj

```bash
aws logs tail /ecs/cmind --since 5m         # kompaktowe dzienniki JSON
curl -s "$(terraform output -raw web_url)/version"
```

## Notatki produkcyjne

- Dodaj słuchacz HTTPS + certyfikat ACM; ogranicz grupę bezpieczeństwa ALB.
- Przechowuj sekrety w AWS Secrets Manager / SSM, wtryskuj poprzez `secrets` definicji zadań zamiast plaintext `environment`.
- Włącz RDS Multi-AZ + kopie zapasowe.
- Ślady (X-Ray), metryki (CloudWatch EMF), dzienniki (CloudWatch Logs) przewodowała automatycznie poprzez sidecar ADOT; korelyuj na `trace_id`. Patrz [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Aplikacja już wskazuje `OTEL_EXPORTER_OTLP_ENDPOINT` na sidecar w-zadań; wskaż do kolektora zewnętrznego, jeśli wolisz scentralizować.

## Agent kopii + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` dodaje usługę **copy-agent** ECS Fargate hosting `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) z **bez ALB** — pracownik trzymający długotrwałe gniazda cTrader. Ciąg połączenia DB przechowywany w **AWS Secrets Manager**, wtryskiwany poprzez blok `secrets` zadania (rola wykonawcza przyznana `secretsmanager:GetSecretValue` tylko na tym sekrecie), nie plaintext env. `NodeName` każdego zadania domyślnie wynosi jej nazwę hosta kontenera (unikalna na Fargate zadanie), więc atrybuty leasingu DB profilami biegającymi na zadanie — dwa zadania nigdy nie gośćią jeden. Skaluj `copy_agent_count` do dodania pojemności kopii; pierścień klucza DataProtection współdzielony przez Postgres, więc każde zadanie może odszyfrować przechowywane tokeny Open API.
