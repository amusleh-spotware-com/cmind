---
description: "deploy/aws = Terraform modul: ECS Fargate (Web + MCP) zadaj ALB, RDS Postgres, CloudWatch dnevniki."
---

# Postavitev AWS — korak za korakom

`deploy/aws` = Terraform modul: **ECS Fargate** (Web + MCP) zadaj **ALB**, **RDS Postgres**, CloudWatch dnevniki.

## 1. Predpogoji

- Terraform ≥ 1.5 + AWS poverilnice (`aws configure` / spremenljivke okolice) s pravicami za VPC-obsežne
  vire, ECS, RDS, ALB, IAM.
- Tri slike v registru, ki ga lahko ECS povlečkuje (ECR ali GHCR javno).

## 2. Inicializacija

```bash
cd deploy/aws
terraform init
```

## 3. Uporabi

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Naredi: RDS Postgres (`appdb`), ECS gruča, Fargate storitve za Web + MCP, ALB (Web na `/`,
MCP na `/mcp`), varnostne skupine, CloudWatch skupino dnevnikov, **ADOT (AWS Distro za
OpenTelemetry) zbiralnik sidecar** v vsaki nalogi. Aplikacija izvozi OTLP na sidecar, ki pošlje
sledi na **X-Ray**, metrike na **CloudWatch** (EMF, imenski prostor `cmind`); dnevniki ostanejo na
gonilnik `awslogs` kot kompakten JSON. Odkrivanje za Web. Vloga naloge daje sidecar
X-Ray + dostop za pisanje CloudWatch — nobenega zbiranja za samimi teči.

> Uporablja **privzeto VPC/podomrežja** računa za krajšo. Za proizvodnjo, žico lastne VPC, zasebne
> podomrežja, HTTPS poslušalec (ACM certifikat).

## 4. Pridobite naslove URL

```bash
terraform output web_url   # ALB koren
terraform output mcp_url   # ALB /mcp
```

Odprite `web_url`, se prijavite z lastnikom (prisilna sprememba gesla ob prvem prijavi).

## 5. Dodajte vozlišče agente (ločeno)

Fargate zavrne privilegiran/DinD, zato tečete agente drugje, ki kažejo na `web_url`:

- **ECS na EC2** — ponudnik zmogljivosti s `privileged = true` definicijami nalog, ki tečejo
  `cmind-node-agent`.
- **EKS** — Helm grafikon ([kubernetes.md](kubernetes.md)) z `nodeAgent.privileged=true`.

Nastavite `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent dosegljiv URL>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Agenti se samogostovajo — glej
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Potrdite

```bash
aws logs tail /ecs/cmind --since 5m         # kompaktni JSON dnevniki
curl -s "$(terraform output -raw web_url)/version"
```

## Produkcijske opombe

- Dodajte HTTPS poslušalec + ACM certifikat; omejite varnostno skupino ALB.
- Shranjujte skrivnosti v AWS Secrets Manager / SSM, injicirajte preko naloge-definicije `secrets` namesto
  golo besedilo `environment`.
- Omogočite RDS Multi-AZ + varnostne kopije.
- Sledi (X-Ray), metrike (CloudWatch EMF), dnevniki (CloudWatch Logs) žičani avtomatično s
  ADOT sidcar; korelativno na `trace_id`. Glej
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Aplikacija že kaže `OTEL_EXPORTER_OTLP_ENDPOINT` na sidecar v nalogi; ponovno usmerite na zunanji
  zbiralnik, če raje centralizirate.

## Agent kopiranja + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` dodaj **copy-agent** ECS Fargate storitev gostovanje `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) z **brez ALB** — delavec, ki drži dolgoživje
cTrader vtičnike. Niz povezave DB shranjen v **AWS Secrets Manager**, injiciran preko
naloge `secrets` blok (vloga izvedbe dodelila `secretsmanager:GetSecretValue` samo na to skrivnost),
ne golo besediloenv. Vsake naloge `NodeName` privzeto njene vsebnik ime (edinstveno na Fargate nalogo), torej
DB zakupa atributov tečeče profile na nalogo — dve nalogi nikoli ne sogostuje ene. Lestvica
`copy_agent_count` za dodajanje kopije zmogljivosti; DataProtection ključni prsten deliti skozi Postgres, zato vsaka naloga
lahko dešifrira shranjene Open API žetone.
