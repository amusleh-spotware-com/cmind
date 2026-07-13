---
slug: /contributing
title: Hozzájárulás
description: Hogyan járulhatsz hozzá a cMind-hez — az emberi vagy AI-asszisztált PR-ek üdvözlendőek. Első hozzájárulás 10 perc alatt.
sidebar_position: 5
---

# Hozzájárulás a cMind-hez 🛠️

Köszönjük, hogy itt vagy. A cMind minden alkalommal jobban lesz, amikor valaki megnyit egy problémát, pontos cTrader-viselkedést jelent, javít egy gépelési hibát ebben a dokumentációban, vagy egy PR-t szállít. **Nem kell, hogy .NET-varázsló legyél** — a tesztelők, kereskedők és dokumentáció-javítók ugyanolyan értékesek, mint az aggregátumokat írók.

:::tip A kanonikus útmutató az adattárban található
Ez az oldal a barátságos bevezetés. A teljes, mindig aktuális folyamat — alapelvek, kódolási konvenciók, felülvizsgálat-folyamat — a **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**-ben van.
:::

## Az első hozzájárulás ~10 perc alatt

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 figyelmeztetés, vagy a CI udvariasan megtagadja
dotnet test           # egység + integráció + E2E
```

Talált valamit, amit javítani kell? Ágazza meg, módosítsa, adjon hozzá egy tesztet, és nyisson egy PR-t. Ez az egész hurok.

## Módok a segítségnek (nem mindegyik kód)

| Hozzájárulás | Erőfeszítés | Hely |
|---|---|---|
| 🐛 Jelenjen meg egy reprodukálható hibát | 10 perc | [Hibajelentés](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Javaslatot tegyen egy funkció | 10 perc | [Funkcióirányzat](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Javítsa ezeket a dokumentákat | 15 perc | Szerkesztés a `website/docs/` alatt és PR |
| 🧪 Adjon hozzá egy hiányzó tesztet | 30 perc | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Jelenti az pontosan cTrader-viselkedést | 10 perc | [Nyisson Vitát](https://github.com/amusleh-spotware-com/cmind/discussions) |

## A házszabályok (rövid verzió)

A cMind **valódi pénzt** mozgat, tehát néhány dolog nem megállapodható — és őszintén szólva, az a kódtábla csodálatos munkahelyet tesz:

- **Szigorú Domain-Driven Design.** Az üzleti logika az aggregátumokon és értékobjektumokon él, sosem a végpontokon vagy UI-ban. (Az adattárban van egy barátságos játékkönyv.)
- **Három teszti szint, minden változtatásban.** Egység + integráció + E2E, *beleértve* a hibaútvonalakat (csökkent kapcsolatok, visszautasított rendelések, halott csomópontok). A zöld tesztek az intézmény belépésének ára.
- **Nulla figyelmeztetés.** `TreatWarningsAsErrors=true`. Modern C# 14 kifejezések.
- **Nincs titok, nincs varázsló karakterláncok, soha `DateTime.UtcNow`** (helyette `TimeProvider` inject).
- **Docs ugyanabban a commitban.** Viselkedés módosítása → frissítse a dokumentációját. Igen, ez magát ezt az oldalt is magában foglalja.

A teljes részlet, az egyes szabályok *miértjével* együtt, a [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) és az [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md) között található.

## Hozzájárulás AI-nal 🤖

Nagyon szívesen fogadunk **AI-asszisztált PR-eket** — ez a projekt arra van kialakítva, hogy ügynökök és emberek is működjenek rajta. Ha a Claude, Copilot vagy hasonló meghajtáson van: mutasson az [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md) felé, hagyja, hogy olvassa a beágyazott `CLAUDE.md`-fájlokat, és tartsa ugyanabban a szintben (tesztek, nulla figyelmeztetések, DDD). Egy jó AI PR nem különbözik egy jó emberi PR-tól — ugyanaz a felülvizsgálat, ugyanaz az üdvözlet.

## Légy kitűnő egymásnak

Van egy [Magatartási Kódex](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
Az középsulya: légy kedves, tegyél fel jó hitről, és emlékezz, hogy a másik végén van egy ember (vagy egy ember ügynöke). Kérdezd meg korán — ez erő, nem zavar.

Üdvözöljük a fedélzeten. Nem tudjuk, hogy mit építesz. 🎉
