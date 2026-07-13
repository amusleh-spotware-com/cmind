---
description: "Buduj, uruchamiaj, backtest cTrader cBots (C# i Python, oba .NET) z in-browser Monaco IDE, uruchamiaj na oficjalnym ghcr.io/spotware/ctrader-console obrazie."
---

# Budowanie i backtest cBots

Buduj, uruchamiaj, backtest cTrader cBots (C# **i** Python, oba .NET) z in-browser Monaco
IDE, uruchamiaj na oficjalnym `ghcr.io/spotware/ctrader-console` obrazie.

## Budowanie

- Strona **Builder** hostuje editor Monaco; `CBotBuilder` kompiluje projekt z
  `dotnet build` **w jednorazowym kontenerze** (`AppOptions.BuildImage`, work dir bind-mount
  na `/work`), więc nieufny target użytkownika MSBuild nie dochodzi do host. NuGet restore cached
  między buildami przez wspólny volumen. Web host potrzebuje dostępu do Docker socket.
- Szablony startowe C# + Python żyją w `src/Nodes/Builder/Templates/`.

## Uruchamianie i backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition zastępuje entity (zmiana id),
  container id przeniosity.
- `NodeScheduler` bierze least-loaded eligible node; `ContainerDispatcherFactory` rozsyła do
  remote node HTTP agent lub localny Docker dispatcher.
- Completion pollers reconcilią exited containers (backtest containers self-exit przez
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream do browser nad SignalR; backtest equity curves parsed z
  report + charted.

## cTrader Console CLI notatki

Backtesty potrzebują `--data-mode` (domyślnie `m1`), daty jako `dd/MM/yyyy HH:mm`, i
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Zobacz
`ContainerCommandHelpers`.

## Nodes i skalowanie

Capacity egzekucji skaluje się przez dodanie node agents (self-register + heartbeat). Zobacz
[node discovery](../operations/node-discovery.md) i [scaling](../deployment/scaling.md).
