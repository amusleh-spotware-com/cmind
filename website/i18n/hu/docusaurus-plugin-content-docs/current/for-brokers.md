---
slug: /for-brokers
title: cMind cTrader-brókereknek
description: Miért cTrader-brókernek futtassa a saját klienseihez szánt fehér címkés cMind-et — adjon kereskedőknek AI-t, másolási kereskedést és prop-firm-kihívásokat a Ön márkaneve alatt, korlátozza az számlákat a brókerségéhez, és szerezzen előnyt a versenytársakkal szemben.
keywords:
  - cTrader bróker
  - fehér címkés kereskedelmi platform
  - bróker technológia
  - másolási kereskedés brókereknek
  - AI kereskedelmi eszközök
  - prop firm szoftver
sidebar_position: 6
---

# cMind cTrader-brókereknek 🏦

Ön egy cTrader-brókerséget vezet. Az Ön kliensai már tudnak kereskedni — de így tudnak az összes többi bróker klienseiként is. **A cMind lehetővé teszi, hogy az Ön kereskedőinek egy teljes AI-alapú kereskedelmi műveleti platformot adjon meg, a saját márkaneve alatt**, így stratégiákat építenek, backtesztelnek, futtatnak, másolnak és figyelemmel követnek az *Ön* ökoszisztémájában, ahelyett, hogy harmadik féltől származó eszközre sodródnának. Ez ragasztósabb kliensek, nagyobb mennyiség és igazi előny azokkal a brókerekkel szemben, akik semmit nem kínálnak, mint egy terminál.

:::tip[TL;DR]
Futtasson egy fehér címkés cMind-et az Ön klienseihez. Korlátozza az számlákat az **Ön** brókerségéhez, kapcsolja be az AI-t és a másolási kereskedést, és szállítsa a saját márkaneve alatt. → [Fehér címke az üzlethez](./white-label-for-business.md)
:::

## Az előny, amelyet más brókerekhez képest kap

- **Differenciálódjon az eszközökön, nem csak a spreadeken.** Adjon klienseknek AI cBot-generálást, backtesztelést egy felügyelt klaszteren, másolási kereskedést és prop-firm-kihívásokat — képességek, amelyeket a legtöbb bróker egyszerűen nem kínál.
- **Tartsa a klienseket az ökoszisztémájában.** Ha a kereskedők stratégiákat építenek és futtatnak a saját márkanem platformjában, maradnak. A megtartás az egész játék.
- **Az Ön márkaneve alatt, az Ön doménon.** Név, logó, színek, ikonfájl, még az telepítendő telefonalkalmazás is — mindez az Ön tulajdona. Senki sem látja a "cMind-et." → [Fehér címke funkció](./features/white-label.md)

## Csak az Ön számláit szállítja (bróker-engedélyezési lista)

Fehér címkét futtat az *Ön* klienseihez? Korlátozza, hogy mely brókerek kereskedelmi számláit adhat hozzá a felhasználó, így az Ön telepítése csak az Ön könyvét szolgálja:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Az Ön brókerségének neve"]
    }
  }
}
```

Ha az engedélyezési lista be van állítva, a cMind ellenőrzi minden számláját, amelyet a felhasználó megpróbál hozzáadni — mind a cTrader Open API-n keresztül, mind a manuális cID-bejelentkezésen keresztül (az számla valódi brókerasszisztá-nevének olvasásával ellenőrzött) — és elutasítja az Ön listáján kívüli számlákat. Hagyja üresen, és minden bróker engedélyezve van (az alapértelmezett). Tekintse meg a [Fehér címke funkcióját](./features/white-label.md#broker-allowlist) az teljes mechanikához.

## Egy Open API-alkalmazást szállít az összes felhasználó számára

Hagyja ki a felhasználóonkénti nyűgöt: adjon meg **egy cTrader Open API-alkalmazást**, és minden kliens engedélyezi a számláit rajta — nincs olyan kliens, amely regisztrál a sajátjukat. Regisztráljon egyetlen átirányítási URL-t, dobja le a hitelesítő adatokat a config-ba vagy a tulajdonos beállításaiba, és a megosztott mód az összes számára bekapcsol. Tárgyalt magasabb cTrader-üzenetkorlátot? Hangolja be a **üzenettípusonkénti kliens sebesség-korlátokat** (vagy tiltsa le a tempót). → [Megosztott Open API-alkalmazás és sebesség-korlátok](./features/open-api-shared-app.md)

## Új pénzszerzési módok

- **AI, nulla súrlódással a kliensek számára.** Adjon meg egy alapértelmezett AI-szolgáltatói kulcsot a telepítési szinten, és minden kliens azonnal megkapja az AI-funkciókat — nincs szükség regisztrációra máshol. Jelöljük fel, vagy csomagoljuk be a prémium szintekbe. A kliensek még mindig hozhatják saját kulcsukat. → [AI funkció](./features/ai.md)
- **Prop-firm-kihívások.** Futtasson finanszírozott kereskedő-kihívásokat élő saját tőke-nyomon követéssel és kényszerített szabályokkal, és számítson fel belépésekért. → [Prop-firm szabályok](./features/prop-firm.md)
- **Másolási kereskedés üzlet.** A teljesítmény-díjak és a szolgáltatói piacok a másolási kereskedést bevétellé alakítják. → [Teljesítmény-díjak](./features/copy-performance-fees.md) · [Szolgáltatói piac](./features/copy-provider-marketplace.md)
- **Funkció szintek.** Döntse el, mely képességeket lássa az egyes kliens-szegmensek a [funkcióváltógombokkal](./features/feature-toggles.md).

## Szabályozott, auditálható, multi-bérlős

- **[Compliance](./features/compliance.md)** naplók adják az auditcsapást, amelyet a szabályozó megkérdez.
- **[Kétfaktoros hitelesítés](./features/two-factor-auth.md)** kötelezővé tehető telepítésenkénti.
- **Ügyfélenkénti márkaépítés** — futtasson külön márkanem példányt szegmensenként, az Ön saját kontrolláló síkjáról hajtva. → [Multi-bérlős márkaépítés](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Hogyan kezdje el

1. Olvassa el a [Fehér címke az üzlethez](./white-label-for-business.md) feladatot a 60 másodperces átmárkaépítéshez.
2. Állítsa a `App:Accounts:AllowedBrokers` értéket az Ön brókerségéhez, és válassza ki az [funkcióeszközt](./features/feature-toggles.md).
3. [Telepítse](./deployment/cloud.md) — Docker, Kubernetes, Azure vagy AWS.

Nem szeretnél magadnak infrastruktúrát futtatni? Egy üzemeltetési szolgáltatót felügyelet alatt álló cMind-et működtethet — mutasd az [Üzembentartók és VPS-szolgáltatók számára](./for-cloud-providers.md) címét.

## Alakítsd az ütemtervet

A cMind nyílt forráskódú. A brokerekre építő cégeknek disproportionálisan nagy beleszólása van arra, hogy hová megy — kérje meg az integrációkat és vezérléseket, amelyekre szüksége van, és járuljon hozzájuk a [Hozzájárulás útmutatón](./contributing.md) keresztül.
