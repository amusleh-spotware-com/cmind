---
slug: /for-brokers
title: cMind pre cTrader brokerov
description: Prečo by mal cTrader broker spustiť white-label cMind pre svojich vlastných klientov — dajte obchodníkom AI, copy trading a prop-firm challenges pod vašou značkou, obmedzte účty na vašu brokeráž a vyhrať výhodu nad konkurenciou.
keywords:
  - cTrader broker
  - white-label trading platform
  - broker technology
  - copy trading for brokers
  - AI trading tools
  - prop firm software
sidebar_position: 6
---

# cMind pre cTrader brokerov 🏦

Prevádzkyujete cTrader brokeráž. Vaši klienti už môžu obchodovať — ale tak to môžu aj každého iného brokerovi
klienti. **cMind vám umožňuje dať vašim obchodníkom úplnú AI-powered trading operations platformu, branded ako
vašu vlastnú**, takže stavajú, backtestujú, spúšťajú, kopírujú a monitorujú stratégie vnútri *vašej* ekosystému
namiesto driftu na tretiu stranu nástroj. To je lepší retenčný klienti, viac objemu a skutočná výhoda nad
brokermi, ktorí ponúkajú len terminál.

:::tip[TL;DR]
Spustite white-label cMind pre vašich klientov. Obmedzte účty na **vašu** brokeráž, zapnite AI a
copy trading a dodajte to pod vašou značkou. → [White-label pre obchod](./white-label-for-business.md)
:::

## Výhoda, ktorú získate nad ostatnými brokermi

- **Rozlišovať na tooling, nie len spreads.** Dajte klientom AI cBot generáciu, backtesting na
  managed cluster, copy trading a prop-firm challenges — schopnosti, ktoré väčšina brokerov jednoducho nepongajú.
- **Keep clients v vašom ekosystému.** Keď obchodníci stavajú a spúšťajú svoje stratégie vnútri vašej branded
  platformy, zostanú. Retention je celá hra.
- **Pod vašou značkou, na vašej doméne.** Meno, logo, farby, favicon, dokonca aj installable phone app —
  všetko vaše. Nikto nevidí "cMind." → [White-label feature](./features/white-label.md)

## Servírujte iba vaše účty (broker allowlist)

Spúšťate white-label pre *vašich* klientov? Obmedzte, ktorých brokerov obchodné účty používatelia môžu pridať, takže
vaša deployment slúži iba vašej knihe:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

Keď je allowlist nastavený, cMind kontroluje každý účet, ktorý sa používateľ pokúsi pridať — cez cTrader Open
API a cez manual cID login (overené čítaním real broker name účtu) — a odmietne akýkoľvek
účet, ktorý nie je na vašom zozname. Nechajte to prázdne a každý broker je povolený (default). Pozrite
[White-label feature doc](./features/white-label.md#broker-allowlist) pre plnú mechaniku.

## Dodaj jednu Open API app pre všetkých vašich používateľov

Preskočte per-user hassle: poskytujte **jednu cTrader Open API aplikáciu** a každý klient autorizuje
svoje účty cez to — žiadny klient nikdy registruje svoje vlastné. Zaregistrujte jednu redirect URL, pudajte
poverenia v config alebo owner settings a shared-mode sa zapne pre všetkých. Dojednali ste vyšší cTrader
message limit? Tune **per-message-type client rate limits** (alebo disable pacing). → [Shared Open API application & rate limits](./features/open-api-shared-app.md)

## Nové spôsoby monetizácie

- **AI, s nulovou trením pre klientov.** Poskytujte default AI provider kľúč na level nasadenia a
  každý klient dostáva AI features okamžite — bez registrácie inde. Marca to hore, alebo bundlujte do premium
  tiers. Klienti stále môžu priniesť svoj vlastný kľúč. → [AI feature](./features/ai.md)
- **Prop-firm challenges.** Spustite funded-trader challenges s live equity tracking a enforced rules
  a napopaťte za položky. → [Prop-firm rules](./features/prop-firm.md)
- **Copy-trading business.** Performance fees a provider marketplace zmenia copy trading na
  príjmy. → [Performance fees](./features/copy-performance-fees.md) ·
  [Provider marketplace](./features/copy-provider-marketplace.md)
- **Feature tiers.** Rozhodnite, ktoré schopnosti každý segment klienta vidí s
  [feature toggles](./features/feature-toggles.md).

## Regulované, auditable, multi-tenant

- **[Compliance](./features/compliance.md)** logs vám dajú audit trail, ktorý váš regulátor požiada.
- **[Two-factor auth](./features/two-factor-auth.md)** môže byť povinný per deployment.
- **Per-client branding** — spustite odd branded inštanciu per segment, driven z vašej vlastnej control
  plane. → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Ako začať

1. Čítajte [White-label pre obchod](./white-label-for-business.md) pre 60-second rebrand.
2. Nastavte `App:Accounts:AllowedBrokers` na vašu brokeráž a vyberte si vašu [feature set](./features/feature-toggles.md).
3. [Deploy](./deployment/cloud.md) to — Docker, Kubernetes, Azure alebo AWS.

Nechcete spúšťať infraštruktúru sami? Hosting provider môže prevádzkovať managed cMind pre vás
— nasmerujte ich na [Pre cloud & VPS providers](./for-cloud-providers.md).

## Formovať roadmap

cMind je open source. Brokeri, ktorí na ňom stavajú, dostanú nadmernú voz v tom, kam ide — požiadajte
integrácie a ovládače, ktoré potrebujete, a prispejte ich späť cez
[Contributing guide](./contributing.md).
