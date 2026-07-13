---
description: "deploy/aws = Terraform-Modul: ECS Fargate (Web + MCP) hinter ALB, RDS Postgres, CloudWatch-Protokolle."
---

# AWS-Bereitstellung — Schritt für Schritt

`deploy/aws` = Terraform-Modul: **ECS Fargate** (Web + MCP) hinter **ALB**, **RDS Postgres**, CloudWatch-Protokolle.

## 1. Voraussetzungen

- Terraform ≥ 1.5 + AWS-Anmeldedaten (`aws configure` / Umgebungsvariablen) mit Rechten zum Erstellen von VPC-bezogenen Ressourcen, ECS, RDS, ALB, IAM.
- Drei Images in einer Registry, die ECS abrufen kann (ECR oder GHCR öffentlich).

## 2. Initialisierung

```bash
cd deploy/aws
terraform init
```

## 3. Anwenden

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Erstellt: RDS Postgres (`appdb`), ECS-Cluster, Fargate-Services für Web + MCP, ALB (Web unter `/`, MCP unter `/mcp`), Sicherheitsgruppen, CloudWatch-Protokollgruppe, **ADOT (AWS Distro for OpenTelemetry) Collector-Sidecar** in jeder Task. Die App exportiert OTLP an den Sidecar, der Traces an **X-Ray** und Metriken an **CloudWatch** (EMF, Namespace `cmind`) versendet; Protokolle bleiben auf dem `awslogs`-Treiber als kompaktes JSON. Ermittlung für Web aktiviert. Task-Rolle gewährt Sidecar X-Ray + CloudWatch-Schreibzugriff — kein Collector zum Selberlaufen erforderlich.

> Verwendet für Kürze das **Standard-VPC/Subnets** des Kontos. Für die Produktion eigene VPC, private Subnetze, HTTPS-Listener (ACM-Zertifikat) verdrahten.

## 4. URLs abrufen

```bash
terraform output web_url   # ALB-Wurzel
terraform output mcp_url   # ALB /mcp
```

Öffnen Sie `web_url`, melden Sie sich mit dem Besitzer an (erzwungener Passwortänderung beim ersten Login).

## 5. Node-Agenten hinzufügen (separate)

Fargate verbietet privilegierte/DinD, daher führen Sie Agenten an anderer Stelle aus und verweisen auf `web_url`:

- **ECS auf EC2** — Kapazitätsanbieter mit Task-Definitionen `privileged = true` mit `cmind-node-agent`.
- **EKS** — Helm-Diagramm ([kubernetes.md](kubernetes.md)) mit `nodeAgent.privileged=true`.

Setzen Sie `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent erreichbare url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Agenten registrieren sich selbst — siehe [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Überprüfung

```bash
aws logs tail /ecs/cmind --since 5m         # kompakte JSON-Protokolle
curl -s "$(terraform output -raw web_url)/version"
```

## Produktionshinweise

- Fügen Sie HTTPS-Listener + ACM-Zertifikat hinzu; beschränken Sie ALB-Sicherheitsgruppe.
- Speichern Sie Geheimnisse in AWS Secrets Manager / SSM, injizieren Sie über Task-Definition `secrets` anstelle von Klartext `environment`.
- Aktivieren Sie RDS Multi-AZ + Sicherungen.
- Traces (X-Ray), Metriken (CloudWatch EMF), Protokolle (CloudWatch Logs) werden automatisch über ADOT-Sidecar verdrahtet; korrelieren Sie auf `trace_id`. Siehe [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Die App verweist bereits auf in-task Sidecar `OTEL_EXPORTER_OTLP_ENDPOINT`; verweisen Sie auf externen Collector um, wenn Sie es vorziehen zu zentralisieren.

## Copy-Trading-Agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` fügt **copy-agent** ECS Fargate-Service hinzu, der `CopyEngineSupervisor` hostet (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) ohne **ALB** — Worker, der langlebige cTrader-Sockets hält. DB-Verbindungszeichenfolge gespeichert in **AWS Secrets Manager**, injiziert über den `secrets`-Block der Task (Ausführungsrolle gewährt `secretsmanager:GetSecretValue` nur auf diesem Secret), nicht Klartext env. `NodeName` jeder Task standardmäßig auf ihrem Container-Hostnamen (eindeutig pro Fargate-Task), daher DB-Lease-Attribute ausführende Profile pro Task — zwei Tasks hosten niemals eine doppelt. Skalieren Sie `copy_agent_count`, um Kopierkapazität zu erhöhen; DataProtection-Schlüsselring, der über Postgres freigegeben wird, daher kann jede Task gespeicherte Open-API-Token entschlüsseln.
