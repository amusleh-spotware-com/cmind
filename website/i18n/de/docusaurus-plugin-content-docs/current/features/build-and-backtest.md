---
description: "cBots erstellen und backtesten (C# und Python, beide .NET) im In-Browser-Monaco-Editor, ausgeführt auf dem offiziellen ghcr.io/spotware/ctrader-console Image."
---

# Build & backtest cBots

Erstellen und backtesten Sie cTrader cBots (C# **und** Python, beide .NET) im In-Browser-Monaco-Editor und führen Sie sie auf dem offiziellen `ghcr.io/spotware/ctrader-console` Image aus.

## Build

- **Builder** Seite hostet den Monaco-Editor; `CBotBuilder` kompiliert das Projekt mit `dotnet build` **in einem wegwerfbaren Container** (`AppOptions.BuildImage`, Arbeitsverzeichnis mit Bind-Mount auf `/work`), damit nicht vertrauenswürdige MSBuild-Ziele den Host nicht erreichen können. Die NuGet-Wiederherstellung wird über ein gemeinsames Volume über alle Builds hinweg zwischengespeichert. Der Web-Host benötigt Zugriff auf die Docker-Socket.
- C# und Python Starter-Templates befinden sich in `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH-Zustandshierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Übergänge ersetzen die Entity (ID-Wechsel), Container-ID wird mitgeführt.
- `NodeScheduler` wählt den am wenigsten belasteten anspruchsvollen Node; `ContainerDispatcherFactory` leitet an den HTTP-Agent des Remote-Nodes oder den lokalen Docker-Dispatcher weiter.
- Completion Poller gleichen beendete Container ab (Backtest-Container werden automatisch über `--exit-on-stop` beendet); Report vorhanden → abgeschlossen (speichert `ReportJson`), fehlend → fehlgeschlagen.
- Live-Container-Logs werden über SignalR an den Browser gestreamt; Backtest-Equity-Kurven werden aus dem Report analysiert und grafisch dargestellt.

## Backtest market data is cached per account

Das cTrader Console lädt historische Tick/Bar-Daten in sein `--data-dir` Verzeichnis herunter. Dieses Verzeichnis ist ein **stabiler, persistenter Cache, der nach dem Trading-Konto** (seiner Kontonummer) indiziert ist — mit Bind-Mount vom Datenträger des Nodes unter seinem eigenen Container-Pfad (`/mnt/data`), einem **separaten, nicht verschachtelten Mount** vom Arbeitsverzeichnis pro Instanz. So werden bei jedem Backtest auf demselben Konto die bereits heruntergeladenen Daten **wiederverwendet**, anstatt sie bei jedem Run erneut herunterzuladen. (Früher befand sich das Datenverzeichnis im Arbeitsverzeichnis pro Instanz, dessen ID sich bei jedem Run ändert, was einen neuen Download bei jedem Backtest erzwang.) Das ephemere Arbeitsverzeichnis pro Instanz enthält weiterhin den Algorithmus, Parameter, das Passwort und den Report; der gemeinsame Daten-Cache wird in der Backtest-Datennutzung eines Nodes gezählt und durch die Node-Clean-Aktion gelöscht.

## Backtest settings

Der **Backtest** Dialog zeigt jede Einstellung, die die cTrader Console Backtest CLI akzeptiert, sodass Sie niemals eine Befehlszeile berühren müssen:

- **From / To** — das Backtest-Fenster (`--start` / `--end`).
- **Data mode** — einer der drei cTrader-Modi (`--data-mode`): **Tick data** (`tick`, präzise), **m1 bars** (`m1`, schnell) oder **Open prices only** (`open`, schnellste).
- **Starting balance** — Standardwert `10000` (`--balance`). Ein **0-Guthaben führt keine Trades durch und bewirkt, dass cTrader einen leeren Report ausgibt, auf dem es abstürzt** ("Message expected"), daher wird immer ein Guthaben ungleich Null gesendet.
- **Commission** und **Spread** — `--commission` / `--spread` (Spread in Pips).
- **Data file** (optional) — ein Node-seitiger Pfad zu einer Historische-Daten-Datei (`--data-file`); lassen Sie das Feld leer, um die heruntergeladenen/zwischengespeicherten Daten zu verwenden.
- **Expose environment variables** — ein Umschalter, der die Host-Umgebungsvariablen an den cBot übergibt (das `--environment-variables` Flag).

## Instance detail page

Das Öffnen einer Instanz (`/instance/{id}`) zeigt ihren Live-Status, Logs und — für einen Backtest — die Equity-Kurve. Der **Browser-Tab-Titel** widerspiegelt die spezifische Instanz (**cBot-Name · Typ · Symbol**, z. B. `TrendBot · Backtest · EURUSD`), sodass ein Live-Run-Tab und ein Backtest-Tab auf einen Blick unterscheidbar sind. Ein Run und ein Backtest desselben cBot werden als unterschiedliche **Linienräume** verfolgt (eine stabile Lineage-ID, die über Zustandsübergänge erhalten bleibt), sodass die Seite genau eine Instanz verfolgt und niemals Run-Daten mit Backtest-Daten mischt.

## Instance lifecycle controls

Jede Instanzzeile (und ihre Detailseite) verfügt über zustandskorrekte Steuerelemente. Eine **aktive** Instanz zeigt **Stop**; eine **terminale** (Stopped / Completed / Failed) zeigt **Start (▶)**, um sie erneut mit demselben cBot, Konto, Symbol, Timeframe, ParamSet und Image zu starten (ein Run wird als Run neu gestartet, ein Backtest als Backtest). Wenn Sie auf Stop klicken, wird eine Mitteilung "Stopping…" angezeigt und das Symbol wird deaktiviert, bis es sich auflöst. Ein neu erstellter Run wird sofort in der Liste angezeigt — ohne Seitenneuladen.

Console-Logs werden **beim Beenden einer Instanz beibehalten** — für einen Run (beim Stop) und für einen **Backtest** (beim Abschluss) — sodass die Logs des letzten Runs auf der Detailseite verbleiben und über die Log-Symbolleiste **in die Zwischenablage kopiert** (Symbol "Copy logs") oder **heruntergeladen** (Symbol "Download logs") werden können, auch nachdem der Container verschwunden ist. Beide wirken sich auf das vollständige Konsolenlog der Instanz aus, nicht nur auf den auf dem Bildschirm sichtbaren Kauderwelsch.

Ein hochgeladenes `.algo` wurde hier nie erstellt, daher ist die Spalte **Last Build** auf der cBots-Seite leer (es zeigt eine Build-Zeit nur für cBots, die Sie im Browser erstellen).

## Edit & re-run a stopped instance

Eine **gestoppte** Instanz (Run oder Backtest) hat ein **Edit** Steuerelement — ein Symbol auf ihrer Zeile in der Liste **und** neben Start/Stop auf ihrer Detailseite — das einen Dialog öffnet, der **mit der aktuellen Konfiguration gefüllt ist**. Sie können das **Trading-Konto, Symbol, Timeframe, ParamSet und Image-Tag** ändern (und für einen Backtest auch das **Fenster und alle obigen Backtest-Einstellungen**), dann wird **Save & start** es mit den neuen Einstellungen neu starten (ersetzt die gestoppte Instanz). Das Steuerelement ist **deaktiviert, während die Instanz aktiv ist** — nur eine gestoppte Instanz kann bearbeitet werden.

## Run from the code editor

Wenn Sie im Code-Editor auf **Run** klicken, wird ein Dialog geöffnet, anstatt einen blinden, hartcodierten Run zu starten:

- **Trading account** (erforderlich) — das cTrader-Konto, mit dem sich der cBot verbindet.
- **Parameter set** (optional) — wählen Sie einen vorhandenen Satz aus, oder lassen Sie das Feld leer, um mit den **Standardparameterwerten** des cBot zu laufen. Eine **+** Schaltfläche neben dem Selector erstellt einen neuen ParamSet inline (siehe unten) und wählt ihn aus.
- **Symbol / Timeframe** nehmen standardmäßig den Wert `EURUSD` / `h1` an und können geändert werden; **Cancel** oder **Run**.

Bei **Run** speichert der Editor die aktuelle Quelle + erstellt sie, startet die Instanz auf dem ausgewählten Konto mit den ausgewählten Parametern und tailed dann die Live-Container-Logs. (Der Log-Stream leitet das Auth-Cookie des angemeldeten Benutzers an den `/hubs/logs` SignalR Hub weiter, sodass die Verbindung hergestellt wird, anstatt mit `Invalid negotiation response received` fehlzuschlagen.)

## Parameter sets

Ein **Parameter set** ist ein benannter, wiederverwendbarer Satz von cBot-Parameterüberschreibungen, der als flaches JSON-Objekt gespeichert wird, das jeden Parameternamen einem Skalwert zuordnet, z. B. `{"Period": 14, "Label": "trend"}`. Bei der Run/Backtest-Zeit wird es in die cTrader `params.cbotset` Datei umgewandelt (`{ "Parameters": { … } }`). Sie können einen Satz als rohes JSON aus dem **Parameter sets** Dialog des cBot oder inline aus dem Run-Dialog erstellen/bearbeiten.

Jeder Parameter Set **gehört zu einem cBot**: Der Dialog für den neuen Parameter Set listet alle Ihre cBots auf und Sie **müssen einen auswählen** — die Erstellung wird blockiert, bis ein cBot ausgewählt ist. Der **Name eines Sets ist pro cBot eindeutig**: Das Erstellen oder Umbenennen eines Sets zu einem Namen, den ein anderer Set desselben cBot bereits verwendet, wird abgelehnt (ein klarer Fehler im Dialog, `409 Conflict` im API). Derselbe Name kann auf einem **anderen** cBot wiederverwendet werden.

Das JSON wird **beim Speichern validiert**: Es muss ein einzelnes flaches Objekt sein, dessen Werte alle Skalare sind (string / number / bool). Eine nicht-Objekt-Wurzel, ein Array, ein verschachteltes Objekt, ein `null` Wert oder malformed JSON wird abgelehnt (ein klarer Fehler im Dialog, `400 Bad Request` im API). Ein leeres Objekt `{}` ist zulässig und bedeutet "keine Überschreibungen".

## cTrader Console CLI notes

Backtests benötigen `--data-mode` (Standardwert `m1`), Datumsangaben als `dd/MM/yyyy HH:mm` und `params.cbotset` JSON Positionsargument; `run` lehnt `--data-dir` ab (nur Backtest). Siehe `ContainerCommandHelpers`.

## Nodes & scale

Die Ausführungskapazität skaliert durch das Hinzufügen von Node-Agenten (Selbstregistrierung + Heartbeat). Siehe [node discovery](../operations/node-discovery.md) und [scaling](../deployment/scaling.md).

## A trading account is required

Das Ausführen oder Backtesten eines cBot erfordert ein cTrader-Trading-Konto zum Verbinden. Bis Sie einen unter **Trading accounts** hinzufügen, sind die Schaltflächen **Run New cBot** / **Backtest New cBot** deaktiviert (mit einem Tooltip) und die Seite zeigt einen Hinweis, der zur Kontoeinrichtung verlinkt — Sie erhalten keinen rohen `stream connect failed` Fehler von einem Bot ohne Konto mehr.
