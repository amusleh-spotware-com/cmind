---
description: "Erstellen, führen aus und backtesten Sie cTrader cBots (C# und Python, beide .NET) aus dem In-Browser-Monaco-Editor; Ausführung auf dem offiziellen Image ghcr.io/spotware/ctrader-console."
---

# Build & Backtest von cBots

Erstellen, führen aus und backtesten Sie cTrader cBots (C# **und** Python, beide .NET) aus dem In-Browser-Monaco-Editor; Ausführung auf dem offiziellen `ghcr.io/spotware/ctrader-console`-Image.

## Build

- **Builder**-Seite hostet Monaco-Editor; `CBotBuilder` kompiliert das Projekt mit `dotnet build` **in einem Wegwerf-Container** (`AppOptions.BuildImage`, Arbeitsverzeichnis bind-mounted unter `/work`), sodass unterstützte Benutzer-MSBuild-Ziele den Host nicht erreichen. NuGet-Wiederherstellung wird über Builds hinweg über ein gemeinsames Volume gecacht. Der Web-Host benötigt Zugriff auf den Docker-Socket.
- C# + Python-Startervorlagen befinden sich unter `src/Nodes/Builder/Templates/`.

## Ausführung & Backtest

- **Instances** = TPH-Zustandshierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Übergänge ersetzen Entität (ID-Änderung),
  Container-ID wird übernommen.
- `NodeScheduler` wählt den am wenigsten belasteten geeigneten Node aus; `ContainerDispatcherFactory` leitet weiter an
  Remote-Node-HTTP-Agent oder lokalen Docker-Dispatcher.
- Completion-Poller stimmen ausgezogene Container ab (Backtest-Container beenden sich selbst via
  `--exit-on-stop`); Report vorhanden → completed (speichert `ReportJson`), fehlend → failed.
- Live-Container-Logs streamen zum Browser über SignalR; Backtest-Equity-Kurven werden aus
  Report geparst + diagrammiert.

## Backtest-Marktdaten werden pro Konto gecacht

Die cTrader Console lädt historische Tick-/Bar-Daten in ihr `--data-dir` herunter. Dieses Verzeichnis ist ein
**stabiler, persistenter Cache, der am Handels-Konto** (seiner Kontonummer) angeordnet ist — bind-mounted von der
Node-Festplatte unter seinem eigenen Container-Pfad (`/mnt/data`), ein **separates, nicht verschachteltes Mount** vom
Arbeitsverzeichnis pro Instance. Daher wird bei jedem Backtest auf demselben Konto **wiederverwendet** die bereits heruntergeladenen Daten
anstatt sie bei jedem Durchlauf erneut herunterzuladen. (Zuvor befand sich das
Datenverzeichnis unter dem Arbeitsverzeichnis pro Instance, dessen ID sich bei jedem Durchlauf ändert, was einen frischen
Download bei jedem Backtest erzwang.) Das ephemere Arbeitsverzeichnis pro Instance hält immer noch den Algo, Parameter, Passwort
und Report; der gemeinsame Daten-Cache wird in der Backtest-Datennutzung eines Node gezählt und durch die
Node-Clean-Aktion gelöscht.

## Backtest-Einstellungen

Der **Backtest**-Dialog legt die vom Benutzer abstimmbaren cTrader-Console-Backtest-Einstellungen offen, sodass Sie nie einen
Befehlszeile anfassen müssen:

- **Symbol / Timeframe** — der Timeframe ist ein **Dropdown von jedem cTrader-Zeitraum** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, und die Renko-/Range-/Heikin-Perioden), in der
  kanonischen Schreibweise der Konsole, sodass Sie immer ein gültiges `--period` wählen.
- **From / To** — das Backtest-Fenster (`--start` / `--end`).
- **Data mode** — einer der drei cTrader-Modi (`--data-mode`): **Tick-Daten** (`tick`, genau),
  **m1-Balken** (`m1`, schnell), oder **Nur Eröffnungspreise** (`open`, am schnellsten).
- **Starting balance** — standardmäßig `10000` (`--balance`). Ein **0-Saldo führt zu keinen Trades und lässt
  cTrader einen leeren Report ausgeben, auf dem es dann abstürzt** ("Message expected"), daher wird immer ein Nicht-Null-Saldo gesendet.
