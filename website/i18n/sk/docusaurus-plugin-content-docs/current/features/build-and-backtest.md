---
description: "Build, run, backtest cTrader cBots (C# a Python, oboje .NET) z in-browser Monaco IDE, spustite na official ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, spustite, backtest cTrader cBots (C# **a** Python, oboje .NET) z in-browser Monaco
IDE, spustite na official `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile projekt s
  `dotnet build` **v throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  na `/work`), takže neverihodný user MSBuild targets nemôže dosiahnuť host. NuGet restore cached
  cross builds cez shared volume. Web host potrebuje Docker socket access.
- C# + Python starter templates žijú v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route na
  remote node HTTP agent alebo local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit cez
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream na browser cez SignalR; backtest equity curves parsed z
  report + charted.

## cTrader Console CLI notes

Backtesty potrebujú `--data-mode` (default `m1`), dates ako `dd/MM/yyyy HH:mm` a
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Pozrite
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale by adding node agents (self-register + heartbeat). Pozrite
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Spustenie z editora kódu

Kliknutie na **Spustiť** v editore kódu otvorí dialóg namiesto slepého, napevno zakódovaného spustenia:

- **Obchodný účet** (povinné) — účet cTrader, ku ktorému sa cBot pripája.
- **Súprava parametrov** (voliteľné) — vyberte existujúcu súpravu alebo nechajte prázdne pre spustenie s **predvolenými hodnotami parametrov** cBota. Tlačidlo **+** vedľa výberu vytvorí novú súpravu parametrov priamo tu (pozri nižšie) a vyberie ju.
- **Symbol / Časový rámec** sú predvolene `EURUSD` / `h1` a možno ich zmeniť; **Zrušiť** alebo **Spustiť**.

Pri **Spustiť** editor uloží a zostaví aktuálny zdrojový kód, spustí inštanciu na zvolenom účte so zvolenými parametrami a potom sleduje živé logy kontajnera. (Prúd logov posiela autentifikačné cookie prihláseného používateľa do SignalR hubu `/hubs/logs`, takže sa pripojí namiesto zlyhania s `Invalid negotiation response received`.)

## Súpravy parametrov

**Súprava parametrov** je pomenovaná, opakovane použiteľná súprava prepisov parametrov cBota, uložená ako plochý objekt JSON mapujúci každý názov parametra na skalárnu hodnotu, napr. `{"Period": 14, "Label": "trend"}`. Pri spustení/backteste sa prevedie na súbor cTrader `params.cbotset` (`{ "Parameters": { … } }`). Súpravu možno vytvoriť/upraviť ako čisté JSON z dialógu **Súpravy parametrov** cBota alebo priamo z dialógu Spustiť.

JSON sa pri uložení **validuje**: musí to byť jediný plochý objekt, ktorého všetky hodnoty sú skalárne (reťazec / číslo / bool). Koreň, ktorý nie je objekt, pole, vnorený objekt, hodnota `null` alebo poškodené JSON sa odmietnu (jasná chyba v dialógu, `400 Bad Request` v API). Prázdny objekt `{}` je povolený a znamená „žiadne prepisy".
