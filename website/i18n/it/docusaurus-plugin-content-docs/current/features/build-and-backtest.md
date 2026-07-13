---
description: "Build, run, backtest cTrader cBots (C# and Python, entrambi .NET) da Monaco IDE in-browser, eseguiti su immagine ufficiale ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **e** Python, entrambi .NET) da Monaco
IDE in-browser, eseguiti sull'immagine ufficiale `ghcr.io/spotware/ctrader-console`.

## Build

- La pagina **Builder** host Monaco editor; `CBotBuilder` compila il progetto con
  `dotnet build` **in container monouso** (`AppOptions.BuildImage`, work dir bind-mount
  a `/work`), così i target MSBuild non trusted dell'utente non raggiungono l'host. Il restore NuGet è cached
  attraverso i build via volume condiviso. L'host Web necessita accesso al Docker socket.
- I starter template C# + Python vivono in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = gerarchia stato TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transizione rimpiazza l'entità (cambio id),
  il container id è portato over.
- `NodeScheduler` sceglie il nodo eleggibile meno caricato; `ContainerDispatcherFactory` instrada all'
  agente nodo HTTP remoto o al dispatcher Docker locale.
- I poller di completamento riconciliano i container usciti (i container backtest escono da soli via
  `--exit-on-stop`); report presente → completed (memorizza `ReportJson`), mancante → failed.
- I log container live stream al browser su SignalR; le curve equity del backtest sono parsificate dal
  report + charted.

## Note CLI cTrader Console

I backtest necessitano `--data-mode` (default `m1`), date come `dd/MM/yyyy HH:mm`, e
`params.cbotset` JSON positional arg; `run` rifiuta `--data-dir` (solo backtest). Vedere
`ContainerCommandHelpers`.

## Nodi e scale

La capacità di esecuzione scala aggiungendo agenti nodo (auto-register + heartbeat). Vedere
[node discovery](../operations/node-discovery.md) e [scaling](../deployment/scaling.md).

## È richiesto un account di trading

Eseguire o backtestare un cBot necessita di un account di trading cTrader a cui connettersi. Finché non ne
aggiungi uno sotto **Trading accounts**, i pulsanti **Run New cBot** / **Backtest New cBot** sono
disabilitati (con una tooltip) e la pagina mostra un prompt che linka al setup dell'account — non ricevi
più un raw errore `stream connect failed` da un bot senza account.
