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

## Aus dem Code-Editor ausführen

Ein Klick auf **Ausführen** im Code-Editor öffnet einen Dialog, statt einen blinden, fest verdrahteten Lauf zu starten:

- **Trading-Konto** (erforderlich) – das cTrader-Konto, mit dem sich der cBot verbindet.
- **Parametersatz** (optional) – einen vorhandenen Satz wählen oder leer lassen, um mit den **Standard-Parameterwerten** des cBots zu laufen. Eine **+**-Schaltfläche neben der Auswahl erstellt inline einen neuen Parametersatz (siehe unten) und wählt ihn aus.
- **Symbol / Zeitrahmen** sind standardmäßig `EURUSD` / `h1` und änderbar; **Abbrechen** oder **Ausführen**.

Bei **Ausführen** speichert und baut der Editor den aktuellen Quellcode, startet die Instanz auf dem gewählten Konto mit den gewählten Parametern und verfolgt dann die Live-Container-Logs. (Der Log-Stream leitet das Auth-Cookie des angemeldeten Benutzers an den SignalR-Hub `/hubs/logs` weiter, sodass er sich verbindet, statt mit `Invalid negotiation response received` zu scheitern.)

## Parametersätze

Ein **Parametersatz** ist ein benannter, wiederverwendbarer Satz von cBot-Parameter-Überschreibungen, gespeichert als flaches JSON-Objekt, das jeden Parameternamen auf einen skalaren Wert abbildet, z. B. `{"Period": 14, "Label": "trend"}`. Beim Ausführen/Backtesten wird daraus die cTrader-Datei `params.cbotset` (`{ "Parameters": { … } }`) erzeugt. Sie können einen Satz als reines JSON über den Dialog **Parametersätze** des cBots oder inline aus dem Ausführen-Dialog erstellen/bearbeiten.

Das JSON wird beim Speichern **validiert**: Es muss ein einzelnes flaches Objekt sein, dessen Werte alle Skalare sind (String / Zahl / Bool). Ein Nicht-Objekt-Root, ein Array, ein verschachteltes Objekt, ein `null`-Wert oder fehlerhaftes JSON wird abgelehnt (klarer Fehler im Dialog, `400 Bad Request` an der API). Ein leeres Objekt `{}` ist erlaubt und bedeutet „keine Überschreibungen".

## Steuerung des Instanz-Lebenszyklus

Jede Instanzzeile (und ihre Detailseite) hat zustandskorrekte Steuerelemente. Eine **aktive** Instanz zeigt **Stopp**; eine **terminale** (Gestoppt / Abgeschlossen / Fehlgeschlagen) zeigt **Start (▶)**, um sie mit demselben cBot, Konto, Symbol, Zeitrahmen, Parametersatz und Image erneut zu starten (ein Lauf startet als Lauf, ein Backtest als Backtest). Ein Klick auf Stopp zeigt einen Hinweis „Wird gestoppt…" und deaktiviert das Symbol, bis er abgeschlossen ist; ein neu erstellter Lauf erscheint sofort in der Liste – ohne Neuladen der Seite.

Konsolenprotokolle werden **beim Beenden einer Instanz gespeichert** – sowohl für einen Lauf (beim Stoppen) als auch für einen **Backtest** (beim Abschluss) – sodass die Protokolle des letzten Laufs auf der Detailseite sichtbar und über das Symbol **Protokolle herunterladen** herunterladbar bleiben, auch nachdem der Container weg ist.
