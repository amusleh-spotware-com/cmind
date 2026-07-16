---
description: "Erstellen, starten und backtesten Sie cTrader cBots (C# und Python, beide .NET) mit dem integrierten Monaco-Editor im Browser, führen Sie diese auf dem offiziellen ghcr.io/spotware/ctrader-console Image aus."
---

# Erstellen und Backtesten von cBots

Erstellen, starten und backtesten Sie cTrader cBots (C# **und** Python, beide .NET) mit dem integrierten Monaco-Editor im Browser. Die Ausführung erfolgt auf dem offiziellen `ghcr.io/spotware/ctrader-console` Image.

## Erstellen

- **Builder**-Seite hostet Monaco-Editor; `CBotBuilder` kompiliert das Projekt mit `dotnet build` **in einem Wegwerf-Container** (`AppOptions.BuildImage`, Arbeitsverzeichnis als Bind-Mount unter `/work`), sodass nicht vertrauenswürdige MSBuild-Ziele den Host nicht erreichen können. NuGet-Wiederherstellung wird über ein gemeinsames Volume über alle Builds hinweg gecacht. Der Web-Host benötigt Zugriff auf die Docker-Sockel.
- C# und Python Starter-Templates befinden sich in `src/Nodes/Builder/Templates/`.

## Starten und Backtesten

- **Instances** = TPH-Zustandshierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Der Übergang ersetzt die Entity (Änderung der id), Container-id wird beibehalten.
- `NodeScheduler` wählt den am wenigsten belasteten qualifizierten Node aus; `ContainerDispatcherFactory` leitet weiter zum HTTP-Agent des Remote-Node oder zum lokalen Docker-Dispatcher.
- Abschluss-Poller stimmen beendete Container ab (Backtest-Container beenden sich selbst über `--exit-on-stop`); Report vorhanden → abgeschlossen (speichert `ReportJson`), fehlend → fehlgeschlagen.
- Live-Container-Logs streamen zum Browser über SignalR; Backtest-Equity-Kurven werden aus Report geparst und dargestellt.

## Backtest-Marktdaten werden pro Konto gecacht

Die cTrader Console lädt historische Tick-/Bar-Daten in sein `--data-dir` herunter. Dieses Verzeichnis ist ein **stabiler, persistenter Cache, der nach dem Handelskonto** (dessen Kontonummer) keyed wird — Bind-Mount vom Node-Datenträger auf seinem eigenen Container-Pfad (`/mnt/data`), ein **separates, nicht verschachteltes Mount** vom ephemeren Pro-Instance-Arbeitsverzeichnis. So wird bei jedem Backtest auf demselben Konto **die bereits heruntergeladenen Daten wiederverwendet**, anstatt sie bei jedem Durchlauf neu herunterzuladen. (Früher befand sich das Datenverzeichnis unter dem Pro-Instance-Arbeitsverzeichnis, dessen id sich bei jedem Durchlauf änderte, was einen neuen Download bei jedem Backtest erzwang.) Das ephemere Pro-Instance-Arbeitsverzeichnis enthält immer noch den Algorithmus, Parameter, Passwort und Report; der gemeinsame Daten-Cache wird in der Backtest-Datennutzung eines Knotens gezählt und durch die Knoten-Bereinigungsaktion gelöscht.

## Backtest-Einstellungen

Der Dialog **Backtest** zeigt die benutzerdefinierbaren cTrader Console Backtest-Einstellungen an, sodass Sie nie eine Befehlszeile anfassen müssen:

- **Symbol / Timeframe** — das Timeframe ist ein **Dropdown aller cTrader-Perioden** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` und die Renko/Range/Heikin-Perioden), in kanonischer Schreibweise der Konsole, sodass Sie immer ein gültiges `--period` auswählen.
- **Von / Bis** — das Backtest-Fenster (`--start` / `--end`).
- **Datenmodus** — einer der drei cTrader-Modi (`--data-mode`): **Tick-Daten** (`tick`, genau), **m1-Balken** (`m1`, schnell) oder **Nur Open-Preise** (`open`, am schnellsten).
- **Anfangsbilanz** — standardmäßig `10000` (`--balance`). Ein **0-Saldo platziert keine Trades und lässt cTrader einen leeren Report ausgeben, auf dem es dann abstürzt** ("Message expected"), daher wird immer ein Saldo ungleich Null gesendet.
- **Provision** — `--commission`.
- **Spread** — `--spread`, ein **numerisches Feld in Pips, das nicht unter 0 gehen kann**. Es ist **im Tick-Datenmodus ausgeblendet**, wobei cTrader den Spread aus den Tick-Daten selbst ableitet (kein `--spread` wird gesendet).

Das Datenverzeichnis (`--data-file` / `--data-dir`) wird von der App selbst verwaltet (ein Pro-Konto-Cache, siehe oben), nicht im Dialog angezeigt.

:::note cTrader stürzt bei einem leeren Backtest ab
Wenn ein Backtest **keine Ergebnisse** erzeugt — keine Trades oder keine Marktdaten für die gewählten Daten/Symbol — wirft der Report-Writer der cTrader Console selbst `Message expected` aus und beendet sich ohne Report. Die App kann diesen Upstream-Bug nicht beheben, aber sie erkennt ihn und markiert die Instance als **Fehlgeschlagen** mit einem aussagekräftigen Grund ("keine Backtest-Ergebnisse für den ausgewählten Bereich…"), anstatt einer einfachen Stack Trace. Wählen Sie einen breiteren Datumsbereich mit verfügbaren Marktdaten und versuchen Sie es erneut.
:::

## Detailseite der Instance

Das Öffnen einer Instance (`/instance/{id}`) zeigt ihren Live-Status, Protokolle und — für einen Backtest — die Equity-Kurve an. Der **Browser-Tabname** spiegelt die spezifische Instance (**cBot-Name · Art · Symbol**, z. B. `TrendBot · Backtest · EURUSD`) wider, sodass ein Live-Run-Tab und ein Backtest-Tab auf einen Blick unterscheidbar sind. Ein Run und ein Backtest desselben cBot werden als unterschiedliche **Linien** (eine stabile Linien-id über Zustandsübergänge hinweg) nachverfolgt, sodass die Seite genau eine Instance verfolgt und nie Run-Daten mit Backtest-Daten mischt.

## Steuerungen des Instance-Lebenszyklus

Jede Instance-Zeile (und ihre Detailseite) hat zustandskorrekte Steuerungen. Eine **aktive** Instance zeigt **Stopp**; eine **terminale** (Gestoppt / Abgeschlossen / Fehlgeschlagen) zeigt **Start (▶)**, um sie mit demselben cBot, Konto, Symbol, Timeframe, ParamSet und Image erneut zu starten (ein Run wird als Run neu gestartet, ein Backtest als Backtest). Das Klicken auf Stop zeigt eine "Stopping…"-Mitteilung an und deaktiviert das Symbol, bis es sich auflöst, und ein neu erstellter Run wird sofort in der Liste angezeigt — keine Seitenneuladeung.

Konsolenprotokolle werden **beibehalten, wenn eine Instance beendet wird** — für einen Run (beim Stopp) und für einen **Backtest** (beim Abschluss) gleichermaßen — sodass die Protokolle des letzten Runs auf der Detailseite angezeigt bleiben und über die Protokoll-Symbolleiste **in die Zwischenablage kopiert** (Symbol "Protokolle kopieren") oder **heruntergeladen** (Symbol "Protokolle herunterladen") werden können, selbst nachdem der Container weg ist. Beide wirken auf das vollständige Konsolenprotokoll der Instance, nicht nur auf die angezeigten Endergebnisse.

Ein **abgeschlossener Backtest** behält seinen **cTrader Report** auch in beiden Formaten bei — den uncodierten **JSON** (denselben, den die Equity-Kurve und die KI-Analyse lesen) und den vollständigen **HTML**-Report. Beide sind von der Backtest-Zeile **und** der Detailseite über spezielle Symbole herunterladbar. Nur die **letzten Runs** Reports werden behalten, und die Symbole sind **deaktiviert** für alle Backtests, die nicht gestartet, ausgeführt oder fehlgeschlagen sind (und werden für einen Run-Instance nie angezeigt) — nur ein abgeschlossener Backtest hat einen herunterlad baren Report.

Ein **hochgeladenes** `.algo` wurde hier nie gebaut, daher ist die Spalte **Letzter Build** auf der cBots-Seite leer gelassen (sie zeigt eine Buildzeit nur für cBots, die Sie im Browser erstellen).

## Bearbeiten und Neustart einer gestoppten Instance

Eine **gestoppte** Instance (Run oder Backtest) hat ein **Bearbeiten**-Steuerung — ein Symbol in ihrer Zeile in der Liste **und** neben Start/Stop auf ihrer Detailseite — das einen Dialog öffnet, der mit ihrer aktuellen Konfiguration **vorgefüllt** ist. Sie können das **Handelskonto, Symbol, Timeframe, ParamSet und Image-Tag** ändern (und für einen Backtest auch das **Fenster und alle Backtest-Einstellungen** oben), dann **Speichern und Starten** startet es mit den neuen Einstellungen neu (ersetzen der gestoppten Instance). Das Steuerung ist **deaktiviert, während die Instance aktiv ist** — nur eine gestoppte Instance kann bearbeitet werden.

## Ausführung vom Code-Editor aus

Das Klicken auf **Run** im Code-Editor öffnet einen Dialog, anstatt einen blinden, hartcodierten Run auszulösen:

- **Handelskonto** (erforderlich) — das cTrader-Konto, mit dem sich der cBot verbindet.
- **ParamSet** (optional) — wählen Sie einen vorhandenen Satz aus, oder lassen Sie ihn leer, um mit den **Standard-Parameterwerten** des cBot auszuführen. Ein **+**-Button neben dem Selector erstellt einen neuen ParamSet inline (siehe unten) und wählt ihn aus.
- **Symbol / Timeframe** standardmäßig auf `EURUSD` / `h1` und können geändert werden; **Abbrechen** oder **Ausführen**.

Beim **Ausführen** speichert der Editor + erstellt die aktuelle Quelle, startet die Instance auf dem gewählten Konto mit den gewählten Parametern und verfolgt dann die Live-Container-Protokolle. (Der Log-Stream leitet den Auth-Cookie des angemeldeten Benutzers an den SignalR Hub `/hubs/logs` weiter, sodass er sich verbindet, anstatt mit `Invalid negotiation response received` zu scheitern.)

## Parameter Sets

Ein **ParamSet** ist ein benannter, wiederverwendbarer Satz von cBot-Parameterüberschreibungen, der als flaches JSON-Objekt gespeichert ist, das jeden Parameternamen einem Skalarwert zuordnet, z. B. `{"Period": 14, "Label": "trend"}`. Zur Ausführungs- oder Backtest-Zeit wird dies in die cTrader `params.cbotset` Datei umgewandelt (`{ "Parameters": { … } }`). Sie können einen Satz als uncodierten JSON aus dem Dialog **ParamSets** des cBot oder inline aus dem Run-Dialog erstellen/bearbeiten.

Jeder ParamSet **gehört zu einem cBot**: Der Dialog Neuer ParamSet listet alle Ihre cBots auf, und Sie **müssen einen auswählen** — die Erstellung wird blockiert, bis ein cBot ausgewählt ist. Der **Name eines Satzes ist eindeutig pro cBot**: Das Erstellen oder Umbenennen eines Satzes in einen Namen, den ein anderer Satz desselben cBot bereits verwendet, wird abgelehnt (ein klarer Fehler im Dialog, `409 Conflict` bei der API). Derselbe Name kann auf einem **anderen** cBot wiederverwendet werden.

Das JSON ist **bei Speicherung validiert**: Es muss ein einzelnes flaches Objekt sein, dessen Werte alle Skalare sind (String / Nummer / Bool). Eine nicht-Objekt-Wurzel, ein Array, ein verschachteltes Objekt, ein `null` Wert oder fehlerhaftes JSON wird abgelehnt (ein klarer Fehler im Dialog, `400 Bad Request` bei der API). Ein leeres Objekt `{}` ist zulässig und bedeutet "keine Überschreibungen".

## cTrader Console CLI-Hinweise

Backtests benötigen `--data-mode` (Standard `m1`), Daten als `dd/MM/yyyy HH:mm` und `params.cbotset` JSON-Positionsargument; `run` lehnt `--data-dir` ab (nur Backtest). Siehe `ContainerCommandHelpers`.

## Nodes und Skalierung

Die Ausführungskapazität wird durch das Hinzufügen von Node-Agents skaliert (Selbstregistrierung + Heartbeat). Siehe [Node-Erkennung](../operations/node-discovery.md) und [Skalierung](../deployment/scaling.md).

## Ein Handelskonto ist erforderlich

Das Ausführen oder Backtesten eines cBot erfordert ein cTrader Handelskonto, mit dem sich der cBot verbindet. Bis Sie eines unter **Handelskonten** hinzufügen, sind die Buttons **Neuen cBot ausführen** / **Neuen cBot backtesten** deaktiviert (mit einem Tooltip) und die Seite zeigt eine Aufforderung mit Link zur Kontoeinrichtung an — Sie treffen nicht mehr auf einen rohen Fehler wie `stream connect failed` von einem Bot ohne Konto.
