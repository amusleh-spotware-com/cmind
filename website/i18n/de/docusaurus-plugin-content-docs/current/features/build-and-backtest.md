---
description: "cTrader-cBots (C# und Python, beide .NET) direkt im Browser über die Monaco-IDE erstellen, ausführen und Backtests durchführen – basierend auf dem offiziellen ghcr.io/spotware/ctrader-console-Image."
---

# cBots erstellen & backtesten

cTrader-cBots (C# **und** Python, beide .NET) direkt im Browser über die Monaco-IDE erstellen, ausführen und Backtests durchführen – basierend auf dem offiziellen `ghcr.io/spotware/ctrader-console`-Image.

## Erstellen

- Die **Builder**-Seite hostet den Monaco-Editor; `CBotBuilder` kompiliert das Projekt mit
  `dotnet build` **in einem Einweg-Container** (`AppOptions.BuildImage`, Workdir als Bind-Mount
  bei `/work`), sodass nicht vertrauenswürdige MSBuild-Targets keinen Zugriff auf das Host-System haben.
  NuGet-Restore wird über ein gemeinsames Volume über Builds hinweg gecacht. Der Web-Host
  benötigt Zugriff auf den Docker-Socket.
- C#- und Python-Starter-Templates befinden sich in `src/Nodes/Builder/Templates/`.

## Ausführen & Backtesten

- **Instanzen** = TPH-Statushierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Bei einer Statusänderung wird die Entität ersetzt (ID ändert sich),
  die Container-ID wird beibehalten.
- `NodeScheduler` wählt den am wenigsten ausgelasteten geeigneten Node; `ContainerDispatcherFactory` leitet an
  den Remote-Node-HTTP-Agenten oder den lokalen Docker-Dispatcher weiter.
- Completion-Pollers gleichen beendete Container ab (Backtest-Container beenden sich selbst über
  `--exit-on-stop`); Bericht vorhanden → abgeschlossen (speichert `ReportJson`), fehlend → fehlgeschlagen.
- Live-Container-Logs werden über SignalR an den Browser gestreamt; Backtest-Equity-Kurven werden aus dem
  Bericht geparst und als Chart dargestellt.

## cTrader Console CLI – Hinweise

Backtests benötigen `--data-mode` (Standard `m1`), Datumsangaben als `dd/MM/yyyy HH:mm` und
`params.cbotset` als JSON-Positionsargument; `run` lehnt `--data-dir` ab (nur für Backtests). Siehe
`ContainerCommandHelpers`.

## Nodes & Skalierung

Die Ausführungskapazität wird durch Hinzufügen von Node-Agenten erhöht (Self-Registration + Heartbeat). Siehe
[Node-Erkennung](../operations/node-discovery.md) und [Skalierung](../deployment/scaling.md).
