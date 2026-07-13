---
slug: /for-brokers
title: cMind pro cTrader brokery
description: Proč by měl cTrader broker spustit white-label cMind pro své vlastní klienty — dejte traderkům AI, copy trading a prop-firm výzvy pod vaší značkou, omezit účty na vaši brokerage, a vyhrát výhodu nad konkurenty.
keywords:
  - cTrader broker
  - white-label trading platform
  - broker technology
  - copy trading for brokers
  - AI trading tools
  - prop firm software
sidebar_position: 6
---

# cMind pro cTrader brokery

Provozujete cTrader brokerství. Vaši klienti už mohou obchodovat — ale tak mohou klienti každého jiného brokera. **cMind vám umožňuje dát svým traderkům plnou AI-powered trading operace platformu, značenou jako vaši**, takže oni staví, testují, spouští, kopírují a monitorují strategie uvnitř *vaší* ekosystému místo aby se driftu do nástrojů třetích stran. To je lepší držení klientů, více objemů, a skutečná výhoda nad brokery nabízející nic než terminál.

:::tip TL;DR
Spusťte white-label cMind pro své klienty. Omezit účty na **vaši** brokerage, přepnout AI a copy trading, a lodní jej pod vaší značkou. → [White-label pro firmy](./white-label-for-business.md)
:::

## Výhoda kterou dostanete nad ostatními brokery

- **Diferencujte na nástrojích, ne jen spread.** Dejte klientům AI cBot generování, backtesting na spravovaném clusteru, copy trading, a prop-firm výzvy — schopnosti, které si většina brokerů jednoduše neponechává.
- **Udržujte klienty v přátelské ekosystému.** Když tradeři staví a spouští své strategie uvnitř vaší značené platformy, zůstávají. Retence je celá hra.
- **Pod vaší značkou, na vaší doméně.** Jméno, logo, barvy, favicon, i instalovatelná telefonní aplikace — vše vaše. Nikdo nevidí "cMind." → [White-label vlastnost](./features/white-label.md)

## Obsluha pouze vašich účtů (broker allowlist)

Spuštění white-label pro *vaše* klienty? Omezit kterých brokerů trading účty, kterou uživatelé mohou přidat aby vaše nasazení pouze kdy-koli sloužilo vaší knize:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Vaše jméno brokerage"]
    }
  }
}
```

Když je allowlist nastaveno, cMind kontroluje každý účet, který se uživatel pokusí přidat — jak přes cTrader Open API, tak přes manuální cID login (ověřeno čtením skutečného jména brokera účtu) — a odmítá jakýkoliv účet, který není na vašem seznamu. Nechaj to prázdné a každý broker je povolen (výchozí). Viz [White-label vlastnost doc](./features/white-label.md#broker-allowlist) pro plné mechanics.

## Loď jeden Open API app pro všechny vaše uživatele

Přeskočit per-user háček: poskytnout **jednu cTrader Open API aplikaci** a každý klient schvaluje své účty přes něj — žádný klient nikdy nezaregistruje svůj vlastní. Zaregistrujte jedinou redirect URL, vložte pověření v config nebo owner nastavení, a shared-mode zapne se pro všechny. Vyjednaný vyšší limit cTrader zpráv? Vyladit **per-message-type client rate limity** (nebo vypnout pacing). → [Sdílené Open API aplikace & rate limity](./features/open-api-shared-app.md)

## Nové způsoby jak si vydělat

- **AI, bez tření pro klienty.** Poskytnout výchozí AI klíč poskytovatele na úrovni nasazení a každý klient dostane AI vlastnosti okamžitě — bez přihlášení jinde. Označit jej, nebo jej zabalit do prémiových úrovní. Klienti mohou stále přinést svůj vlastní klíč. → [AI vlastnost](./features/ai.md)
- **Prop-firm výzvy.** Spusťte financované-trader výzvy s live equity tracking a vynucenými pravidly, a účtujte za vstupy. → [Prop-firm pravidla](./features/prop-firm.md)
- **Copy-trading obchod.** Výkonnostní poplatky a marketplace poskytovatele změní copy trading na tržbu. → [Výkonnostní poplatky](./features/copy-performance-fees.md) · [Provider marketplace](./features/copy-provider-marketplace.md)
- **Feature tiers.** Rozhodněte které schopnosti každý klientský segment vidí s [feature toggles](./features/feature-toggles.md).

## Regulované, auditovatelné, multi-tenant

- **[Compliance](./features/compliance.md)** logy vám dají audit trail kterých bude váš regulátor žádat.
- **[Two-factor auth](./features/two-factor-auth.md)** lze učinit povinným per nasazení.
- **Per-client branding** — spustěte oddělenou značenou instanci na segment, řízenu z vaší vlastní control plane. → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Jak začít

1. Přečtěte si [White-label pro firmy](./white-label-for-business.md) pro 60-sekundový rebrand.
2. Nastavit `App:Accounts:AllowedBrokers` na vaši brokerage a vybrat váš [feature set](./features/feature-toggles.md).
3. [Nasaďte](./deployment/cloud.md) jej — Docker, Kubernetes, Azure, nebo AWS.

Nechcete sami spouštět infrastrukturu? Hosting poskytovatel může spustit spravovaného cMind pro vás — ukázat jim [Pro cloud & VPS poskytovatele](./for-cloud-providers.md).

## Utvářejte plán

cMind je open source. Brokery, kteří na něm staví, získávají přehmotný hlas v tom, kam jde — požádejte o integrace a kontroly, které potřebujete, a přispějte je zpět přes [Contributing guide](./contributing.md).
