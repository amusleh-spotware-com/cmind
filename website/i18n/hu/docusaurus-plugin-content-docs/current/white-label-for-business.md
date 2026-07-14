---
slug: /white-label-for-business
title: Fehér címke az üzlethez
description: Szállítsa a cMind-et a saját márkanem termékként — prop-cégeknek, bróker asztaloknak és másolási kereskedési vállalkozásoknak. Márkaváltsa minden felületet a config-on, kódmódosítás nélkül.
sidebar_position: 4
---

# Fehér címke cMind az Ön üzletéhez 🏢

Egy prop-céget, egy bróker asztalt vagy egy másolási kereskedési szolgáltatást futtat? A cMind az első naptól kezdve arra építése meg lett, hogy **a saját termékeként legyen újra értékesítve**. Minden felület — a név, a logó, a favicon, a színek, még az telepítendő telefonalkalmazás is — megkötött az Ön márkanevéhez. Az Ön ügyfelei az *Ön* cégét látják. Nincs kódmódosítás, nincs fork, csak config.

:::tip[TL;DR]
Mutasd az `App:Branding` értéket az Ön nevére, színeire és logójára. Indítsa újra. Kész. Teljes technikai referencia a [Fehér címke funkcióját](./features/white-label.md) tartalmazó dokumentumban él.
:::

## Mit márkaválthat

| Felület | Mi változik |
|---|---|
| **Terméknév** | Alkalmazás sáv szövege + böngészőfül címe |
| **Logó és favicon** | Az Ön jelölések mindenhol, beleértve a böngészőfülit |
| **Színek** | Teljes paletta — elsődleges, felületek, állapot-színek — áramlik az egész UI-n *és* az alkalmazás saját CSS-én a tervezési tokeneken keresztül |
| **Telepítendő alkalmazás (PWA)** | A hozzáadás-kezdőképernyő név, ikon és sikló az Ön márkanevet használja |
| **Meta / SEO** | A leírás és a támogatás URL az Ön tulajdona |
| **Egyéni CSS** | Injektálja a saját csiszolódást az utolsó 5%-hoz |

Mindez az alapértelmezett cMind-identitásra esik, így csak azt a felülírása, ami Önt aggódtat.

## A 60 másodperces átmárkaépítés

Állítsa be ezeket az Ön telepítésén (JSON config vagy környezeti változók):

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

Környezeti-változó forma: `App__Branding__ProductName=AcmeFX`. A színek az indításkor ellenőrzödnek — a rossz hex-érték nem jó csizma az egyértelmű üzenettel ahelyett, hogy egy törött oldalt renderne. Szép és hangos, pontosan akkor, amikor akarod.

## A "cMind-vel működik" hivatkozás

**Alapértelmezettként** az irányítópult egy kis, ízléses **"cMind-vel működik"** hivatkozást mutat, amely látogatókat erre az oldalra mutat. Alapértelmezettként van, mert büszke vagyunk a projektre, és segít más kereskedőknek megtalálni — de ez az **Ön döntése**.

- **Tartsd meg** (alapértelmezett): egy finom hitel-hivatkozás az irányítópulton. Semmi sem költségez, segít a projektnek.
- **Rejtse el**: állítsa a `App__Branding__ShowSiteLink=false` értéket, és teljesen eltűnik — tökéletes egy teljes fehér címkés telepítéshez, ahol a termék vitathatatlanul az *Ön* tulajdona.

Tekintse meg a [Fehér címke funkcióját](./features/white-label.md#powered-by-link) az pontosan azt, ahol megjelenik.

## Multi-bérlős, ügyfélenkénti márkaépítés

Mivel a márkaépítés csak telepítési config, az egyes bérlő-telepítések saját identitást vihetnek. Futtasson külön példányt ügyfélenkénti, vagy hajtsa meg a márkaépítést az Ön saját kontrolláló síkjáról — az alkalmazás az `IOptionsMonitor`-ból olvassa, így még akkor is élőben felépítheti a témát az opciók megváltozásakor.

Párosítsd ezt:

- **[Funkcióváltógombok](./features/feature-toggles.md)** — döntsd meg, mely képességeket látod az egyes bérlők.
- **[Prop-firm szabályok](./features/prop-firm.md)** — erőszakolj meg kihívás-szabályokat élő saját tőke-nyomon követéssel.
- **[Teljesítmény-díjak](./features/copy-performance-fees.md)** + **[szolgáltatói piac](./features/copy-provider-marketplace.md)** — pénzszerezd a másolási kereskedést.
- **[Compliance](./features/compliance.md)** — tartsd meg az auditcsapást, amelyet a szabályozó megkérdez.

## Eszközök és üzemeltetés

Dobja le a logót/favicont a Web-alkalmazás `wwwroot/branding/` könyvtárába (vagy mutassa az `LogoUrl`/`FaviconUrl` értékeket bármilyen abszolút URL-re). Telepítse ahogy meg van írva — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) vagy [AWS](./deployment/cloud-aws.md).

Kész, hogy magadévá tedd? Kezdd a [technikai fehér címke referenciájával →](./features/white-label.md)
