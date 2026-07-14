---
slug: /features
title: Značilnosti — polni obhod
description: Vse, kar zmore cMind — kopiranje trgovanja, AI, gradnja in testiranje, varnostne službe za lastnine, belo označena, PWA, MCP in še več.
sidebar_label: Pregled
---

# Značilnosti — polni obhod 🧭

Dobrodošli na velikem obhodu. cMind pakira *veliko* v eno aplikacijo, zato je tukaj zemljevid. Vsaka
zmožnost ima svoj poglobljeni dokument — klikni na katero koli vozlino, ki ti praskati.

## 🔁 Kopiranje trgovanja

Krona dragulj. Zrcalite glavni račun na mnoge in jih obdržite v sinhronizaciji, tudi ko se internet
ne obnaša.

- **[Kopiranje trgovanja](./copy-trading.md)** — jedro: zrcaljenje, vrste naročil, SL/TP, zdrs, desinhronizacija/resinhronizacija.
- **[Transparentnost izvajanja](./copy-execution-transparency.md)** — poglejte, kaj je bilo kopiranega, kdaj in zakaj.
- **[Provizije za zmogljivost](./copy-performance-fees.md)** — zaračunajte za svoj signal, slog visoke vode.
- **[Trg ponudnikov](./copy-provider-marketplace.md)** — dovolite trgovcem, da odkrijejo in sledijo ponudnikom.
- **[Obvestila](./copy-notifications.md)** — bodite obveščeni, ko vam nekaj potrebuje.
- **[Priporočnik za kopiranje AI](./ai-copy-recommender.md)** — pusti AI, da predlaga, komu slediti.
- **[Obdobje žetona Open API](./token-lifecycle.md)** — kako cMind ohranja točno en veljaven žeton na cID.

## 📊 Vaša domača baza

- **[Nadzorna plošča](./dashboard.md)** — živi, mobilno prvo poveljni center: KPI s črtami, grafikon dejavnosti,
  prstan statusa, živi vir in (za administratorje) zdravje grozda. Samočisti se samočisti.

## 🧠 Jedro AI

Ne pogovorna polja, prilepljena na stran — AI, ki res *dela delo*.

- **[Pomočnik AI, agent, varnostna varnica in obvestila](./ai.md)** — generiranje strategije, samopopravljajoče se gradnje,
  varnostna varnica v ozadju, ki lahko samodejno ustavlja bote, in pametna obvestila.

## 🛠️ Gradnja in tečenje

- **[Gradnja in testiranje cBotov](./build-and-backtest.md)** — IDE Monaco v brskalniku, predloge C#/Python, peskovniku
  gradnje in živih krivuljah lastnine.
- **[Strežnik MCP](./mcp.md)** — razkrijte orodja cMind preko HTTP + SSE, da lahko odjemalci AI to pogonijo.

## 🏢 Tečite ga kot poslovanje

- **[Belo označena / branding](./white-label.md)** — ponovno branding vsake površine prek konfiguracije.
- **[Simulacija izziva za lastnino](./prop-firm.md)** — uveljavljajte dnevne izgube, narihtanje in ciljne pravila z
  živo lastnino.
- **[Stikalne zmožnosti](./feature-toggles.md)** — odločite, kaj vsako nameščanje/najemnik vidi.
- **[Skladnost / zakonodajno](./compliance.md)** — sledilnik revizije in zakonska površina.

## 📱 Izkušnja

- **[Namestljiva aplikacija (PWA)](./pwa.md)** — mobilno prvo, lupina v režimu brez interneta, dodajte na domačo zaslon.
- **[Sistem oblikovanja uporabniškega vmesnika in mobilno prvo](../ui-guidelines.md)** — žetoni oblikovanja in
  pravila za videz.

## ⚙️ Pod pokrovom

Operativni deli, ki to vse drži delujoče:

- **[Flota vozlišč in odkrivanje](../operations/node-discovery.md)** — kako se vozlišča samodejno registrirajo in zdravijo.
- **[Vodoravno skaliranje](../deployment/scaling.md)** — dodajte replike, ne potrebnega zunanjega usklajevanja.
- **[Beležka in revizija](../operations/logging.md)** — strukturirani dnevniki + OpenTelemetry.
- **[Nameščanje](../deployment/local.md)** — ga tečite kjerkoli.

:::note[Keeping docs honest]
Vsak dokument značilnosti je vzdržavan v koraku s kodo — spremenite vedenje, ažurirajte dokument, isti
potrdek. Če kdaj opazite drift, to je napaka: prosimo
[odprite težavo](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) ali pošljite PR. 🙏
:::
