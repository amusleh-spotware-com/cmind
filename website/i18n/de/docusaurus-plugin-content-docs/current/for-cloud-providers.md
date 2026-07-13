---
slug: /for-cloud-providers
title: cMind für Cloud- und VPS-Anbieter
description: Warum ein Cloud- oder VPS-Anbieter verwaltetes cMind-Hosting anbieten sollte – ein fertiges, differenziertes Produkt für Algo-Trader, Broker und Prop Firms, mit klaren Wegen zur Monetarisierung von Rechenleistung, White-Label-Reselling und verwalteter KI.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind für Cloud- und VPS-Anbieter 🖥️

Du vermietest bereits Rechenleistung. cMind ist ein fertiges, Open-Source-Produkt, das du um diese Rechenleistung wickeln kannst: **biete verwaltetes cMind-Hosting an** und lande eine hochwertige, klebrige, rechenleistungshungrige Workload – Algo-Trader, Broker, Prop Firms und Trading-Communities, die die Plattform laufen lassen möchten, ohne selbst das Ops-Team zu werden.

:::tip TL;DR
Führe die zustandslose Ebene + Postgres + eine Node-Flotte aus; hand Kunden eine gebrandete URL. Monetarisiere das Abonnement, die Rechenleistung, das White-Label und die KI. → [Bereitstellung in die Cloud](./deployment/cloud.md)
:::

## Warum verwaltetes cMind anbieten

- **Keine Build-Kosten.** Es ist Open Source, MIT-lizenziert und bereits dokumentiert, getestet und containerisiert. Du packst und bedienst es – du baust es nicht.
- **Ein differenziertes Produkt für eine lukrative Nische.** Algo-Trading ist rechenleistungshungrig: Backtests und Live-Nodes verbrauchen CPU, was *nutzbare Rechenleistung* ist, die du bereits verkaufst.
- **Klebrige Kunden.** Trader, die Strategien in der Plattform bauen und ausführen, sind nicht beiläufig wandernd.
- **Verwandelt einen Vorbehalt in einen Upsell.** cMind ist von Natur aus selbstgehostet – für Kunden, die "nicht das Ops-Team sein möchten," *du* bist die Antwort.

## Wer verwaltetes cMind von dir kauft

- **Einzelne Quants & Trader** die es gehostet möchten. → [Für Trader](./for-traders.md)
- **cTrader-Broker** die ein White-Label für ihre Kunden ausführen. → [Für Broker](./for-brokers.md)
- **Prop Firms & Copy-Trading-Unternehmen** die gebrandete, prüfbare Infrastruktur brauchen.

## Was "verwaltetes cMind" bedeutet, zu betreiben

Du bedienst drei Ebenen; der Kunde bekommt eine gebrandete Web-URL:

| Ebene | Was es ist | Wo es läuft |
|---|---|---|
| Zustandslos (Web + MCP) | Die App + API + MCP-Server | Jede Container-Plattform, autoskaliert |
| Datenbank | PostgreSQL | Verwaltetes Postgres (RDS / Flexible Server / Dein eigenes) |
| Node-Flotte | Baut und führt cTrader-Container aus | **VMs oder Kubernetes – benötigt privilegiertes Docker** |

:::warning Eine Sache, um vorab zu planen
Node-Agents bauen und führen cTrader-Container aus, daher benötigen sie **privilegiertes Docker**. Das schließt serverlose Container-Runtimes aus (Azure Container Apps, AWS Fargate) *für die Agents* – führe diese auf [Kubernetes](./deployment/kubernetes.md), einer VM oder EC2 aus. Die zustandslose Ebene läuft überall.
:::

Echte, Copy-Paste-Bereitstellungsanleitungen machen dies konkret: [Cloud-Übersicht](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Skalierung](./deployment/scaling.md).

## Wie du es monetarisierst

- **Verwaltetes Hosting-Abonnement.** Monatliche Starter / Team / Business Pläne, dimensioniert nach Node-Flotte und Backtest-Parallelität.
- **Nutzungs- und Rechenleistungs-Metering.** Berechne Backtest-Stunden, Live-Node-Stunden und Speicher – natürlich gemessen durch die Container-Flotte, die du bereits läufst.
- **White-Label-Reseller-Tiers.** Berechne mehr für ein volles Rebrand (Logo, Farben, PWA, `ShowSiteLink=false`) und für das Aktivieren von Premium-Funktionen über [Feature-Toggles](./features/feature-toggles.md). → [White-Label](./features/white-label.md)
- **Verwaltete KI.** Bündel einen Standard-KI-Provider-Schlüssel, damit jeder Kundenbenutzer KI ohne Setup bekommt, und makiere die Nutzung – oder biete Bring-Your-Own-Key an. → [KI-Feature](./features/ai.md)
- **Prop-Firm & Copy-Trading-Umsatzbeteiligung.** Host-Firmen, die Challenges und Performance Fees laufen lassen und nehmen einen Plattform-Cut. → [Prop-Firm](./features/prop-firm.md) · [Performance Fees](./features/copy-performance-fees.md) · [Provider Marketplace](./features/copy-provider-marketplace.md)
- **Setup, Onboarding & SLA.** Befestige professionelle Services und Premium-Support.

## Multi-Tenant-Muster

- **Bereitstellung-pro-Tenant (empfohlen).** Eine gebrandete Instanz pro Kunde – starke Isolation, Pro-Tenant-Branding und Datenbank, ein unterschiedliches Node-Join-Token pro Tenant. Branding wird aus `IOptionsMonitor` gelesen, daher trägt jede Instanz ihre eigene Identität. → [Multi-Tenant Branding](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Node-Erkennung](./operations/node-discovery.md)
- **Gemeinsame Control Plane (fortgeschritten).** Fahre viele Instanzen von deiner eigenen Provisioning-Schicht aus, Seeding Branding und Features pro Tenant programmgesteuert.

## Metering-Nutzung für Abrechnung

Ein Besitzer-/Admin-nur **`GET /api/usage`** Endpunkt gibt eine schreibgeschützte Zusammenfassung zurück, die ein Anbieter ablesen und berechnen kann – ohne irgendwelche neuen Domain oder Persistierung, projiziert bestehenden State:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Lese es pro Tenant-Bereitstellung ab, um Sitz-basierte, Flotten-basierte oder Workload-basierte Preisgestaltung zu fahren. Koppelt mit [Logging & Observability](./operations/logging.md) für feinere Rechenleistungs-Metering.

## Margen vorhersehbar halten

Skaliere Nodes zu Nachfrage, teile Postgres-Tiers und autoskaliere die zustandslose Ebene. Die operativen Oberflächen, die du brauchst, sind bereits da:

- [Skalierung & Self-Healing](./deployment/scaling.md)
- [Logging & Observability](./operations/logging.md)
- [Sicherung & Wiederherstellung](./operations/backup-recovery.md)

## Anfangen

1. Stelle eine Referenzbenutzung aus den [Cloud-Guides](./deployment/cloud.md) auf.
2. Template-it pro Tenant (Branding + Join-Token + DB) und drahte deine Abrechnung zur Rechenleistungs-Nutzung.
3. Liste es – du hast jetzt eine verwaltete Algo-Trading-Plattform zu verkaufen.

## Trag bei

Anbieter, die cMind im großen Maßstab laufen, treffen die scharfen Kanten zuerst. Upstream-Betrieb deine operativen Fixes und IaC-Verbesserungen halten deine Flotte billig zu unterhalten – beginne mit dem [Beitragsleitfaden](./contributing.md).
