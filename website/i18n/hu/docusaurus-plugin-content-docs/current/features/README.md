---
slug: /features
title: Funkciok - a teljes koru bemutato
description: Amit a cMind tud - masolasi kereskedes, AI, epit es backtest, prop-firm orok, white-label, PWA, MCP es meg sok mas.
sidebar_label: Áttekintés
---

# Funkciók - a teljes koru bemutató

Üdvözöl a grand tour. A cMind rengeteg funkciót zsúfol egyetlen alkalmazásba, szóval itt a térkép. Minden képességnek saját mély-merülős docja van - kattints át, amid itch-et karcol.

## 🔁 Masolasi kereskedes

A koronaékszer. Tükrözd egy master számlát sok másra, és tartsd őket szinkronban, még ha az internet bajban is van.

- **[Masolasi kereskedes](./copy-trading.md)** - a lényeg: tükrözés, megbízástípusok, SL/TP, csúszás, deszinkronizáció/újraszinkronizáció.
- **[Végrehajtási átláthatóság](./copy-execution-transparency.md)** - lásd pontosan, mi lett másolva, mikor és miért.
- **[Teljesítménydíjak](./copy-performance-fees.md)** - terhelj a jeledért, magas vízjel stílusban.
- **[Szolgáltatói piactér](./copy-provider-marketplace.md)** - lehetoség a kereskedőknek, hogy felfedezzék és kövessék a szolgáltatókat.
- **[Értesítések](./copy-notifications.md)** - értesülj, amikor valamire szükséged van.
- **[AI másolási ajánló](./ai-copy-recommender.md)** - az AI sugallja, kit érdemes másolni.
- **[Open API token életciklus](./token-lifecycle.md)** - hogyan tart a cMind pontosan egy érvényes tokent cID-nként.

## 📊 Az otthoni bázisod

- **[Műszerfal](./dashboard.md)** - az élő, mobil-először parancsközpont: KPI-k sparkline-okkal, aktivitási chart, állapot gyűrű, élő feed, és (adminoknak) a fürt egészsége. Önmagát frissíti.

## 🧠 AI mag

Nem egy chat doboz, amit odaragasztottak - AI, ami tényleg *csinálja a munkát*.

- **[AI asszisztens, ügynök, kockázati őr és riasztások](./ai.md)** - stratégia generálás, önjavító build-ek, egy háttér kockázati őr, ami auto-leállíthatja a botokat, és okos riasztások.

## 🛠️ Építés és futtatás

- **[Build és backtest cBots](./build-and-backtest.md)** - a böngészőben lévő Monaco IDE, C#/Python sablonok, sandboxolt build-ek és élő equity görbék.
- **[MCP szerver](./mcp.md)** - teszi a cMind eszközeit elérhetővé HTTP + SSE felett, hogy az AI kliensek vezethessék.

## 🏢 Üzemeltetés üzletként

- **[White-label / branding](./white-label.md)** - minden felület átnevezése konfiguráció révén.
- **[Prop-firm challenge szimuláció](./prop-firm.md)** - kényszerítsd a napi veszteség, drawdown és cél szabályokat élő equity-vel.
- **[Funkció kapcsolók](./feature-toggles.md)** - döntsd el, mit lát minden telepítés/bérlő.
- **[Compliance / jogi](./compliance.md)** - az audit nyomvonal és jogi felület.

## 📱 A felhasználói élmény

- **[Telepíthető alkalmazás (PWA)](./pwa.md)** - mobil-először, offline shell, hozzáadás a kezdőképernyőhöz.
- **[UI tervezési rendszer és mobil-először](../ui-guidelines.md)** - a tervezési tokenek és szabályok a megjelenés mögött.

## ⚙️ A motorháztető alatt

A működésben tartó operatív részek:

- **[Csomópont fletta és felfedezés](../operations/node-discovery.md)** - hogyan regisztrálják magukat a csomópontok és gyógyulnak meg.
- **[Vízszintes skálázás](../deployment/scaling.md)** - adj hozzá replikákat, külső koordinátor nélkül.
- **[Naplózás és audit](../operations/logging.md)** - strukturált naplók + OpenTelemetry.
- **[Telepítés](../deployment/local.md)** - futtasd bárhol.

:::note A docs-ok őszintétartása
Minden funkció docja lépést tart a kóddal - változtasd meg a viselkedést, frissítsd a docot, ugyanabban a commitban. Ha valaha eltérést észlelsz, az egy hiba: kérlek nyiss egy issue-t vagy küldj PR-t. :::
