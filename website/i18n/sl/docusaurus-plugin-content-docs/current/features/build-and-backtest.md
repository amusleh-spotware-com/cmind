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
