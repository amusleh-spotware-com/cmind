---
description: "Erstellen, führen aus und backtesten Sie cTrader cBots (C# und Python, beide .NET) im In-Browser-Monaco-Editor und führen Sie diese auf dem offiziellen ghcr.io/spotware/ctrader-console-Image aus."
---

# Build & Backtest von cBots

Erstellen, führen aus und backtesten Sie cTrader cBots (C# **und** Python, beide .NET) im In-Browser-Monaco-Editor und führen Sie diese auf dem offiziellen `ghcr.io/spotware/ctrader-console`-Image aus.

## Build

- Die **Builder**-Seite hostet den Monaco-Editor; `CBotBuilder` kompiliert das Projekt mit `dotnet build` **in einem Einweg-Container** (`AppOptions.BuildImage`, Workdir als Bind-Mount bei `/work`), sodass nicht vertrauenswürdige Benutzer-MSBuild-Targets den Host nicht erreichen. NuGet-Wiederherstellung wird über gemeinsame Volumes hinweg gecacht. Der Web-Host benötigt Docker-Socket-Zugriff.
- C#- und Python-Starter-Templates befinden sich in `src/Nodes/Builder/Templates/`.

## Ausführen & Backtest

- **Instanzen** = TPH-Statushierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Zustandsübergang ersetzt die Entität (ID-Änderung), Container-ID wird übertragen.
- `NodeScheduler` wählt den am wenigsten belasteten berechtigten Node; `ContainerDispatcherFactory` leitet an einen Remote-Node-HTTP-Agent oder lokalen Docker-Dispatcher weiter.
- Completion-Poller gleichen beendete Container ab (Backtest-Container beenden sich selbst über `--exit-on-stop`); Bericht vorhanden → abgeschlossen (speichert `ReportJson`), fehlend → fehlgeschlagen.
- Live-Container-Logs werden über SignalR an den Browser gestreamt; Backtest-Eigenkapitalkurven werden aus dem Bericht analysiert und grafisch dargestellt.

## Backtest-Marktdaten werden pro Konto gecacht

Die cTrader Console lädt historische Tick-/Bar-Daten in ihr `--data-dir` herunter. Dieses Verzeichnis ist ein **stabiler, persistenter Cache, der nach dem Handelskonto** (seiner Kontonummer) schlüsselt – bind-mounted von der Festplatte des Nodes in seinen eigenen Container-Pfad (`/mnt/data`), ein **separates, nicht verschachteltes Mount** vom pro-Instanz-Workdir. Somit wird bei jedem Backtest auf demselben Konto **bereits heruntergeladene Daten wiederverwendet**, anstatt sie bei jedem Lauf erneut herunterzuladen. (Früher befand sich das Datenverzeichnis unter dem Workdir pro Instanz, dessen ID sich bei jedem Lauf änderte, was einen erneuten Download bei jedem Backtest erzwang.) Das kurzlebige Workdir pro Instanz enthält immer noch den Algorithmus, Parameter, Passwort und Bericht; der gemeinsame Daten-Cache wird bei der Backtest-Datennutzung eines Nodes gezählt und durch die Node-Bereinigungsaktion gelöscht.

## Backtest-Einstellungen

Der **Backtest**-Dialog stellt jede Einstellung zur Verfügung, die die cTrader Console Backtest-CLI akzeptiert, sodass Sie nie eine Befehlszeile berühren müssen:

- **Von / Bis** – das Backtest-Fenster (`--start` / `--end`).
- **Datenmodus** – `m1` (1-Minuten-Balken) oder `tick` (`--data-mode`).
- **Anfangsguthaben** – Standard `10000` (`--balance`). Ein **Guthaben von 0 führt zu keinen Trades und veranlasst cTrader, einen leeren Bericht auszugeben, der dann abstürzt** („Message expected"), daher wird immer ein Guthaben ungleich Null gesendet.
- **Provision** und **Spread** (`--commission` / `--spread`, Spread in Pips).
- **Erweiterte Optionen** – ein freies Textfeld `Name=Wert` pro Zeile für alle anderen Backtest-Optionen, die cTrader unterstützt (z. B. `applyCommissionAutomatically=true`); jede Zeile wird zu einem `--Name Wert` CLI-Argument.

## Detailseite der Instanz

Wenn Sie eine Instanz öffnen (`/instance/{id}`), werden der Live-Status, Logs und – für einen Backtest – die Eigenkapitalkurve angezeigt. Der **Browser-Tab-Titel** spiegelt die spezifische Instanz wider (**cBot-Name · Typ · Symbol**, z. B. `TrendBot · Backtest · EURUSD`), sodass ein Live-Run-Tab und ein Backtest-Tab auf einen Blick unterscheidbar sind. Ein Run und ein Backtest desselben cBots werden als separate **Abstammungen** verfolgt (eine stabile Abstammungs-ID, die über Zustandsübergänge hinweg erhalten bleibt), daher folgt die Seite genau einer Instanz und vermischt nie die Daten eines Runs mit denen eines Backtests.

## Lebenszyklussteuerelemente der Instanz

Jede Instanzzeile (und ihre Detailseite) hat zustandskorrekte Steuerelemente. Eine **aktive** Instanz zeigt **Stop**; eine **terminale** (Gestoppt / Abgeschlossen / Fehlgeschlagen) zeigt **Start (▶)**, um sie mit demselben cBot, Konto, Symbol, Timeframe, Parametersatz und Image erneut zu starten (ein Run wird als Run neu gestartet, ein Backtest als Backtest). Durch Klicken auf Stop wird eine „Stopping…"-Meldung angezeigt und das Symbol deaktiviert, bis es aufgelöst ist; ein neu erstellter Run wird sofort in der Liste angezeigt – ohne Seite neu zu laden.

Console-Logs werden **beibehalten, wenn eine Instanz endet** – bei einem Run (beim Stopp) und bei einem **Backtest** (nach Abschluss) – sodass die Logs des letzten Runs auf der Detailseite sichtbar bleiben und über die Log-Symbolleiste **in die Zwischenablage kopiert** werden (Symbol „Logs kopieren") oder **heruntergeladen** werden (Symbol „Logs herunterladen"), auch nachdem der Container weg ist. Beide reagieren auf das gesamte Konsolenprotokoll der Instanz, nicht nur auf den sichtbaren Tail.

Ein **hochgeladenes** `.algo` wurde hier nie erstellt, daher ist die Spalte **Letzter Build** auf der cBots-Seite leer (sie zeigt eine Build-Zeit nur für cBots, die Sie im Browser erstellen).

## Bearbeiten und erneutes Ausführen einer gestoppten Instanz

Eine **gestoppte** Instanz (Run oder Backtest) hat ein **Bearbeitungs**-Steuerelement – ein Symbol in ihrer Zeile in der Liste **und** neben Start/Stop auf ihrer Detailseite – das einen mit ihrer aktuellen Konfiguration **vorausgefüllten** Dialog öffnet. Sie können das **Handelskonto, Symbol, Timeframe, Parametersatz und Image-Tag** ändern (und für einen Backtest das **Fenster und alle oben genannten Backtest-Einstellungen**), dann startet **Speichern & Ausführen** ihn mit den neuen Einstellungen erneut (ersetzt die gestoppte Instanz). Das Steuerelement ist **deaktiviert, während die Instanz aktiv ist** – nur eine gestoppte Instanz kann bearbeitet werden.

## Ausführen aus dem Code-Editor

Wenn Sie auf **Run** im Code-Editor klicken, wird ein Dialog geöffnet, anstatt einen blinden, hartcodierten Run zu starten:

- **Handelskonto** (erforderlich) – das cTrader-Konto, mit dem sich der cBot verbindet.
- **Parametersatz** (optional) – wählen Sie einen vorhandenen Satz aus, oder lassen Sie ihn leer, um mit den **Standardparameterwerten** des cBots auszuführen. Ein **+** Button neben dem Selektor erstellt einen neuen Parametersatz inline (siehe unten) und wählt ihn aus.
- **Symbol / Timeframe** Standard `EURUSD` / `h1` und können geändert werden; **Abbrechen** oder **Ausführen**.

Beim **Ausführen** speichert der Editor den aktuellen Quellcode + erstellt, startet die Instanz auf dem gewählten Konto mit den gewählten Parametern und verfolgt dann die Live-Container-Logs. (Der Log-Stream leitet das Auth-Cookie des angemeldeten Benutzers an den SignalR-Hub `/hubs/logs` weiter, damit er sich verbindet, anstatt mit `Invalid negotiation response received` zu scheitern.)

## Parametersätze

Ein **Parametersatz** ist ein benannter, wiederverwendbarer Satz von cBot-Parameterüberschreibungen, gespeichert als flaches JSON-Objekt, das jeden Parameternamen auf einen Skalarwert abbildet, z. B. `{"Period": 14, "Label": "trend"}`. Bei der Ausführungs-/Backtestzeit wird es in die cTrader `params.cbotset`-Datei konvertiert (`{ "Parameters": { … } }`). Sie können einen Satz als reines JSON aus dem **Parametersätze**-Dialog des cBots oder inline aus dem Run-Dialog erstellen/bearbeiten.

Jeder Parametersatz **gehört zu einem cBot**: Der Dialog „Neuer Parametersatz" listet alle Ihre cBots auf und Sie **müssen einen auswählen** – die Erstellung wird blockiert, bis ein cBot ausgewählt ist. Der **Name eines Satzes ist pro cBot eindeutig**: Das Erstellen oder Umbenennen eines Satzes in einen Namen, den bereits ein anderer Satz desselben cBots verwendet, wird abgelehnt (ein klarer Fehler im Dialog, `409 Conflict` bei der API). Derselbe Name kann auf einem **anderen** cBot wiederverwendet werden.

Das JSON wird **beim Speichern validiert**: Es muss ein einzelnes flaches Objekt sein, dessen Werte alle Skalare (String / Zahl / Bool) sind. Ein Nicht-Objekt-Root, ein Array, ein verschachteltes Objekt, ein `null`-Wert oder fehlerhaftes JSON wird abgelehnt (ein klarer Fehler im Dialog, `400 Bad Request` bei der API). Ein leeres Objekt `{}` ist zulässig und bedeutet „keine Überschreibungen".

## Hinweise zur cTrader Console-CLI

Backtests benötigen `--data-mode` (Standard `m1`), Datumsangaben als `dd/MM/yyyy HH:mm` und `params.cbotset` als JSON-Positionsargument; `run` lehnt `--data-dir` ab (nur für Backtests). Siehe `ContainerCommandHelpers`.

## Nodes & Skalierung

Die Ausführungskapazität wird durch Hinzufügen von Node-Agenten erhöht (Self-Registration + Heartbeat). Siehe [Node-Erkennung](../operations/node-discovery.md) und [Skalierung](../deployment/scaling.md).

## Ein Handelskonto ist erforderlich

Das Ausführen oder Backtesten eines cBots erfordert ein cTrader-Handelskonto zum Verbinden. Bis Sie ein Konto unter **Handelskonten** hinzufügen, sind die Schaltflächen **Run New cBot** / **Backtest New cBot** deaktiviert (mit einem Tooltip) und die Seite zeigt einen Hinweis mit einem Link zur Kontoeinrichtung – Sie erhalten keinen Rohfehler `stream connect failed` von einem Bot ohne Konto.
