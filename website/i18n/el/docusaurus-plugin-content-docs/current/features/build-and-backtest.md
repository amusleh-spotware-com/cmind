---
description: "Δημιουργήστε, εκτελέστε, κάντε backtest cTrader cBots (C# και Python, και τα δύο .NET) από το in-browser Monaco IDE, εκτελέστε στο επίσημο ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Δημιουργήστε, εκτελέστε, κάντε backtest cTrader cBots (C# **και** Python, και τα δύο .NET) από το in-browser Monaco
IDE, εκτελέστε στο επίσημο `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page φιλοξενεί το Monaco editor; `CBotBuilder` μεταγλωττίζει το project με
  `dotnet build` **σε throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  στο `/work`), ώστε τα untrusted user MSBuild targets να μην έχουν πρόσβαση στο host. Η NuGet restore κρυώνεται
  διαδρόμων builds μέσω shared volume. Το Web host χρειάζεται Docker socket access.
- C# + Python starter templates ζουν στο `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Η transition αντικαθιστά την entity (id change),
  container id μεταφέρεται.
- `NodeScheduler` επιλέγει το λιγότερο φορτωμένο eligible node; `ContainerDispatcherFactory` δρομολογεί
  στον remote node HTTP agent ή local Docker dispatcher.
- Completion pollers συμφιλιώνουν exited containers (backtest containers self-exit μέσω
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Τα live container logs ρέουν στο browser μέσω SignalR; τα backtest equity curves αναλύονται από
  report + charted.

## cTrader Console CLI σημειώσεις

Τα Backtests χρειάζονται `--data-mode` (default `m1`), dates ως `dd/MM/yyyy HH:mm`, και
`params.cbotset` JSON positional arg; `run` απορρίπτει `--data-dir` (backtest-only). Δείτε
`ContainerCommandHelpers`.

## Nodes & scale

Η execution capacity κλιμακώνεται με την προσθήκη node agents (self-register + heartbeat). Δείτε
[node discovery](../operations/node-discovery.md) και [scaling](../deployment/scaling.md).