- **Commission** — `--commission`.
- **Spread** — `--spread`, ein **numerisches Feld in Pips, das nicht unter 0 gehen kann**. Es ist **im Tick-
  Daten-Modus ausgeblendet**, wo cTrader den Spread selbst aus den Tick-Daten ableitet (kein `--spread` wird gesendet).

Das Datenverzeichnis (`--data-file` / `--data-dir`) wird von der App selbst verwaltet (ein Pro-Konto-Cache, siehe
oben), nicht im Dialog offengelegt.

:::note cTrader stürzt bei einem leeren Backtest ab
Wenn ein Backtest **keine Ergebnisse** produziert — keine Trades oder keine Marktdaten für die gewählten Daten/Symbole —
wirft die Report-Schreibweise von cTrader Console `Message expected` und beendet sich ohne Report. Die App kann diesen
Upstream-Bug nicht beheben, erkennt ihn aber und markiert die Instance als **Failed** mit einem umsetzbaren Grund
("no backtest results for the selected range…") anstelle einer rohen Stack-Spur. Wählen Sie einen breiteren Datumsbereich
mit verfügbaren Marktdaten und versuchen Sie es erneut.
:::

## Instance-Detailseite

Beim Öffnen einer Instance (`/instance/{id}`) werden deren Live-Status, Logs und — bei einem Backtest — die Equity-
Kurve angezeigt. Der **Browser-Tab-Titel** spiegelt die spezifische Instance wider (**cBot-Name · Art · Symbol**, z. B.
`TrendBot · Backtest · EURUSD`), sodass ein Live-Run-Tab und ein Backtest-Tab auf einen Blick unterscheidbar sind.
Ein Durchlauf und ein Backtest desselben cBot werden als unterschiedliche **Lineages** verfolgt (eine stabile Lineage-ID, die
über Zustandsübergänge hinweg übernommen wird), sodass die Seite genau eine Instance folgt und nie einen Lauf mit einem
Backtest vermischt.

## Instance-Lebenszyklussteuerungen

Jede Instance-Reihe (und ihre Detailseite) hat zustandskorrekte Steuerungen. Eine **aktive** Instance zeigt
**Stop**; eine **terminale** (Stopped / Completed / Failed) zeigt **Start (▶)**, um sie mit
demselben cBot, Konto, Symbol, Timeframe, ParamSet und Image erneut zu starten (ein Durchlauf wird als Durchlauf neu gestartet, ein
Backtest als Backtest). Das Klicken auf Stop zeigt eine „Stopping…"-Benachrichtigung und deaktiviert das Symbol, bis es
sich auflöst, und ein neu erstellter Durchlauf erscheint sofort in der Liste — kein Seiten-Reload.

Konsolenprotokolle werden **beim Beenden einer Instance persistiert** — für einen Durchlauf (bei Stop) und für einen
**Backtest** (bei Abschluss) gleichermaßen — sodass die Protokolle des letzten Durchlaufs auf der Detailseite angezeigt bleiben und,
über die Log-Symbolleiste, **in die Zwischenablage kopiert** werden (Symbol zum Kopieren von Logs) oder **heruntergeladen** werden (Symbol zum Herunterladen von Logs)
sogar nach dem Container ist weg. Beide verarbeiten die vollständige Konsolenprotokoll der Instance, nicht nur das
auf dem Bildschirm angezeigte Ende.

Eine **hochgeladene** `.algo` wurde niemals hier erstellt, daher ist ihre **Letzter Build**-Spalte auf der cBots-Seite
leer gelassen (sie zeigt nur eine Build-Zeit für cBots, die Sie im Browser erstellen).

## Bearbeitungs- und Neuausführung einer gestoppten Instance

Eine **gestoppte** Instance (Durchlauf oder Backtest) hat eine **Edit**-Steuerung — ein Symbol in ihrer Reihe in der Liste **und**
neben Start/Stop auf ihrer Detailseite — das einen Dialog öffnet, der **mit ihrer aktuellen Konfiguration vorausgefüllt ist**.
Sie können das **Handelskonto, Symbol, Timeframe, ParamSet und Image-Tag** ändern (und für einen
Backtest, das **Fenster und alle obigen Backtest-Einstellungen**), dann **Speichern & Start** startet es mit der
neuen Einstellungen neu (ersetzt die gestoppte Instance). Das Steuerelement ist **während die Instance aktiv ist deaktiviert** —
nur eine gestoppte Instance kann bearbeitet werden.

