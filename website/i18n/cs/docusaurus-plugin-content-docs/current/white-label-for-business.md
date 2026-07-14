---
slug: /white-label-for-business
title: White-label pro obchod
description: Lodní cMind jako svůj vlastní značkovaný produkt — pro prop firmy, brokery a copy-trading firmy. Přebrandujte každý povrch přes config, bez změn kódu.
sidebar_position: 4
---

# White-label cMind pro váš obchod

Spuštění prop firmy, broker desk, nebo copy-trading služby? cMind byl postaven od prvního dne jako **prodáno jako váš vlastní produkt**. Každý povrch — jméno, logo, favicon, barvy, i instalovatelná telefonní aplikace — ohýbá se na vaši značku. Vaši zákazníci vidí *vaši* společnost. Bez změn kódu, bez fork, jen config.

:::tip[TL;DR]
Bod `App:Branding` na vaše jméno, barvy a logo. Restart. Hotovo. Plná technická reference žije v [White-label feature doc](./features/white-label.md).
:::

## Co můžete přebrandovat

| Povrch | Co se změní |
|---|---|
| **Název produktu** | App bar text + browser tab titul |
| **Logo & favicon** | Vaše značky všude, včetně browser tab |
| **Barvy** | Plná paleta — primární, povrchy, status barvy — tečou celou UI *a* app vlastní CSS přes design tokeny |
| **Instalovatelná aplikace (PWA)** | Add-to-home-screen jméno, ikona a splash používají vaši značku |
| **Meta / SEO** | Popis a support URL jsou vaši |
| **Vlastní CSS** | Injekovat vaši vlastní leštění za posledních 5 % |

Vše výchozí na stock cMind identitu, takže pouze přepisujete co vám záleží.

## 60-sekundový rebrand

Nastavit to na vaše nasazení (JSON config nebo environment proměnné):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Environment-proměnný tvar: `App__Branding__ProductName=AcmeFX`. Barvy jsou ověřovány na startup — špatná hex hodnota selhá boot s jasným poselstvím místo renderování rozbitou stránku. Hezky a hlasitě, přesně když jej chcete.

## "Powered by cMind" odkaz

By **default**, dashboard ukazuje malý, vkusný **"Powered by cMind"** odkaz, který poukazuje návštěvníky zpět na tuto stránku. Je to výchozí, protože jsme hrdi na projekt a pomáhá to ostatním traderkům jej najít — ale je to **vaše volání**.

- **Udržovat to** (výchozí): subtilní credit odkaz na dashboard. Stojí vás nic, pomáhá projektu.
- **Skrýt to**: nastavit `App__Branding__ShowSiteLink=false` a zmizí zcela — dokonalé pro plně white-labeled nasazení kde je produkt jednoznačně *vůš*.

Viz [White-label feature doc](./features/white-label.md#powered-by-link) pro přesně kde rendery.

## Multi-tenant, per-customer branding

Protože branding je jen deployment config, každé tenant nasazení může nést svou vlastní identitu. Spustit oddělenou instanci per customer, nebo řídím branding z vaší vlastní control plane — aplikace to čte z `IOptionsMonitor`, takže to může i znovu staví téma live když se možnosti změní.

Pár to s:

- **[Feature toggles](./features/feature-toggles.md)** — rozhoduji které schopnosti každý tenant vidí.
- **[Prop-firm pravidla](./features/prop-firm.md)** — vynucovat vaše pravidla výzvy s live equity tracking.
- **[Performance poplatky](./features/copy-performance-fees.md)** + **[provider marketplace](./features/copy-provider-marketplace.md)** — peníze copy trading.
- **[Compliance](./features/compliance.md)** — udržovat audit trail vaši regulátor budou žádat.

## Assety & hosting

Dej vaše logo/favicon do Web app `wwwroot/branding/` (nebo odkaz `LogoUrl`/`FaviconUrl` na jakoukoliv absolutní URL). Nasaďte jak se vám líbí — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), nebo [AWS](./deployment/cloud-aws.md).

Připraveni učinit to vaším? Začít s [technickou white-label reference →](./features/white-label.md)
