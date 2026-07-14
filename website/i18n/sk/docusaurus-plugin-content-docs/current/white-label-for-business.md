---
slug: /white-label-for-business
title: White-label pre obchod
description: Dodajte cMind ako váš vlastný branded produkt — pre prop firmy, brokerov a copy-trading businesses. Rebrand každý povrch cez config, bez zmien kódu.
sidebar_position: 4
---

# White-label cMind pre váš obchod 🏢

Prevádzkyujete prop firmu, broker desk alebo copy-trading službu? cMind bol postavený od prvého dňa aby bol
**resold ako váš vlastný produkt**. Každý povrch — meno, logo, favicon, farby, dokonca
installable phone app — sa ohýba vašej značke. Vaši zákazníci vidia *vašu* spoločnosť. Žiadne zmeny kódu,
žiadny fork, len config.

:::tip[TL;DR]
Nasmerujte `App:Branding` na vaše meno, farby a logo. Restart. Hotovo. Plný technický reference žije
v [White-label feature doc](./features/white-label.md).
:::

## Čo môžete rebrandovať

| Povrch | Čo sa zmení |
|---|---|
| **Product name** | App bar text + browser tab title |
| **Logo & favicon** | Vaše značky všade, vrátane browser tab |
| **Colors** | Plná paleta — primary, surfaces, status colors — tečie cez celý UI *a* app CSS cez design tokens |
| **Installable app (PWA)** | Add-to-home-screen meno, ikona a splash používajú vašu značku |
| **Meta / SEO** | Popis a support URL sú vaše |
| **Custom CSS** | Injektujte vašu vlastnú leštenie pre poslednú 5% |

Všetko defaults to stock cMind identitu, takže vy len overridujete to, čo vás zaujíma.

## 60-second rebrand

Nastavte tieto na vašej deployment (JSON config alebo environment variables):

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

Environment-variable forma: `App__Branding__ProductName=AcmeFX`. Farby sú validované pri startup —
bad hex value zlyháva boot s jasným posolstvom namiesto renderovat broken page. Pekne a
hlasno, presne keď to chcete.

## "Powered by cMind" link

**V defaults**, dashboard ukazuje malý, vkusný **"Powered by cMind"** link, ktorý
nasmeruje návštevníkov späť na túto stránku. Je to na defaults, pretože sme hrdí na projekt a
to pomáha ostatným obchodníkom ho nájsť — ale je to **vaša rozhodnutie**.

- **Nechajte to** (default): subtle credit link na dashboard. Stojí vás nič, pomáha projektu.
- **Skryť to**: nastavte `App__Branding__ShowSiteLink=false` a zmizne úplne — perfektné pre
  plne white-labeled deployment, kde produkt je nešetriacny *váš*.

Pozrite [White-label feature doc](./features/white-label.md#powered-by-link) pre presne kde to
renderuje.

## Multi-tenant, per-customer branding

Pretože branding je len deployment config, každá tenant deployment môže niesť svoju vlastnú identitu. Spustite
oddelený instancia per zákazník, alebo riaďte branding z vašej vlastnej control plane — aplikácia to číta z
`IOptionsMonitor`, takže môže dokonca rebuídať tému live, keď sa zmenia možnosti.

Pairujte to s:

- **[Feature toggles](./features/feature-toggles.md)** — rozhodnite, ktoré schopnosti každá tenant vidí.
- **[Prop-firm rules](./features/prop-firm.md)** — vynúťte vaše challenge pravidlá s live equity tracking.
- **[Performance fees](./features/copy-performance-fees.md)** + **[provider marketplace](./features/copy-provider-marketplace.md)** — monetizujte copy trading.
- **[Compliance](./features/compliance.md)** — udržujte audit trail, ktorý váš regulátor požiada.

## Assets & hosting

Padnite váš logo/favicon do Web app `wwwroot/branding/` (alebo nasmerujte `LogoUrl`/`FaviconUrl`
na akýkoľvek absolute URL). Deploy ako vám vyhovuje — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) alebo
[AWS](./deployment/cloud-aws.md).

Pripravení to urobiť svojím? Začnite s [technickým white-label reference →](./features/white-label.md)
