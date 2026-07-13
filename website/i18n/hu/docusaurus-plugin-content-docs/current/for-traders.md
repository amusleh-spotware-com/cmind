---
slug: /hu/for-traders
title: cMind cTrader kereskedőknek
description: Miért egy cTrader kereskedő legyen öngazda cMind-et — saját stack és adatok, szerző, backtest, futtatás és monitor cBot-ot az egyik AI-meghatározott konzol, az laptopon, VPS vagy telefonon.
keywords:
  - cTrader
  - algoritmus kereskedés
  - öngazda kereskedés platform
  - cBot backtesting
  - AI kereskedés botok
  - nyílt forráskódú kereskedés szoftver
sidebar_position: 5
---

# cMind cTrader kereskedőknek 📈

Már kereskedik a cTrader-en. Már juggling egy kód szerkesztő, egy backtester, egy VPS, és három böngésző lapot. **A cMind összeomlik az összes azt az egy sötét, billentyűbarát konzol amely az futtat magad** — és ez nyílt forráskódú, így semmi körülötted az él, az stratégiák, vagy az hitelesítő adatok soha nem hagyja az dobozat.

:::tip TL;DR
Öngazda cMind az laptop, az olcsó VPS, vagy az házi szerver. Szerző, backtest, futtatás, és monitor cBot-okat az egy hely, az AI mag még az rá csuklik. → [Futtatódik azt az 5 perc](./deployment/local.md)
:::

## Miért öngazda helyette az üzemeltetett szolgáltatás?

- **Saját az stack és az adatok.** Az cBot-ok, hitelesítő adatok, tokenek, és saját-történet az él az **az saját infrastruktúrát** — nincs külső-fél, nincs lock-in, nincs "az cél az terméket."
- **Ez az valóban az te az megváltoztatni.** C# 14 / .NET 10, szigorú DDD, EF Core + PostgreSQL, az MCP szerver — az összes nyílt forráskódú és hacker. Villanyöd azt, kiterjeszted azt, küldd egy PR.
- **Nincs per-funkció paywall.** Hozz az saját AI kulcsot az bármi szolgáltató; az mindegyik AI funkció az.

Szeretne nem futtatódik szerver magad? Az üzemeltetési vállalat lehet futtat az felügyelt cMind az te — lásd az [Az felhő & VPS szolgáltatóknak](./for-cloud-providers.md).

## Egy konzol, nincs lap-juggling

- **Szerző** az egy valódi Monaco IDE (az VS Code szerkesztő), az C# **és** Python sablonokat és az sandboxed `dotnet build` az eldobható konténerek. → [Építés & backtest](./features/build-and-backtest.md)
- **Backtest** az flotta az node-ok és az nézet egyenlőség görbék áramlik vissza él.
- **Futtatódik** stratégiák él és **monitor** azokat az egy műsorfal. → [Műsorfal](./features/dashboard.md)
- **Másolás** egy fő számlát az sok számlák az felett az brókerek és az cTrader ID-ked, az egyeztetés amely az túléli az csökkent összeköttetések és az forgató tokenek. → [Másolás kereskedés](./features/copy-trading.md)

## AI amit az marad az rá csuklik, nem az apró beszélgetés

Hozz az saját API kulcsot (az bármi támogatott szolgáltató — az felhő vagy az helyi modell) és az kap az egyenes-angol → az egy valódi compiláció cBot az self-javítás hurok, az paraméter tuning, az backtest után-mortemek, és az kockázat őr amely lehet azt önállóan-leállítás egy misbehaving bot. → [Találkozni az AI mag](./features/ai.md)

## Intézmények-minőség: eszközök, az egy

Az azonos szigor egy asztal fizet az, az saját dobozat:

- [Backtest integritás](./features/backtest-integrity.md) · [Pozíció méretezés](./features/position-sizing.md)
- [Stratégia egészség](./features/strategy-health.md) · [Rezsim labor](./features/regime-lab.md)
- [Végre hajtás TCA](./features/execution-tca.md) · [Kereskedés napló](./features/trading-journal.md)
- [Ügynök Studio](./features/agent-studio.md) · [Ellentétes pozicionálás](./features/contrarian-positioning.md)

## Futtatódik ahol az te

Kezdem az laptoppal az `docker compose up`, végzem az olcsó VPS vagy az házi szerver amikor az kész volt, és az bejelöl az cBot-ok az laptopon — a cMind az egy telepíthető, mobil-első [PWA](./features/pwa.md). → [Futtatódik azt helyileg](./deployment/local.md)

Szeretnél az AI kliens az meghajtás azt? Van egy beépített [MCP szerver](./features/mcp.md).

## Segítséget nyújt ajánló az azt

A cMind nyílt forráskódú és MIT-Licensed — az útitérkép közösség-alakított:

- Fájl problémákat és funkció kérelmeket, és szavazat az mi tét.
- Adjon hozzá cBot sablonokat, AI szolgáltató adaptációkat, vagy UI fordítások.
- Küldjön PR-eket — három teszt szintek (egység + integráció + E2E) és szigorú DDD megtartani az rúd magas, és az [Közreműködés útmutató](./contributing.md) járul során azt.

Kész? → [Olvas az bevezetés](./intro.md) majd [futtatódik azt helyileg](./deployment/local.md).
