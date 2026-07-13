---
slug: /for-cloud-providers
title: cMind pre cloud & VPS poskytovateľov
description: Prečo by cloud alebo VPS poskytovateľ mal ponúkať managed cMind hosting — hotový, diferencovaný produkt pre algo obchodníkov, brokerov a prop firmy, s jasným spôsobom monetizácie compute, white-label reselling a managed AI.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind pre cloud & VPS poskytovateľov 🖥️

Už prenajímате výpočetnú kapacitu. cMind je hotový, open-source produkt, ktorý môžete zabalit okolo tej
compute: **ponúknite managed cMind hosting** a prikryte si high-value, sticky, compute-hungry workload —
algoritmus obchodníci, brokeri, prop firmy a trading komunity, ktorí chcú platformu bežiacu
bez toho, aby sa stali ops tímom sami.

:::tip TL;DR
Spustite stateless tier + Postgres + node fleet; dajte zákazníkom branded URL. Monetizujte
subscription, compute, white-label a AI. → [Deploy do cloud](./deployment/cloud.md)
:::

## Prečo ponúkať managed cMind

- **Žiadne build náklady.** Je to open source, MIT-licensed a už zdokumentovaný, testovaný a containerizovaný.
  Balíte a prevádzkyujete — nerealizujete.
- **Diferencovaný produkt pre lukratívnu niku.** Algo trading je compute-hungry: backtesty a
  live uzly pálenia CPU, čo je *billable usage*, ktorý už predávate.
- **Sticky customers.** Obchodníci, ktorí stavajú a spúšťajú stratégie vnútri platformy nechúrnia ľahko.
- **Zmena caveat na upsell.** cMind je self-hosted by design — pre zákazníkov, ktorí "nechcú
  byť ops tím," *vy* ste odpoveď.

## Kto kupuje managed cMind od vás

- **Individuální quants & obchodníci** ktorí ho chcú hostovaní. → [Pre obchodníkov](./for-traders.md)
- **cTrader brokeri** spúšťajúci white-label pre svojich klientov. → [Pre brokerov](./for-brokers.md)
- **Prop firmy & copy-trading businesses** ktorý potrebujú branded, auditable infraštruktúru.

## Čo "managed cMind" znamená spúšťať

Prevádzkyujete tri tiers; zákazník dostáva branded web URL:

| Tier | Čo to je | Kde to beží |
|---|---|---|
| Stateless (Web + MCP) | Aplikácia + API + MCP server | Akýkoľvek container platform, autoscaled |
| Database | PostgreSQL | Managed Postgres (RDS / Flexible Server / váš vlastný) |
| Node fleet | Builds & spúšťa cTrader kontajnery | **VMs alebo Kubernetes — potrebuje privileged Docker** |

:::warning Jedna vec na scope up front
Node agenti stavajú a spúšťajú cTrader kontajnery, takže potrebujú **privileged Docker**. To vylučuje
serverless container runtimes (Azure Container Apps, AWS Fargate) *pre agents* — spustite na
[Kubernetes](./deployment/kubernetes.md), VM alebo EC2. Stateless tier bežíč kdekoľvek.
:::

Skutočný, copy-paste deployment guides to konkrétny: [cloud overview](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## Ako to monetizujete

- **Managed hosting subscription.** Mesiac Starter / Team / Business plány podľa node fleet a
  backtest concurrency.
- **Usage & compute metering.** Bill backtest-hours, live-node-hours a storage — prirodzene metered
  kontajnerom fleet, ktorý už spúšťate.
- **White-label reseller tiers.** Nábor viac pre full rebrand (logo, farby, PWA,
  `ShowSiteLink=false`) a za enabling premium capabilities cez
  [feature toggles](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **Managed AI.** Bundle default AI provider kľúč, takže každý zákazník dostáva AI bez setup a
  mark up the usage — alebo ponúknite bring-your-own-key. → [AI feature](./features/ai.md)
- **Prop-firm & copy-trading revenue share.** Hostovať firmy spúšťajúce challenges a performance fees a
  vezmite si platform cut. → [Prop-firm](./features/prop-firm.md) ·
  [Performance fees](./features/copy-performance-fees.md) ·
  [Provider marketplace](./features/copy-provider-marketplace.md)
- **Setup, onboarding & SLA.** Pripojte profesionálne služby a premium support.

## Multi-tenant patterns

- **Deployment-per-tenant (recommended).** Jedna branded inštancia per zákazník — strong isolation,
  per-tenant branding a database, distinct node join token per tenant. Branding je read z
  `IOptionsMonitor`, takže každá inštancia nese svoju vlastnú identitu.
  → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Node discovery](./operations/node-discovery.md)
- **Shared control plane (advanced).** Riadiť mnoho inštancií z vašej vlastnej provisioning layer, seeding
  branding a features per tenant programmatically.

## Metering usage pre billing

Owner/admin-only **`GET /api/usage`** endpoint vracia read-only sumár, ktorý provider môže poll a
bill on — bez akéhokoľvek nového domény alebo persistence, project existing state:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Poll per tenant deployment riadiť seat-based, fleet-based alebo workload-based pricing. Pair s
[logging & observability](./operations/logging.md) pre finer compute metering.

## Zachovávanie marží predictable

Scale nodes to demand, share Postgres tiers a autoscale stateless tier. Operačný
surfaces, ktoré potrebujete sú už tam:

- [Scaling & self-healing](./deployment/scaling.md)
- [Logging & observability](./operations/logging.md)
- [Backup & recovery](./operations/backup-recovery.md)

## Začať

1. Stand up reference deployment z [cloud guides](./deployment/cloud.md).
2. Template per tenant (branding + join token + DB) a wire váš billing na compute usage.
3. List — teraz máte managed algo-trading platformu na predaj.

## Prispejte späť

Providers spúšťajúce cMind at scale hit sharp edges prvý. Upstreaming vašich operačných opravy a
IaC improvements udržuje váš fleet lacný na údržbu — začnite s
[Contributing guide](./contributing.md).
