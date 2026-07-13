---
slug: /for-cloud-providers
title: cMind per provider di cloud e VPS
description: Perché un provider di cloud o VPS dovrebbe offrire hosting cMind gestito — un prodotto pronto all'uso e differenziato per trader algoritmici, broker e prop firm, con modi chiari per monetizzare il calcolo, il white-label reselling e l'AI gestito.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind per provider di cloud e VPS

Affitti già il calcolo. cMind è un prodotto open-source pronto all'uso che puoi avvolgere attorno a quel calcolo: **offri hosting cMind gestito** e atterra un carico di lavoro di alto valore, appiccicaticcio e affamato di calcolo — trader algoritmici, broker, prop firm e comunità di trading che vogliono la piattaforma in esecuzione senza diventare loro il team ops.

:::tip TL;DR
Esegui il tier stateless + Postgres + una flotta di nodi; dai ai clienti un URL marchiato. Monetizza l'abbonamento, il calcolo, il white-label e l'AI. → [Distribuisci al cloud](./deployment/cloud.md)
:::

## Perché offrire cMind gestito

- **Nessun costo di build.** È open source, con licenza MIT ed è già documentato, testato e containerizzato. Tu lo configuri e lo gestisci — non lo costruisci.
- **Un prodotto differenziato per una nicchia redditizia.** Il trading algoritmico richiede calcolo: backtest e nodi live bruciano CPU, che è *utilizzo fatturabile* che già vendi.
- **Clienti appiccicaticci.** I trader che costruiscono e eseguono strategie all'interno della piattaforma non si logorano casualmente.
- **Trasforma un cavillo in un upsell.** cMind è auto-hosted per design — per i clienti che "non vogliono essere il team ops", *tu* sei la risposta.

## Chi compra cMind gestito da te

- **Quant e trader individuali** che lo vogliono ospitato. → [Per trader](./for-traders.md)
- **Broker cTrader** che gestiscono un white-label per i loro clienti. → [Per broker](./for-brokers.md)
- **Prop firm e aziende di copy-trading** che hanno bisogno di infrastrutture marchio e verificabili.

## Cosa significa "cMind gestito" da gestire

Gestisci tre tier; il cliente ottiene un URL web marchiato:

| Tier | Che cos'è | Dove viene eseguito |
|---|---|---|
| Stateless (Web + MCP) | L'app + API + server MCP | Qualsiasi piattaforma di container, scalabile automaticamente |
| Database | PostgreSQL | Postgres gestito (RDS / Flexible Server / tuoi) |
| Flotta di nodi | Builds & esegue contenitori cTrader | **VM o Kubernetes — necessita di Docker privilegiato** |

:::warning Una cosa da scopo in anticipo
Gli agenti del nodo compilano ed eseguono contenitori cTrader, quindi hanno bisogno di **Docker privilegiato**. Ciò esclude i runtime di container serverless (Azure Container Apps, AWS Fargate) *per gli agenti* — eseguili su [Kubernetes](./deployment/kubernetes.md), una VM o EC2. Il tier stateless viene eseguito ovunque.
:::

Guide di distribuzione reali e copy-paste rendono tutto concreto: [panoramica cloud](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## Come lo monetizzi

- **Abbonamento hosting gestito.** Piani mensili Starter / Team / Business dimensionati per flotta di nodi e concorrenza di backtest.
- **Metriche di utilizzo e calcolo.** Fattura ore di backtest, ore di nodo live e archiviazione — naturalmente misurate dalla flotta di container che già esegui.
- **Tier di white-label reseller.** Addebita di più per un rebranding completo (logo, colori, PWA, `ShowSiteLink=false`) e per l'abilitazione di capacità premium tramite [feature toggle](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **AI gestita.** Includi una chiave del provider AI predefinita in modo che gli utenti di ogni cliente ottengano AI senza configurazione e marca il costo — o offri bring-your-own-key. → [Funzione AI](./features/ai.md)
- **Prop-firm e revenue share di copy-trading.** Ospita aziende che eseguono sfide e commissioni sulle prestazioni e preleva un taglio della piattaforma. → [Prop-firm](./features/prop-firm.md) · [Commissioni sulle prestazioni](./features/copy-performance-fees.md) · [Marketplace provider](./features/copy-provider-marketplace.md)
- **Setup, onboarding e SLA.** Allega servizi professionali e supporto premium.

## Pattern multi-tenant

- **Distribuzione per tenant (consigliato).** Un'istanza marchiata per cliente — isolamento forte, branding e database per tenant, un token di join di nodo distinto per tenant. Il branding viene letto da `IOptionsMonitor`, quindi ogni istanza porta la propria identità.
  → [Branding multi-tenant](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Scoperta di nodi](./operations/node-discovery.md)
- **Piano di controllo condiviso (avanzato).** Guida molte istanze dal tuo strato di provisioning, seminando il branding e le funzioni per tenant in modo programmatico.

## Metriche di utilizzo per la fatturazione

Un endpoint **`GET /api/usage`** solo proprietario/admin restituisce un riepilogo di sola lettura che un provider può interrogare e fatturare — senza alcun nuovo dominio o persistenza, proietta lo stato esistente:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Interrogalo per distribuzione per tenant per guidare i prezzi basati su posti, basati su flotta o basati su workload. Abbina a [logging e osservabilità](./operations/logging.md) per metriche di calcolo più fini.

## Mantenere i margini prevedibili

Scala i nodi alla domanda, condividi i tier Postgres e scalin automaticamente il tier stateless. Le superfici operative di cui hai bisogno sono già lì:

- [Scaling e auto-guarigione](./deployment/scaling.md)
- [Logging e osservabilità](./operations/logging.md)
- [Backup e ripristino](./operations/backup-recovery.md)

## Inizia

1. Esegui una distribuzione di riferimento dalle [guide cloud](./deployment/cloud.md).
2. Modellalo per tenant (branding + token di join + DB) e collega la fatturazione all'utilizzo del calcolo.
3. Elencalo — hai ora una piattaforma di trading algoritmico gestita da vendere.

## Contribuisci di ritorno

I provider che eseguono cMind su larga scala colpiscono per primi i bordi affilati. L'upstreaming delle tue correzioni operative e dei miglioramenti IaC mantiene la tua flotta economica da mantenere — inizia con la [guida al contribuire](./contributing.md).
