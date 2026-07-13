---
description: "Stress-Suite. Hämmert App-Teile deren Misserfolg Benutzer Geld kostet — Haupt-Copy-Trading — mit feindselig, zufällig, Fehler-injiziert Arbeitslasten. Behauptet System…"
---

# Stress-Testen

Stress-Suite. Hämmert App-Teile deren Misserfolg Benutzer Geld kostet — Haupt-**Copy-Trading** — mit feindselig, zufällig, Fehler-injiziert Arbeitslasten. Behauptet System bleiben korrekt. Lebt in `tests/StressTests`, läuft in normal `dotnet test` grün Gate.

## Ansatz — Deterministisch Simulations-Prüfung (DST)

Bester Weg Stress verteilt finanzielle Systeme = **Deterministisch Simulations-Prüfung**, pro TigerBeetle, FoundationDB, Antithesis: laufen echte Logik gegen *simuliert* Welt, fahren mit **Besamt** zufällig Arbeitslasten + injiziert Fehler, behaupten Invarianten bei Ruhe. Alle Besamt + Deterministisch → jede Misserfolg reproduziert exakt von Besamt. Kombiniert mit:

- **Chaos-Engineering Fehler-Injektion** (Netflix Chaos Monkey Stil) — Verbindung Tropfen, Bestellung Ablehnung, Token Rotation, Knoten Tod.
- **Eigenschaft-basiert Invarianten** — nein behaupten exakt Anruf-Sequenzen; behaupten Eigenschaften die halten müssen nein Wichtigkeit wie Ereignisse verzahnen (Konvergenz, nein Waisen, bei-meisten-ein Anspruch Halter).

App bereits Schiffe Perfekt DST Welt Modell: `FakeTradingSession`, cTrader-treu In-Memory Open API Sitzung. Stress-Suite wiederverwendet es (verlinkt, einzeln Quelle der Wahrheit) nicht Mock, daher simuliert Broker verhalten wie echte.

## Was es deckt

### Copy-Trading (primär Fokus)

Getrieben via `CopyDstWorld` (`tests/StressTests/CopyTrading/`), läuft Live `CopyEngineHost` gegen Fake-Sitzung, Ausstellungen Mitgliedschaft-konsistent Quell-Arbeitslasten:

| Szenario | Stresse |
|---|---|
| `Mass_fan_out…` | 1 Quelle → 80 Ziele, 150 öffnet dann schließt; vollständig Fan-out + Drainer |
| `High_frequency_open_close…` | 300 schnell verzahnt Öffnung/Schließung; nein Leck Positionen |
| `Partial_close_and_scale_in_storm…` | Teilschließung + Scale-in Churn; Label-Satz Stabilität |
| `Connection_flap_storm…` | wiederholten Socket Trennung/Wiederverbindung + Mid-Flight Desync; Resync Konvergenz |
| `Order_rejection_cascade…` | eine Teilmenge lehnt jede Bestellung ab; gesund Ziele unbeeinträchtigt, dann selbst-heilen via Resync |
| `Token_rotation_storm…` | schnell In-Place Token Tausch während ein Bestellung Sturm |
| `Randomized_chaos_workload…` (10 Samen) | **die DST Kern** — jede Ereignis-Typ + jede Fehler verzahnt unvorhersehbar |
| `CopyLeaseReclaimStressTests` | Knoten Tod + Anspruch Rückforderung über ein Skalierung Cluster (rein Domäne, `FakeTimeProvider`) |

**Konvergenz Invariante.** Bei Ruhe, jede gesund Ziel Spiel exakt Set von noch-offen Quell-Positionen — nein Waisen, nein fehlend. Behauptet auf Label *Set* (Scale-in legitim öffnet zweiten Ziel-Position unter gleich Quell-Label, daher Duplikat-Label erwartet). Ziel aktuell ablehnend Bestellungen erlaubt zu Verzögerung, versöhnt einmal geheilt.

**Anspruch Invariante.** In Cluster wo Knoten sterben + Wiederauferstehen auf Besamt Zeitplan, bei-meisten-ein Knoten jede hält gültig Anspruch auf ein Profil; Tote Knoten Anspruch lapses exakt bei Ablauf, bekommt rückgefordert; gesund Cluster setzt sich mit jede Profil gehalten durch exakt einer Knoten. Spiegel `CopyEngineSupervisor` Anspruch Prädikat gegen `CopyProfile` Domäne Anspruch Methoden.

### Thread-Sicherheit der Geschirr

`FakeTradingSession` einzeln-Gewinde; Stress-Arbeitslasten Mutanten es von Test-Gewinde während Host liest/schreibt von sein Schleife. `SyncTradingSession` Wrap es, macht jede Sitzungs-Operation Atomare auf ein Gate (ohne Gate halten über Wiederverbindung Rückruf — würde Lock Reihenfolge gegen Host `_stateGate` umkehren und Deadlock). Simulator selbst links unberührt.

## Fehler gefunden

- **Startup Resync Rasse in `CopyEngineHost`.** `OnReconnected` verkabelt bevor anfängliche Referenz-Last + erste Resync, die laufen ohne `_stateGate`. Socket Flap während Startup lief zweite Resync Gleichzeitig, verderbt Host Non-Concurrent Zustand Dicts (`_symbolDetails`, `_sourceVolumes`). Behoben: laufen Startup Last + erste Resync unter Gate. Produktion Rasse, nicht Test Artefakt — DST Chaos-Arbeitslasten Oberfläche es.

## Lauf

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serialisiert** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): jede Test spins Live Host Hintergrund Schleife, fahren zu Ruhe unter Wanduhr, daher parallel Lauf Hunger Host Tasks und macht Konvergenz Timeouts Flaky. Arbeitslasten größe zu Fertig in Sekunden daher Suite bleibt in Standard grün Gate. Misserfolg drucken sein Samen; Re-Lauf die Samen zu reproduzieren exakt Verzahnung.

## Erweiterung

- Neu Copy-Verhalten → Hinzufügung Quell-Op zu `CopyDstWorld` (halten Quell-Buch Mitgliedschaft konsistent mit Ereignis-Strom) + Gewichtet Fall in `CopyChaosDstTests`. Wenn es kann erstelle oder Ruhestand ein Ziel-Position, Bestätigung konvergent Invariante noch hält.
- Neu Fehler → Hinzufügung Injector zu `CopyDstWorld` (Delegat zu `FakeTradingSession` Kontroll-Oberfläche via `SyncTradingSession`) + Ausübung in ein benannt Szenario plus Chaos-Mix.
- Halten Simulator cTrader-treu (siehe Root `CLAUDE.md` Auftrag); nie schwächen es zu Stress-Test bestand zu machen.
