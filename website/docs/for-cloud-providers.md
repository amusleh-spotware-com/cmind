---
slug: /for-cloud-providers
title: cMind for cloud & VPS providers
description: Why a cloud or VPS provider should offer managed cMind hosting — a ready-made, differentiated product for algo traders, brokers and prop firms, with clear ways to monetize compute, white-label reselling and managed AI.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind for cloud & VPS providers 🖥️

You already rent compute. cMind is a ready-made, open-source product you can wrap that compute
around: **offer managed cMind hosting** and land a high-value, sticky, compute-hungry workload —
algorithmic traders, brokers, prop firms, and trading communities who want the platform running
without becoming the ops team themselves.

:::tip[TL;DR]
Run the stateless tier + Postgres + a node fleet; hand customers a branded URL. Monetize the
subscription, the compute, the white-label, and the AI. → [Deploy to the cloud](./deployment/cloud.md)
:::

## Why offer managed cMind

- **No build cost.** It's open source, MIT-licensed, and already documented, tested, and containerized.
  You package and operate it — you don't build it.
- **A differentiated product for a lucrative niche.** Algo trading is compute-hungry: backtests and
  live nodes burn CPU, which is *billable usage* you already sell.
- **Sticky customers.** Traders who build and run strategies inside the platform don't churn casually.
- **Turns a caveat into an upsell.** cMind is self-hosted by design — for customers who "don't want to
  be the ops team," *you* are the answer.

## Who buys managed cMind from you

- **Individual quants & traders** who want it hosted. → [For traders](./for-traders.md)
- **cTrader brokers** running a white-label for their clients. → [For brokers](./for-brokers.md)
- **Prop firms & copy-trading businesses** who need branded, auditable infrastructure.

## What "managed cMind" means to run

You operate three tiers; the customer gets a branded web URL:

| Tier | What it is | Where it runs |
|---|---|---|
| Stateless (Web + MCP) | The app + API + MCP server | Any container platform, autoscaled |
| Database | PostgreSQL | Managed Postgres (RDS / Flexible Server / your own) |
| Node fleet | Builds & runs cTrader containers | **VMs or Kubernetes — needs privileged Docker** |

:::warning[One thing to scope up front]
Node agents build and run cTrader containers, so they need **privileged Docker**. That rules out
serverless container runtimes (Azure Container Apps, AWS Fargate) *for the agents* — run those on
[Kubernetes](./deployment/kubernetes.md), a VM, or EC2. The stateless tier runs anywhere.
:::

Real, copy-paste deployment guides make this concrete: [cloud overview](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## How you monetize it

- **Managed hosting subscription.** Monthly Starter / Team / Business plans sized by node fleet and
  backtest concurrency.
- **Usage & compute metering.** Bill backtest-hours, live-node-hours, and storage — naturally metered
  by the container fleet you already run.
- **White-label reseller tiers.** Charge more for a full rebrand (logo, colors, PWA,
  `ShowSiteLink=false`) and for enabling premium capabilities via
  [feature toggles](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **Managed AI.** Bundle a default AI provider key so every customer's users get AI with no setup, and
  mark up the usage — or offer bring-your-own-key. → [AI feature](./features/ai.md)
- **Prop-firm & copy-trading revenue share.** Host firms running challenges and performance fees and
  take a platform cut. → [Prop-firm](./features/prop-firm.md) ·
  [Performance fees](./features/copy-performance-fees.md) ·
  [Provider marketplace](./features/copy-provider-marketplace.md)
- **Setup, onboarding & SLA.** Attach professional services and premium support.

## Multi-tenant patterns

- **Deployment-per-tenant (recommended).** One branded instance per customer — strong isolation,
  per-tenant branding and database, a distinct node join token per tenant. Branding is read from
  `IOptionsMonitor`, so each instance carries its own identity.
  → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Node discovery](./operations/node-discovery.md)
- **Shared control plane (advanced).** Drive many instances from your own provisioning layer, seeding
  branding and features per tenant programmatically.

## Metering usage for billing

An owner/admin-only **`GET /api/usage`** endpoint returns a read-only summary a provider can poll and
bill on — without any new domain or persistence, it projects existing state:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Poll it per tenant deployment to drive seat-based, fleet-based, or workload-based pricing. Pair with
[logging & observability](./operations/logging.md) for finer compute metering.

## Keeping margins predictable

Scale nodes to demand, share Postgres tiers, and autoscale the stateless tier. The operational
surfaces you need are already there:

- [Scaling & self-healing](./deployment/scaling.md)
- [Logging & observability](./operations/logging.md)
- [Backup & recovery](./operations/backup-recovery.md)

## Get started

1. Stand up a reference deployment from the [cloud guides](./deployment/cloud.md).
2. Template it per tenant (branding + join token + DB) and wire your billing to compute usage.
3. List it — you now have a managed algo-trading platform to sell.

## Contribute back

Providers running cMind at scale hit the sharp edges first. Upstreaming your operational fixes and
IaC improvements keeps your fleet cheap to maintain — start with the
[Contributing guide](./contributing.md).