## Ausführung aus dem Code-Editor

Das Klicken auf **Run** im Code-Editor öffnet einen Dialog, anstatt einen blinden, hart codierten Durchlauf zu starten:

- **Handelskonto** (erforderlich) — das cTrader-Konto, mit dem sich der cBot verbindet.
- **Parameter set** (optional) — wählen Sie einen vorhandenen Satz, oder lassen Sie ihn leer, um mit den cBot-Standardwerten zu laufen
  **Standardparameterwerte**. Eine **+**-Schaltfläche neben der Auswahl erstellt einen neuen Parameter-Satz
  inline (siehe unten) und wählt ihn aus.
- **Symbol / Timeframe** nehmen `EURUSD` / `h1` als Standard an und können geändert werden; **Cancel** oder **Run**.

Beim **Run** speichert der Editor + erstellt die aktuelle Quelle, startet die Instance auf dem gewählten Konto
mit den gewählten Parametern und verfolgt dann die Live-Container-Logs. (Der Log-Stream leitet das
signiertes Auth-Cookie des Benutzers an den SignalR-Hub `/hubs/logs`, daher verbindet es sich, anstatt mit
`Invalid negotiation response received` zu fehlschlagen.)

## Parameter Sets

Ein **Parameter Set** ist ein benannter, wiederverwendbarer Satz von cBot-Parameterüberschreibungen, der als flaches JSON-
Objekt gespeichert ist, das jeden Parameternamen einer Skalarwert zuordnet, z. B. `{"Period": 14, "Label": "trend"}`. Bei
Durchlauf-/Backtest-Zeit wird es zur cTrader `params.cbotset`-Datei konvertiert
(`{ "Parameters": { … } }`). Sie können einen Satz als rohe JSON aus dem cBot **Parameter
Sets**-Dialog oder inline aus dem Run-Dialog erstellen/bearbeiten.

Jeder Parameter-Satz **gehört zu einem cBot**: der New Parameter Set-Dialog listet alle Ihre cBots auf und Sie
**müssen einen wählen** — die Erstellung wird blockiert, bis ein cBot ausgewählt ist. Ein Satzes **Name ist eindeutig pro cBot**:
Das Erstellen oder Umbenennen eines Satzes in einen Namen, den ein anderer Satz desselben cBot bereits verwendet, wird abgelehnt (ein klarer
Fehler im Dialog, `409 Conflict` auf der API). Derselbe Name kann auf einem **anderen** cBot wiederverwendet werden.

Das JSON wird **bei Speicherung überprüft**: Es muss ein einzelnes flaches Objekt sein, dessen Werte alle Skalare sind
(string / number / bool). Ein Nicht-Objekt-Root, ein Array, ein verschachteltes Objekt, ein `null`-Wert oder
fehlerhaftes JSON wird abgelehnt (ein klarer Fehler im Dialog, `400 Bad Request` auf der API). Ein leeres Objekt `{}`
ist zulässig und bedeutet „keine Überschreibungen".

## cTrader Console CLI-Hinweise

Backtests benötigen `--data-mode` (Standardwert `m1`), Daten als `dd/MM/yyyy HH:mm` und
`params.cbotset` JSON-Positionsargument; `run` lehnt `--data-dir` ab (nur Backtest). Siehe
`ContainerCommandHelpers`.

## Nodes & Skalierung

Die Ausführungskapazität wird skaliert, indem Node-Agents hinzugefügt werden (selbstregistrierend + Heartbeat). Siehe
[node discovery](../operations/node-discovery.md) und [scaling](../deployment/scaling.md).
## Ein Handelskonto ist erforderlich

Das Ausführen oder Backtesting eines cBot erfordert ein cTrader-Handelskonto, mit dem verbunden werden kann. Bis Sie eines hinzufügen unter
**Trading accounts**, sind die **Run New cBot** / **Backtest New cBot**-Schaltflächen deaktiviert (mit Tooltip) und die Seite zeigt einen Hinweis
verknüpft zu Kontoeinrichtung — Sie erhalten keinen rohen
`stream connect failed`-Fehler mehr von einem Bot ohne Konto.
