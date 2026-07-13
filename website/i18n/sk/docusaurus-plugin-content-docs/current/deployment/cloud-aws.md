---
description: "deploy/aws = Terraform modul: ECS Fargate (Web + MCP) za ALB, RDS Postgres, CloudWatch záznamy."
---

# Nasadenie AWS — krok za krokom

`deploy/aws` = Terraform modul: **ECS Fargate** (Web + MCP) za **ALB**, **RDS Postgres**, CloudWatch záznamy.

## 1. Požiadavky

- Terraform ≥ 1.5 + AWS poverenia (`aws configure` / premenné prostredia) s právami na vytvorenie prostredkov s rozsahom VPC, ECS, RDS, ALB, IAM.
- Tri obrazy v registri, ktoré môže ECS ťahať (ECR alebo GHCR verejný).

## 2. Inicializácia

```bash
cd deploy/aws
terraform init
```

## 3. Aplikovanie

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Vytvorí: RDS Postgres (`appdb`), ECS cluster, Fargate služby pre Web + MCP, ALB (Web na `/`, MCP na `/mcp`), skupiny bezpečnosti, CloudWatch skupinu záznamov, **ADOT (AWS Distro for OpenTelemetry) kolektor vedľajší proces** v každej úlohe. Aplikácia exportuje OTLP do vedľajšieho procesu, ktorý posiela stopy do **X-Ray**, metriky do **CloudWatch** (EMF, meno priestoru `cmind`); záznamy zostávajú na `awslogs` ovládači ako kompaktný JSON. Discovery je zapnutý pre Web. Úloha úlohy udeľuje vedľajšiemu procesu prístup na zápis X-Ray + CloudWatch — nie je potrebné spúšťať kolektor sami.

> Používa **východzí VPC/podsieť** účtu pre stručnosť. Pre produkciu si zapojte vlastný VPC, privátne podsietě, poslucháč HTTPS (certifikát ACM).

## 4. Získajte adresy URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Otvorte `web_url`, prihláste sa s vlastníkom (vynútená zmena hesla pri prvom prihlásení).

## 5. Pridajte uzly agenta (oddelene)

Fargate neumožňuje privilegovaný/DinD, takže spúšťajte agentov inde a zamerajte sa na `web_url`:

- **ECS na EC2** — poskytovateľ kapacity s `privileged = true` definíciami úloh spúšťajúcimi `cmind-node-agent`.
- **EKS** — Helm graf ([kubernetes.md](kubernetes.md)) s `nodeAgent.privileged=true`.

Nastavte `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Agenti sa samoreg istrujú — pozrite [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Overenie

```bash
aws logs tail /ecs/cmind --since 5m         # kompaktné JSON záznamy
curl -s "$(terraform output -raw web_url)/version"
```

## Poznámky k produkcii

- Pridajte poslucháč HTTPS + certifikát ACM; obmedzite skupinu bezpečnosti ALB.
- Uložte tajné kódy v AWS Secrets Manager / SSM, injektujte cez `secrets` definície úloh namiesto úplne otvorených `environment`.
- Povolte RDS Multi-AZ + zálohy.
- Záznamy (X-Ray), metriky (CloudWatch EMF), záznamy (CloudWatch Logs) sú zapojené automaticky cez vedľajší proces ADOT; korelujte na `trace_id`. Pozrite [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Aplikácia už zameráva `OTEL_EXPORTER_OTLP_ENDPOINT` na vedľajší proces v úlohe; zamerajte sa na externý kolektor, ak uprednostňujete centralizáciu.

## Agent kopírovania + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` pridáva službu **kopírovania-agenta** ECS Fargate hostujúcu `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) bez **ALB** — pracovník držiaci dlhodobé zásuvky cTrader. Reťazec pripojenia DB je uložený v **AWS Secrets Manager**, injektovaný cez `secrets` blok úlohy (role spúšťania udelené `secretsmanager:GetSecretValue` len na danú tajnosť), nie úplne otvorené prostredie. Každá `NodeName` úlohy sa štandardne nastavuje na jej názov kontajnera (jedinečný na úlohu Fargate), takže atribúty pronájmu DB spúšťajúce profily na úlohu — dve úlohy nikdy nesúčasne hostia jednu. Zvýšte kapacitu kópií zmenou `copy_agent_count`; kľúčový kruh DataProtection sa delí cez Postgres, takže akákoľvek úloha môže dešifrovať uložené tokeny Open API.
