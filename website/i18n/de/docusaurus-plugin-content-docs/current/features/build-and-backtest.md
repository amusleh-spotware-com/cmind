---
description: "Erstellen Sie cTrader cBots (C# und Python, beide .NET) aus der Monaco-IDE im Browser, führen Sie auf dem offiziellen Image ghcr.io/spotware/ctrader-console aus."
---

# cBots erstellen und backtesten

Erstellen Sie cTrader cBots (C# **und** Python, beide .NET) aus der Monaco-IDE im Browser, führen Sie auf dem offiziellen `ghcr.io/spotware/ctrader-console`-Image aus.

## Erstellen

- **Builder**-Seite hostet Monaco-Editor; `CBotBuilder` kompiliert Projekt mit `dotnet build` **in Einweg-Container** (`AppOptions.BuildImage`, Arbeitsverzeichnis bind-mount unter `/work`), daher können nicht vertrauenswürdige Benutzer MSBuild-Ziele nicht auf dem Host erreichen. NuGet-Wiederherstellung ist über freigegebenes Volume über Builds hinweg zwischengespeichert. Web-Host benötigt Zugriff auf Docker-Socket.
- C# + Python-Starter-Vorlagen befinden sich in `src/Nodes/Builder/Templates/`.

## Ausführen & Backtesten

- **Instances** = TPH-Staatshierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/ `Running`/`Stopping`/`Stopped`/`Failed`). Übergänge ersetzen Entität (ID-Änderung), Container-ID wird mitgenommen.
- `NodeScheduler` wählt die am wenigsten belastete berechtigte Knoten; `ContainerDispatcherFactory` leitet zum Remote-Knoten-HTTP-Agent oder zur lokalen Docker-Dispatcherin weiter.
- Completion Pollers versöhnen ausgequittete Container (Backtest-Container selbst-quit über `--exit-on-stop`); Bericht vorhanden → abgeschlossen (speichern `ReportJson`), fehlend → fehlgeschlagen.
- Live-Container-Protokolle streamen zum Browser über SignalR; Backtest-Eigenkapitalskurven aus Bericht geparst + gechartet.

## cTrader Console CLI-Notizen

Backtests benötigen `--data-mode` (Standard `m1`), Daten als `dd/MM/yyyy HH:mm` und `params.cbotset` JSON-Positionsargument; `run` ablehnen `--data-dir` (nur Backtest). Siehe `ContainerCommandHelpers`.

## Knoten & Skalierung

Die Ausführungskapazität wird durch Hinzufügen von Knoten-Agenten (selbst-Register + Heartbeat) skaliert. Siehe [Knotenerkennung](../operations/node-discovery.md) und [Skalierung](../deployment/scaling.md).
