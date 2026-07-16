---
description: "cTrader cBots (C# und Python, beide .NET) im Browser-Monaco-IDE entwickeln, ausführen und backtesten, auf dem offiziellen ghcr.io/spotware/ctrader-console-Image ausführen."
---

# Build & backtest cBots

Entwickeln, führen aus und backtesten Sie cTrader cBots (C# **und** Python, beide .NET) im Browser-Monaco-Editor aus, führen Sie auf dem offiziellen `ghcr.io/spotware/ctrader-console`-Image aus.

## Build

- Die **Builder**-Seite hostet den Monaco-Editor; `CBotBuilder` kompiliert das Projekt mit
  `dotnet build` **in einem Wegwerf-Container** (`AppOptions.BuildImage`, Arbeitsverzeichnis gebunden
  unter `/work`), sodass nicht vertrauenswürdige MSBuild-Ziele den Host nicht erreichen. Der NuGet-Restore wird
  über einen freigegebenen Volume über mehrere Builds hinweg zwischengespeichert. Der Web-Host benötigt Zugriff auf den Docker-Socket.
- C# und Python Starter-Templates leben in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH-Zustandshierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Der Übergang ersetzt die Entität (Änderung der ID),
  die Container-ID wird übernommen.
- `NodeScheduler` wählt den am wenigsten belasteten geeigneten Node; `ContainerDispatcherFactory` leitet weiter
  an einen Remote-Node-HTTP-Agent oder einen lokalen Docker-Dispatcher.
- Completion Poller koordinieren beendete Container (Backtest-Container beenden sich selbst über
  `--exit-on-stop`); Report vorhanden → Abgeschlossen (Store `ReportJson`), Fehlen → Fehler.
- Live-Container-Protokolle werden über SignalR an den Browser gestreamt; Backtest-Equity-Kurven werden aus dem
  Report geparst und grafisch dargestellt.

## Backtest market data is cached per account

Das cTrader Console lädt historische Tick/Bar-Daten in sein `--data-dir` herunter. Dieses Verzeichnis ist ein
**stabiler, persistenter Cache mit Schlüssel des Trading-Kontos** (seine Kontonummer) — gebunden vom
Node-Disk unter dem eigenen Container-Pfad (`/mnt/data`), ein **separater, nicht verschachtelter Mount** vom
Pro-Instanz-Arbeitsverzeichnis. Daher **erspart sich jeder Backtest** auf demselben Konto die bereits heruntergeladenen Daten
statt sie bei jedem Lauf neu herunterzuladen. (Früher befand sich das
Datenverzeichnis im Pro-Instanz-Arbeitsverzeichnis, dessen ID sich bei jedem Lauf ändert, was einen erneuten
Download bei jedem Backtest erzwang.) Das kurzlebige Pro-Instanz-Arbeitsverzeichnis enthält immer noch den Algorithmus, die Parameter, das Passwort
und den Report; der freigegebene Daten-Cache wird in die Backtest-Daten-Nutzung eines Nodes eingerechnet und durch die
Node-Clean-Aktion geleert.

## Backtest settings

Der Dialog **Backtest** macht die vom Benutzer einstellbaren cTrader Console Backtest-Einstellungen verfügbar, sodass Sie nie eine
Befehlszeile anfassen müssen:

- **Symbol / Timeframe** — Der Zeitrahmen ist ein **Dropdown aller cTrader-Perioden** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` und die Renko/Range/Heikin-Perioden), in der
  Canonical-Schreibweise der Konsole, sodass Sie immer eine gültige `--period` auswählen.
- **From / To** — Das Backtest-Fenster (`--start` / `--end`).
- **Data mode** — Einer der drei cTrader-Modi (`--data-mode`): **Tick data** (`tick`, präzise),
  **m1 bars** (`m1`, schnell) oder **Open prices only** (`open`, schnellstens).
- **Starting balance** — Standard ist `10000` (`--balance`). Ein **Balance von 0 hindert keine Trades und lässt
  cTrader einen leeren Report ausgeben, den es dann abstürzen lässt** ("Message expected"), daher wird immer
  ein Nicht-Null-Saldo gesendet.
- **Commission** und **Spread** — `--commission` / `--spread` (Spread in Pips).

Das Datenverzeichnis (`--data-file` / `--data-dir`) wird von der App selbst verwaltet (ein Pro-Konto-Cache, siehe
oben), nicht im Dialog verfügbar gemacht.

## Instance detail page

Beim Öffnen einer Instanz (`/instance/{id}`) werden der Live-Status, die Protokolle und — für einen Backtest — die Equity-Kurve angezeigt.
Der **Browser-Tab-Titel** widerspiegelt die spezifische Instanz (**cBot-Name · Art · Symbol**, z. B.
`TrendBot · Backtest · EURUSD`), sodass ein Live-Run-Tab und ein Backtest-Tab auf einen Blick
unterscheidbar sind. Ein Run und ein Backtest desselben cBots werden als unterschiedliche **Lineages** verfolgt (eine stabile Lineage-ID, die
über Zustandsübergänge hinweg beibehalten wird), sodass die Seite genau eine Instanz verfolgt und niemals Daten aus einem Run mit
denen eines Backtests mischt.

## Instance lifecycle controls

Jede Instanzzeile (und ihre Detailseite) verfügt über zustandsabhängige Steuerelemente. Eine **aktive** Instanz zeigt
**Stop**; eine **Terminal**-Instanz (Stopped / Completed / Failed) zeigt **Start (▶)**, um sie mit
demselben cBot, Konto, Symbol, Zeitrahmen, Parameter-Set und Image erneut zu starten (ein Run wird als Run neu gestartet, ein
Backtest als Backtest). Beim Klicken auf Stop wird eine "Stopping…"-Mitteilung angezeigt und das Symbol deaktiviert, bis es
sich auflöst, und ein neu erstellter Run wird sofort in der Liste angezeigt — kein Seiten-Neuladen.

Konsolprotokolle werden **persistent gespeichert, wenn eine Instanz beendet wird** — für einen Run (beim Stop) und für einen
**Backtest** (nach Abschluss) gleichermaßen — sodass die Protokolle des letzten Runs auf der Detailseite sichtbar bleiben und
über die Protokoll-Symbolleiste **in die Zwischenablage kopiert** (Symbol "Protokolle kopieren") oder **heruntergeladen** (Symbol "Protokolle herunterladen") werden können, auch nachdem der Container weg ist. Beide werden auf dem vollständigen Konsolprotokoll der Instanz ausgeführt, nicht nur auf dem on-screen-Tail.

Ein hochgeladenes `.algo` wurde hier nie erstellt, daher bleibt die Spalte **Last Build** auf der cBots-Seite
leer (sie zeigt nur eine Build-Zeit für cBots, die Sie im Browser erstellen).

## Edit & re-run a stopped instance

Eine **gestoppte** Instanz (Run oder Backtest) hat ein **Edit**-Steuerelement — ein Symbol in der Liste **und**
neben Start/Stop auf ihrer Detailseite — das einen Dialog öffnet, der **mit ihrer aktuellen Konfiguration vorausgefüllt ist**. Sie
können das **Trading-Konto, Symbol, Zeitrahmen, Parameter-Set und Image-Tag** ändern (und für einen
Backtest das **Fenster und alle Backtest-Einstellungen** oben), dann **Save & start** startet es mit den
neuen Einstellungen erneut (die gestoppte Instanz wird ersetzt). Das Steuerelement ist **während die Instanz aktiv ist deaktiviert** —
nur eine gestoppte Instanz kann bearbeitet werden.

## Run from the code editor

Das Klicken auf **Run** im Code-Editor öffnet einen Dialog, anstatt einen blinden, hartcodierten Run zu starten:

- **Trading account** (erforderlich) — Das cTrader-Konto, das der cBot verbindet.
- **Parameter set** (optional) — Wählen Sie einen vorhandenen Satz aus, oder lassen Sie ihn leer, um mit den cBot-**Standardparameterwerten** ausgeführt zu werden. Ein **+**-Button neben dem Selector erstellt einen neuen Parameter-Set
  inline (siehe unten) und wählt ihn aus.
- **Symbol / Timeframe** werden standardmäßig auf `EURUSD` / `h1` gesetzt und können geändert werden; **Cancel** oder **Run**.

Beim Klicken auf **Run** speichert der Editor die aktuelle Quelle + erstellt, startet die Instanz auf dem gewählten Konto
mit den gewählten Parametern und verfolgt dann die Live-Container-Protokolle. (Der Log-Stream leitet die
Authentifizierungs-Cookie des angemeldeten Benutzers an den SignalR-Hub `/hubs/logs` weiter, sodass er sich verbindet, anstatt
mit `Invalid negotiation response received` zu fehlschlagen.)

## Parameter sets

Ein **Parameter set** ist ein benannter, wiederverwendbarer Satz von cBot-Parameter-Overrides, der als flaches JSON-
Objekt gespeichert ist, das jeden Parameternamen einer Skalarwert zuordnet, z. B. `{"Period": 14, "Label": "trend"}`. Bei
Run-/Backtest-Zeit wird es zur cTrader `params.cbotset`-Datei
(`{ "Parameters": { … } }`). Sie können einen Satz als rohes JSON im Dialog **Parameter
sets** des cBots erstellen/bearbeiten oder inline aus dem Run-Dialog.

Jeder Parameter-Set **gehört zu einem cBot**: Der Dialog "Neuer Parameter-Set" listet alle Ihre cBots auf und Sie
**müssen einen auswählen** — Erstellung wird blockiert, bis ein cBot ausgewählt ist. Der **Name eines Sets ist eindeutig pro cBot**:
Das Erstellen oder Umbenennen eines Sets in einen Namen, den bereits ein anderes Set desselben cBots verwendet, wird abgelehnt (ein klarer
Fehler im Dialog, `409 Conflict` bei der API). Der gleiche Name kann auf einem **anderen** cBot erneut verwendet werden.

Das JSON wird **validiert** beim Speichern: Es muss ein einzelnes flaches Objekt sein, dessen Werte alle Skalare sind
(string / number / bool). Ein nicht-Objekt-Root, ein Array, ein verschachteltes Objekt, ein `null`-Wert oder fehlerhaftes
JSON wird abgelehnt (ein klarer Fehler im Dialog, `400 Bad Request` bei der API). Ein leeres Objekt `{}`
ist erlaubt und bedeutet "keine Overrides".

## cTrader Console CLI notes

Backtests benötigen `--data-mode` (Standard `m1`), Daten als `dd/MM/yyyy HH:mm` und
`params.cbotset` JSON Positionsargument; `run` lehnt `--data-dir` ab (nur Backtest). Siehe
`ContainerCommandHelpers`.

## Nodes & scale

Die Ausführungskapazität skaliert durch das Hinzufügen von Node-Agenten (selbst registrieren + Heartbeat). Siehe
[node discovery](../operations/node-discovery.md) und [scaling](../deployment/scaling.md).

## A trading account is required

Zum Ausführen oder Backtesten eines cBots ist ein cTrader-Trading-Konto erforderlich, das der Bot mit verbinden kann. Bis Sie einen unter
**Trading accounts** hinzufügen, sind die Schaltflächen **Run New cBot** / **Backtest New cBot** deaktiviert (mit einem
Tooltip) und die Seite zeigt eine Eingabeaufforderung an, die zum Account-Setup verlinkt — Sie erhalten keinen rohen
`stream connect failed`-Fehler von einem Bot ohne Konto mehr.
