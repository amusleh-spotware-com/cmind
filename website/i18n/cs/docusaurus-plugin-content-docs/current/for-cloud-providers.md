---
slug: /for-cloud-providers
title: cMind pro cloud & VPS poskytovatele
description: Proč by měl cloud nebo VPS poskytovatel nabídnout spravované hostování cMind — hotový, diferencovaný produkt pro algo tradery, brokery a prop firmy, s jasnými způsoby jak si vydělat.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind pro cloud & VPS poskytovatele

Už pronajímáte compute. cMind je hotový, open-source produkt, který můžete zabalit compute kolem: **nabídněte spravované cMind hostování** a přistání high-value, sticky, compute-hungry workloadu — algoritmické tradery, brokery, prop firmy, a trading komunity, které chtějí platformu běžící bez stávání se ops týmem.

:::tip TL;DR
Spustěte stateless vrstvu + Postgres + fleet uzlu; ručku klientům značku URL. Peníze na subscription, compute, white-label, a AI. → [Nasaďte do cloudu](./deployment/cloud.md)
:::

## Proč nabídnout spravované cMind

- **Žádné stavební náklady.** Je to open source, MIT-licencované, a již dokumentované, testované a containerizované. Balíte a operujete — nestavoujete jej.
- **Diferencovaný produkt pro lukrativní niku.** Algo trading je compute-hungry: backtesty a live uzly spalují CPU, což je *billable usage*, který už prodáváte.
- **Sticky klienti.** Tradeři, kteří staví a spouští strategie uvnitř platformy, nechurn neformálně.
- **Změní caveat na upsell.** cMind je self-hosted podle návrhu — pro klienty, kteří "nechcete být ops týmem," *vy* jste odpověď.

## Kdo si koupí spravované cMind od vás

- **Jednotliví kvantovci a tradeři**, kterí jej chtějí hostován. → [Pro tradery](./for-traders.md)
- **cTrader brokery** spuštění white-label pro své klienty. → [Pro brokery](./for-brokers.md)
- **Prop firmy & copy-trading firmy**, které potřebují značenou, auditovatelnou infrastrukturu.

## Co "spravované cMind" znamená spustit

Provozujete tři vrstvy; klient dostane značku web URL:

| Vrstva | Co to je | Kde běží |
|---|---|---|
| Stateless (Web + MCP) | Aplikace + API + MCP server | Jakákoliv container platforma, autoscaled |
| Databáze | PostgreSQL | Spravované Postgres (RDS / Flexible Server / vaše vlastní) |
| Fleet uzlu | Builds & spouští cTrader kontejnery | **VMs nebo Kubernetes — potřebuje privilegovaný Docker** |

:::warning Jedna věc na scope up front
Agenti uzlu staví a spouští cTrader kontejnery, takže potřebují **privilegovaný Docker**. To vylučuje serverless container runtimes (Azure Container Apps, AWS Fargate) *pro agenty* — spusťte ty na [Kubernetes](./deployment/kubernetes.md), VM, nebo EC2. Stateless vrstva běží kdekoli.
:::

Reální, copy-paste deployment guides dělat toto konkrétní: [cloud přehled](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## Jak jste jej peníze

- **Spravované hostování subscription.** Měsíčně Starter / Team / Business plány dělá podle fleet uzlu a backtest concurrency.
- **Použití & compute metering.** Účet backtest-hours, live-node-hours, a úložiště — přirozeně měřeno kontejnerem fleet, který už spouštíte.
- **White-label reseller tiers.** Účet více pro úplný rebrand (logo, barvy, PWA, `ShowSiteLink=false`) a pro povolení premium schopnosti přes [feature toggles](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **Spravované AI.** Balíček výchozího AI klíče poskytovatele tak aby každý klient's uživatelé dostali AI bez nastavení, a označení nahoru použití — nebo nabídnout bring-your-own-key. → [AI vlastnost](./features/ai.md)
- **Prop-firm & copy-trading revenue share.** Hostují firmy spuštění výzvy a výkonnostní poplatky a berou platformu cut. → [Prop-firm](./features/prop-firm.md) · [Výkonnostní poplatky](./features/copy-performance-fees.md) · [Provider marketplace](./features/copy-provider-marketplace.md)
- **Setup, onboarding & SLA.** Připojit profesionální služby a prémiovou podporu.

## Multi-tenant patterns

- **Deployment-per-tenant (doporučeno).** Jedna značená instance na klienta — silná izolace, per-tenant branding a databáze, odlišný node join token per tenant. Branding se čte z `IOptionsMonitor`, takže každá instance nese svou vlastní identitu.
  → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Node discovery](./operations/node-discovery.md)
- **Sdílená control plane (pokročilá).** Řídí mnoho instancí z vaší vlastní provisioning vrstvy, seeding branding a vlastnosti per tenant programově.

## Metering usage pro billing

Owner/admin-only **`GET /api/usage`** endpoint vrátí read-only shrnutí, které poskytovatel může hlasovat a účet — bez jakékoliv nové domény nebo perzistence, projektuje existující stav:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Poll to per tenant nasazení k řídícím seat-based, fleet-based, nebo workload-based pricing. Pár s [logging & observability](./operations/logging.md) pro jemnější compute metering.

## Udržování marží předvídatelné

Scale uzly na poptávku, sdílej Postgres tiers, a autoscale stateless vrstu. Operační povrchy, které potřebujete, již tam jsou:

- [Scaling & self-healing](./deployment/scaling.md)
- [Logging & observability](./operations/logging.md)
- [Backup & recovery](./operations/backup-recovery.md)

## Začít

1. Stoj si reference nasazení z [cloud guides](./deployment/cloud.md).
2. Šablona to per tenant (branding + join token + DB) a drát váš billing na compute usage.
3. Seznamit jej — máte nyní spravované algo-trading platformu k prodeji.

## Přispívat zpět

Poskytovatelé spuštění cMind v měřítku zasáhli ostré hrany nejdřív. Upstreaming vaší operační opravy a IaC zlepšení udržuje váš fleet levný na údržbu — začít s [Contributing guide](./contributing.md).
