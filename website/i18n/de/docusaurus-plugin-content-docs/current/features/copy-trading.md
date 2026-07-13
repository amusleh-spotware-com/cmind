---
description: "Spiegeln Sie Master cTrader-Konto auf ein+ Slave-Konten — Cross-Broker, Cross-cID — mit Pro-Ziel-Kontrolle + Geld-Grade-Abstimmung."
---

# Copy-Trading

Spiegeln Sie **Master** cTrader-Konto auf ein+ **Slave**-Konten — Cross-Broker, Cross-cID — mit Pro-Ziel-Kontrolle + Geld-Grade-Abstimmung.

## Konzepte

- **Copy-Profil** — ein Master (`SourceAccountId`) + ein+ **Ziele**. Lebenszyklus: `Draft → Running → Paused → Stopped` (`Error` bei Misserfolg). Aggregate-Wurzel: `CopyProfile` (besitzt `CopyDestination`).
- **Ziel** — ein Slave-Konto + vollständiger Regelwerk wie Master darauf kopiert wird. Alle Konfiguration Pro-Ziel, daher ein Master speist konservative + aggressive Slaves gleichzeitig.
- **Copy-Engine-Host** — laufender Worker für Profil (`CopyEngineHost`). Abonniert Master-Ausführungsstrom, wendet jedes Ereignis auf jedes Ziel an.
- **Supervisor** — `CopyEngineSupervisor`, Background-Service auf jedem Knoten. Hostet zugewiesene Profile, selbst-heilt über Cluster (siehe [Skalierung](../deployment/scaling.md)).

## Was wird gespiegelt

| Master-Ereignis | Slave-Aktion |
|--------------|--------------|
| Markt- / Marktbereich-Position offen | Öffnen Sie eine große Copy (gekennzeichnet mit Quellposition-ID) |
| Limit / Stop / Stop-Limit ausstehende Bestellung | Platzieren Sie die entsprechende ausstehende Bestellung |
| Ausstehende Bestellung Änderung | Ändern Sie die gespiegelte ausstehende Bestellung vorhanden |
| Ausstehende Bestellung Stornierung / Ablauf | Stornieren Sie die gespiegelte ausstehende Bestellung |
| Teilschließung | Schließen Sie die gleiche Proportion der Slave-Position |
| Scale-in (Volumen Steigerung) | Öffnen Sie das hinzugefügte Volumen (Opt-in) |
| Stop-Loss / Trailing-Stop-Änderung | Ändern Sie den Schutz der Slave-Position |
| Vollständige Schließung | Schließen Sie die Slave-Copy |

Jeder Copy **gekennzeichnet mit Quellposition/Bestellungs-ID**. Nach Wiederverbindung host baut Status aus Abstimmung auf: öffnet Copies Master hält aber Slave fehlend, schließt Slave "Waisen" Master nicht mehr hält — **ohne Trades zu verdoppeln**.

## Ein Profil erstellen

**Neues Profil** Dialog auf Copy-Trading-Seite sammelt alles im Voraus: Profilname, Quelle (Master) Konto, Ziel (Slave) Konten (Multi-Auswahl mit **Alles auswählen** Schaltfläche; gewählter Master aus Slave-Liste ausgeschlossen), + vollständiger Pro-Ziel-Optionssatz unten. Alle Eingaben **validiert vor dem Speichern** — fehlender Name/Quelle/Ziel, nicht-positive Sizing-Parameter, negative/Inkonsistente Lot-Grenzen, außer-Bereich Drawdown%, keine Bestellungstyp aktiviert, leere Symbol-Filter oder fehlgeformte Symbol-Map-Paare Oberfläche als Fehlerliste + blockieren Speichern. Bei Bestätigung Profil erstellt + jedes gewählte Slave mit gewählten Einstellungen hinzugefügt.

Row-Aktionen respektieren Lebenszyklus: **Start** aktiviert nur wenn nicht laufend, **Stop** + **Pause** nur wenn laufend, **Löschen** deaktiviert während laufend + fragt Bestätigung vor Profil + Ziele entfernen.

