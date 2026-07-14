---
slug: /for-traders
title: cMind cTrader kereskedőknek
description: Miért egy cTrader kereskedő legyen saját üzemeltetésű cMind — saját stack és adatok, szerzőség, backtest, futtatás és monitoring cBotoknak egy AI-meghajtott konzolban, laptopon, VPS-en vagy telefonon.
keywords:
  - cTrader
  - algoritmikus kereskedelem
  - saját üzemeltetésű kereskedelem platform
  - cBot backtesting
  - AI kereskedelem botok
  - nyílt forráskódú kereskedelem szoftver
sidebar_position: 5
---

# cMind cTrader kereskedőknek 📈

Már kereskedik a cTrader-ben. Már juggel egy kódszerkesztővel, egy backtest-tel, egy VPS-sel és három böngészőlappal. **A cMind összecsomagol mindent egy sötét, billentyűbarát konzolba, amelyet Ön maga futtat** — és nyílt forráskódú, így az Ön előnyei, stratégiái vagy hitelesítő adatai soha nem hagyják el a dobozt.

:::tip[TL;DR]
Saját üzemeltetésben futassa a cMind-et egy laptopon, egy olcsó VPS-en vagy egy otthoni szerveren. Szerzőség, backtest, futtatás és monitoring cBotoknak egy helyen, az AI maggal kezelve a rutinfeladatokat. → [5 percben futtat](./deployment/local.md)
:::

## Miért saját üzemeltetés helyett üzemeltetett szolgáltatás?

- **Sajátja a stack és az adatok.** Az Ön cBotjai, hitelesítő adatai, tokenei és tőke-története az **Ön infrastruktúráján** élnek — nincs harmadik fél, nincs zárolás, nincs "ezt a terméket leállítjuk" e-mail.
- **Tényleg az Öntulajdona, amely módosítható.** C# 14 / .NET 10, szigorú DDD, EF Core + PostgreSQL, MCP szerver — mind nyílt forráskódú és módosítható. Forkozzék, bővítse, küldjön PR-t.
- **Nincs funkciónkénti paywall.** Hozza az Ön AI kulcsát bármely szolgáltatóhoz; minden AI funkció bekapcsolt.

Nem szeretne szervereket saját maga futtatni? Egy üzemeltetési vállalat futtathat Ön számára felügyelt cMind-et — lásd: [Felhő és VPS szolgáltatók számára](./for-cloud-providers.md).

## Egy konzol, nincs lapjuggling

- **Szerzőség** egy valódi Monaco IDE-ben (a VS Code szerkesztő), C# **és** Python sablonokkal, valamint sandboxolt `dotnet build`-kel dobható konténerekben. → [Build és backtest](./features/build-and-backtest.md)
- **Backtest** egy csomópontos flottán keresztül, és figyeljük az equity görbéket élőben visszaáramlani.
- **Futtasson** stratégiákat élőben és **figyelje** őket egy irányítópultról. → [Irányítópult](./features/dashboard.md)
- **Másolja** a master fiókot sok fiókra brokerek és cTrader ID-k között, egyeztetéssel, amely túléli az összeomlott csatlakozásokat és a tokenrotációt. → [Másolási kereskedelem](./features/copy-trading.md)

## AI, amely elvégzi a rutinfeladatokat, nem csevegést

Hozza az Ön API kulcsát (bármely támogatott szolgáltató — felhő vagy helyi modell), és kapjon egy valódi fordítható cBotot egyszerű angol nyelvből egy öno javítási hurokkal, paraméter-hangolással, backtest utólagos boncolással és egy olyan kockázati őrséggel, amely képes egy hibás botot auto-megállítani. → [Ismerjük meg az AI maggal](./features/ai.md)

## Intézményi szintű szerszámozás, egy személyre

Ugyanez az ápolgatási szigor, amelyet egy asztal megfizetne, saját dobozán:

- [Backtest integritás](./features/backtest-integrity.md) · [Pozíció méretezés](./features/position-sizing.md)
- [Stratégia egészség](./features/strategy-health.md) · [Regime labor](./features/regime-lab.md)
- [Végrehajtás TCA](./features/execution-tca.md) · [Kereskedelem napló](./features/trading-journal.md)
- [Ügynök Studio](./features/agent-studio.md) · [Ellentétes pozicionálás](./features/contrarian-positioning.md)

## Ott fut, ahol Ön

Kezdje a laptopon a `docker compose up`-val, lépjen egy olcsó VPS-re vagy egy otthoni szerverre, amikor készen van, és figyelje meg a botjait a telefonon — a cMind egy telepíthető, mobilbarát [PWA](./features/pwa.md). → [Helyileg futtatása](./deployment/local.md)

Szeretné, ha az Ön AI kliense üzemeltetné? Van egy beépített [MCP szerver](./features/mcp.md).

## Segítsen, hogy jobb legyen

A cMind nyílt forráskódú és MIT licencelt — az ütemterv közösség alakít:

- Nyisson be hibákat és funkcióköveteléseket, és szavazzon arról, mi számít.
- cBot sablonok, AI szolgáltató adapterek vagy felhasználói felület fordítások hozzáadása.
- Küldjön PR-eket — három teszt szint (egység + integráció + E2E) és szigorú DDD tartja a magas szintet, az [Közreműködési útmutató](./contributing.md) végigvezeti.

Kész? → [Olvassa el a bevezető](./intro.md), majd [helyileg futtatása](./deployment/local.md).
