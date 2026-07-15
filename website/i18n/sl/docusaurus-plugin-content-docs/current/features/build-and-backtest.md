---
description: "Gradnja, tečenje, testiranje cBotov cTraderja (C in Python, oba .NET) iz IDE Monaco v brskalniku, tečenje na uradni sliki ghcr.io/spotware/ctrader-console."
---

# Gradnja in testiranje cBotov

Gradnja, tečenje, testiranje cBotov cTraderja (C# **in** Python, oba .NET) iz IDE Monaco v
brskalniku, tečenje na uradni sliki `ghcr.io/spotware/ctrader-console`.

## Gradnja

- Stran **Graditeljja** je gostitelj urejevalnika Monaco; `CBotBuilder` prevede projekt s
  `dotnet build` **v zahodnem kontejnerju** (`AppOptions.BuildImage`, delavni direktorij
  priklopljen pri `/work`), torej neverjeti uporabnik ciljov MSBuild nihče ne doseže
  gostitelja. NuGet obnova predpomnjena čez gradnje prek skupne glasnosti. Spletni
  gostitelj potrebuje dostop do vtičnice Docker.
- Startni predlogi C# + Python živijo v `src/Nodes/Builder/Templates/`.

## Tečenje in testiranje

- **Instanci** = TPH hierarhija statusa (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prehod nadomesti entiteto (sprememba id),
  id kontejnerja nesen čez.
- `NodeScheduler` izbere najmanj obremenjena upravičenega vozlišča; `ContainerDispatcherFactory`
  usmeriti na daljava vozlišča agent HTTP ali lokalni razpečevalni orodje Docker.
- Pokerji dokončanja se pomirijo izstopljenim kontejnerjem (kontejnerji testiranja samih izstopov
  prek `--exit-on-stop`); report prisoten → zaključen (shraniti `ReportJson`), odsoten → neuspešen.
- Živi dnevniki kontejnerja tečejo na brskalnik čez SignalR; testiranje krivulje lastnine razčlenjene
  iz report + napredkov.

## Opombe CLI cTrader Console

Testiranja potrebujejo `--data-mode` (privzeto `m1`), datumi kot `dd/MM/yyyy HH:mm` in
`params.cbotset` JSON pozicijsko argumento; `run` zavrni `--data-dir` (samo testiranje). Poglejte
`ContainerCommandHelpers`.

## Vozlišča in lestvica

Zmogljivost izvršitve lestvica z dodajanjem agentov vozlišča (samoregustracija + srčni utrip).
Poglejte [odkrivanje vozlišča](../operations/node-discovery.md) in [skaliranje](../deployment/scaling.md).

## Zagon iz urejevalnika kode

Klik na **Zaženi** v urejevalniku kode odpre pogovorno okno namesto slepega, trdo kodiranega zagona:

- **Trgovalni račun** (obvezno) — račun cTrader, s katerim se cBot poveže.
- **Nabor parametrov** (izbirno) — izberite obstoječi nabor ali pustite prazno za zagon s **privzetimi vrednostmi parametrov** cBota. Gumb **+** ob izbirniku ustvari nov nabor parametrov na mestu (glejte spodaj) in ga izbere.
- **Simbol / Časovni okvir** sta privzeto `EURUSD` / `h1` in ju je mogoče spremeniti; **Prekliči** ali **Zaženi**.

Ob **Zaženi** urejevalnik shrani in zgradi trenutno izvorno kodo, zažene instanco na izbranem računu z izbranimi parametri, nato pa sledi dnevnikom vsebnika v živo. (Tok dnevnikov posreduje avtentikacijski piškotek prijavljenega uporabnika v vozlišče SignalR `/hubs/logs`, tako da se poveže namesto da bi spodletel z `Invalid negotiation response received`.)

## Nabori parametrov

**Nabor parametrov** je poimenovan, večkrat uporaben nabor preglasitev parametrov cBota, shranjen kot ploski objekt JSON, ki vsako ime parametra preslika v skalarno vrednost, npr. `{"Period": 14, "Label": "trend"}`. Ob zagonu/backtestu se pretvori v datoteko cTrader `params.cbotset` (`{ "Parameters": { … } }`). Nabor lahko ustvarite/uredite kot čisti JSON iz pogovornega okna **Nabori parametrov** cBota ali na mestu iz pogovornega okna Zaženi.

JSON se ob shranjevanju **preveri**: biti mora en sam ploski objekt, katerega vse vrednosti so skalarne (niz / število / bool). Koren, ki ni objekt, polje, gnezden objekt, vrednost `null` ali napačno oblikovan JSON se zavrne (jasna napaka v pogovornem oknu, `400 Bad Request` v API). Prazen objekt `{}` je dovoljen in pomeni »brez preglasitev«.