## Pro-Ziel-Optionen

Gesetzt in Neues Profil Dialog, auf Copy-Trading-Seite Pro-Ziel Panel oder via `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + Parameter): festes Lot, Lot/Nominalwert Multiplikator, proportionale Balance/Eigenkapital/freier Spielraum, festes Risiko%, feste Hebelwirkung, Auto-Proportional, **Risiko-%-von-Stop** (M7). Plus Min/Max Lot-Grenzen + Force-Min-Lot. **Risiko-von-Stop** größes Ziel daher risikiert es konfiguriertes Prozent seiner *eigenen* Balance, abgeleitet von **Master-Stop-Loss-Abstand** (`Master risikiert 2% → Slave Auto-risikiert 2%`): `Lots = Balance×% ÷ (StopAbstand × Kontraktgröße)`. Master öffnet **ohne** Stop-Loss hat keinen Abstand zum größen gegen → verwendet konfiguriert **Max-Risiko Fallback-Lot** (M7) wenn gesetzt, sonst übersprungen (`no_stop_loss`) nicht geraten. Proportional-**Eigenkapital**/**freier Spielraum** Größe ab real Konto **Eigenkapital** (`Balance + Σ floating P&L`, abgeleitet pro cTrader Open API die Eigenkapital nicht liefert), nicht reine Balance — daher Master sitzend auf offener Gewinn/Verlust Größen Copies richtig. Verwendeter Spielraum nicht verfügbar gemacht von Abstimmungs-API, daher freier Spielraum behandelt als Eigenkapital (ehrlich verfügbare Mittel Proxy); andere Modi lesen Balance + überspringen zusätzliche Neubewertungs-Rundfahrt.
- **Richtungsfilter**: beide / nur-lang / nur-kurz. **Umkehrung**: Flip-Seite (+ Tausch SL↔TP) für konträres Copy.
- **Verwalten-nur** (Neue-Trades-ignorieren / Nur-Schließen): Spiegelbild schließen, Teilschließungen + Schutzänderungen auf bereits-kopierte Positionen, aber öffnen Sie **keine** neuen Positionen/ausstehenden Bestellungen (übersprungen `manage_only`). Verwendung zum Abwickeln des Ziels ohne Schneiden vorhandener Copies.
- **Sync-Öffnung-on-Start** / **Sync-Geschlossen-on-Start** (Standard an): bei Profil **erste** Resync, ob Copies für Master existierende Positionen zu öffnen, + ob Copies Master geschlossene während Profil gestoppt zu schließen. Beide gelten nur beim Start — Mid-Run Wiederverbindung immer voll versöhnt daher Desync erholt unabhängig.
- **Symbol Map** + **Symbol Filter** (Whitelist / Blacklist). Jeder Symbol-Map-Eintrag trägt optional **Pro-Symbol-Volumen-Multiplikator** (cMAM Pro-Symbol Override) Skalierung-Copy-Größe für dieses Symbol auf Top des Ziel-Sizings (1 = keine Änderung). Ganze Map Importe/Exporte als **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; Spalten `Source,Destination,VolumeMultiplier`) — jede Reihe validiert durch Domain-Wertobjekte, daher kann fehlgeformte Datei nicht ungültige Map erzeugen.
- **Trading-Stunden-Fenster** (C18) — Pro-Ziel täglich UTC-Fenster (`Start`/`Endt` Minuten-des-Tages, Ende exklusiv; `Start == Ende` = ganztägig). Neue Opens außerhalb Fenster übersprungen (`trading_hours`); Fenster mit `Start > Ende` Wraps vorbei Mitternacht (z. B. 22:00–06:00). Vorhandene Positionen bleiben verwaltet.
- **Quell-Label-Filter** (C18, cTrader Äquivalent von MT Magic-Nummer-Filter) — wenn gesetzt, kopieren Sie nur Master-Trades deren Label-Spiele **exakt** (z. B. einen Bot-Trades oder nur-manuell-Label); sonst übersprungen (`source_label`). Leer = Copy alle. Getragen auf `ExecutionEvent.SourceLabel` von Master-Position/Bestellung `TradeData.Label`, geehrt auf Resync auch.
- **Kontoschutz** (ZuluGuard / Globaler Kontoschutz) — überblick Ziel **live Eigenkapital** (`Balance + Σ floating P&L`, abgerufen jeden `CopyDefaults.EquityGuardInterval`) gegen `StopEquity` Boden und/oder optional `TakeEquity` Decke. Bei Verstoß, gelten Modus: **CloseOnly** (stoppen neue Copies, halten Verwaltung vorhandener), **Frozen** (stoppen Öffnung), **SellOut** (Schließen Sie **jede** Copy auf Ziel sofort). Einmal Feuer, Ziel Verriegelung — keine neuen Opens bis Host Neustarts — + `CopyAccountProtectionTriggered` Warnung erhöht. `SellOut` erfordert `StopEquity`; `TakeEquity` muss oberhalb `StopEquity` sitzen. **Keine-Garantie-Warnung:** Ausverkauf verwendet Markt-Ausführung — wie jedes Konkurrenten-Äquivalent, kann Füll-Preis nicht in schnell/gegaptem Markt garantieren.
- **Flatten-All Panik-Schaltfläche** (C8) — `POST /api/copy/profiles/{id}/flatten` schließt sofort **jede** kopierte Position auf jedem Ziel + Sperren gegen neue Opens. Geroutet Cross-Prozess: API setzt Flag, Supervisor sendet an laufenden Host (wiederverwendend Token-Rotation-Kanal), der Flache an Ort; Flag gelöscht daher Feuer genau einmal (`CopyFlattenAll` Warnung). Benutzer dann pausiert/stoppt Profil.
- **Prop-Firm Regel-Guard** (C7) — Erzwingung Prop-Firm Copier-Benutzer fragen um. Pro Ziel, **täglich-Verlust Cap** (Verlust von Tag-Öffnungs-Eigenkapital) und/oder **Trailing-Drawdown** Limit (Verlust von laufen Peak-Eigenkapital), beide in Einzahlungs-Währung. Bei Verstoß Ziel **Auto-Flach** (jede Copy geschlossen) + **gesperrt** Rest UTC-Tag (neue Opens übersprungen `prop_lockout`); `CopyPropRuleBreached` Warnung Feuer. Lockout klar wenn UTC-Tag rollt über (frischer Baseline/Peak genommen). Anteile gleiches Live-Eigenkapital Poll wie Kontoschutz.
- **Ausführungs-Jitter** (C11, Standard aus) — zufälliges `0..N` ms Verzögerung vor dem Platzieren jeder Copy, zur De-Korrelation Nahe-identische Bestellungs-Zeitstempel über Benutzer **eigenen** Konten. **Compliance-Warnung:** Hilfe für Prop-Firmen die *erlauben* Kopieren — **nicht** Tool um Firma zu vermeiden die verbietet; bleiben Sie innerhalb Ihrer Firma Regeln ist Ihre Verantwortung.
- **Konfiguration Sperren** (C9) — Einfrieren Ziel Einstellungen für Punkt (`POST …/destinations/{id}/lock` mit Minuten). Während gesperrt, Ziel kann nicht entfernt werden (Aggregate lehnt mit `CopyDestinationConfigLocked` ab) — absichtlich Guard gegen impulsive Änderungen während Drawdown. Sperren läuft automatisch bei seinem Zeitstempel ab.
- **Konsistenz Pre-Warnung** (C10) — warnen (einmal pro UTC-Tag) wenn Ziel **täglich Gewinn** erreichter konfiguriert Prozent der Tag Öffnungs-Eigenkapital (`CopyConsistencyThresholdApproaching`), daher Prop-Firm Konsistenzen Regel respektiert *bevor* es Trips. Gewinn-Seite, unabhängig von Verlust-Seite Lockout; läuft aus gleicher Tag Baseline wie Prop-Regel Guard.
- **Bestellungstyp-Filter** — wählen exakt welche Master-Bestellungstypen zu kopieren: Markt, Marktbereich, Limit, Stop, Stop-Limit (`CopyOrderTypes` Flags; Standard alle). cMAM-Stil Selektivität.
- **Copy SL / Copy TP** — Spiegel Master Stop-Loss / Gewinn-Ziel, oder verwalten Schutz unabhängig.
- **Copy Trailing Stop**, **Spiegel Teilschließung**, **Spiegel Scale-in** — jede unabhängig umschaltbar.
- **Copy ausstehend Ablauf** (Standard an) — Spiegel Master ausstehend Bestellung Good-Till-Date Ablauf Zeitstempel.
- **Copy Master Slippage** (Standard an) — für Marktbereich + Stop-Limit Bestellungen, Platz Slave-Bestellung mit Master-Exakt Slippage-in-Pips (Basis-Preis genommen von Slave Live-Spot).
- **Guards**: Max Drawdown%, täglich Verlust Cap, Max Copy-Verzögerung, Slippage-Filter (Skip Copy wenn Slave-Preis bewegt jenseits N Pips von Master-Eintrag). **Max Copy-Verzögerung** gemessen gegen Master-Ereignis echten Server-Zeitstempel (`ExecutionEvent.ServerTimestamp`) via injiziert `TimeProvider`: Signal älter als konfiguriert Max-Lag übersprungen, daher stale Copy nie spät platziert (vor Verzögerung immer Nullt + Guard tot).
- **SL/TP Präzisions-Normalisierung** (M6) — kopiert Stop-Loss/Take-Profit-Preise gerundert zu **Ziel** Symbol Ziffer Präzision bevor Änderung, daher Master-Preis bei feiner Präzision (oder Cross-Broker Ziffer Mismatch) nie Trip Server `INVALID_STOPLOSS_TAKEPROFIT`.
- **Ablehnung Circuit Breaker / Follower Guard** (G8) — Ziel ablehnen `CopyDefaults.RejectionBudget` öffnet in Reihe ist **getripped**: keine neuen Opens für Cooldown-Fenster (`CopyDestinationTripped` Warnung Feuer), stoppen Ablehnung Sturm aus Hämmern (Prop-Firm) Konto. Vorhandene Positionen noch verwaltet + geschlossen während getripped; Breaker Auto-Rücksetze nach Cooldown + erfolgreich Copy löscht Zähler.
- **Lot Sanity Decke** (C14) — absolut Max Copy-Größe und/oder Multiple-of-Master Cap. Berechnet Copy übersteigt absolut Cap, oder übersteigt `N×` Master Eigene Lot-Größe, **hart-blockiert** (dargestellt als `lot_sanity` Skip, gezählt auf `cmind.copy.skipped`) nicht platziert — verteidigen gegen katastrophal-Übergroße Klasse (0.23-Lot Master in 3 Lots jeder Receiver via Runaway Multiplikator oder Rundung Bug). Beide Dimensionen Standard `0` (aus).

## Zuverlässigkeit & Rand-Fälle

Engine gebaut für Realität dass alles kann Fehler anytime:

- **Slave-ausstehend Füllung-Korrelation Timeout** (C13) — gespiegelt Slave ausstehend dessen Master ausstehend verschwunden (weder Ruhend noch frisch gefüllt) storniert nach Korrelations-Timeout, daher Slave Copy kann Füllung nicht-korreliert in nicht-verwaltete Position nicht (`CopyPendingTimedOut`). Resync auch räumt Bestellungs-ID-gekennzeichnet gefüllt-ausstehend Waise.
- **Robust Schließung/Flatten** (M8) — Schließung Waise auf Resync, oder Flatten auf Guard Bruch, toleriert Position Broker bereits geschlossen (`POSITION_NOT_FOUND`): jede Schließung läuft unabhängig, daher eine Stale ID nie abbruch Resync oder Blatt Rest Konto Un-flattened.

- **Start mit Master bereits in Trades** — auf Start Host Versöhnung + öffnet Copies für Master vorhandene Positionen.
- **Verbindung Tropfen / Desync** — auf Wiederverbindung Host Versöhnung: öffnet fehlende Copies, schließt Waisen, Re-Etiketten ausstehend. Keine Duplikat-Bestellungen.
- **Bestellungs-Platzierung Misserfolg** — Misserfolg auf einem Ziel protokolliert, nie blockiert andere Ziele.
- **Einzelner gültiger Token pro cID** — cTrader ungültig cID alte Zugriff-Token Moment neue ausgestellt. cMind vertauscht laufenden Host Token **an Ort** (Re-Auth auf Live-Socket) daher Kopieren fortsetzt ohne Strom fallen. Siehe [Token Lebenszyklus](token-lifecycle.md).

## Audit-Fähigkeit

Jede Aktion emittiert strukturiert, Quell-generiert Log-Ereignis (`LogMessages`) mit Profil-ID, Ziel cID, Bestellung/Position-IDs, + Werte — Bestellung platziert/übersprungen (mit Grund), Teilschließung, Schutz angewendet, Trailing angewendet, ausstehend platziert/geändert/storniert, Ablauf gespiegelt, Marktbereich Slippage gespiegelt, Token vertauscht, Resync Zusammenfassung. Dies ist das Audit-Trail für Compliance + Streit-Lösung.

Zusammen mit Protokollen, Engine emittiert **OpenTelemetry Metriken** auf `cMind.Copy` Meter (registriert in gemeinsam OTel Pipeline, exportiert über OTLP / zu Azure Monitor wie Rest): `cmind.copy.latency` (Master-Ereignis → Versand, ms), `cmind.copy.dispatch.duration` (Fan-out zu alle Ziele, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (getaggt von Ziel), `cmind.copy.skipped` (getaggt von Grund), + `cmind.copy.failed`. Diese machen Latenz/Slippage Regression messbar, nicht nur sichtbar in Log-Zeile — Live-Suite behauptet sie gegen Budget.

## API

- `GET /api/copy/profiles` — Liste.
- `POST /api/copy/profiles` — erstelle (mit optional Ziel-Konto-IDs).
- `GET /api/copy/profiles/{id}` — vollständig Detail incl. jedes Ziel-Option.
- `POST /api/copy/profiles/{id}/destinations` — füge Ziel mit dem vollständigen Option-Satz.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — entfernen.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — Lebenszyklus.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — Sizing-Modi, Entscheidungs-Filter, Bestellungstyp-Filter, Ablauf-Copy, Marktbereich/Stop-Limit Slippage, SL/TP Umschalter, Teilschließung, ausstehend Änderung/Stornierung, Start-mit-Öffnung, Trennung→Desync→Resync, an-Ort Token Tausch, Cross-cID Ungültigerklärung. Läuft gegen `FakeTradingSession`, cTrader-treuer in-Memory Simulator.
- **Integration** (`tests/IntegrationTests/CopyLive`) — Knoten-Affinität/Anspruch Anspruch, Token-Version Propagation auf real Postgres.
- **E2E** (`tests/E2ETests`) — Ziel-Option Rundfahrt durch API + UI, vollständig Lebenszyklus.
- **Stress / DST** (`tests/StressTests`) — deterministisch-Simulation Prüfung: Besamt zufälliges Arbeitslasten + Fehler Injektion (Socket Flap, Bestellung Ablehnung, Marktbereich Ablehnung, Token Rotation, Knoten Tod) Fahren `CopyEngineHost` zu Ruhe + behaupten Konvergenz Invarianten. Siehe [testing/stress-testing.md](../testing/stress-testing.md). Diese Suite Oberfläche + behoben echten Startup Rasse: `OnReconnected` verkabelt bevor anfängliche Referenz-Last + Resync, daher Socket Flap während Startup könnte zweiten Resync gleichzeitig laufen + verderben Host Non-Concurrent Zustand Wörterbücher — Startup Last + erste Resync jetzt laufen unter `_stateGate`.
- **Live** — echten cTrader Demo-Konten; siehe [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Siehe [dev-credentials.md](../testing/dev-credentials.md) für einzigen Anmeldedaten Datei Live + E2E Tier lesen.
