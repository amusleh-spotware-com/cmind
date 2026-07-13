---
description: "Vollständige Überprüfung der verbleibenden Copy-Trading-Arbeit — alles unten wurde **tatsächlich ausgeführt**, nicht nur verfasst."
---

# Copy-Trading-Überprüfungs-Lauf (2026-07-10)

Vollständige Überprüfung der verbleibenden Copy-Trading-Arbeit — alles unten wurde **tatsächlich ausgeführt**, nicht nur verfasst.

## Live (echte cTrader Demo-Konten) — 8/8 Pass

1:1 · 1:many · Umkehrung · Cross-cID · Teilschließung · **ausstehend Limit + Stornierung** · **Trailing Stop** · Token-Aktualisierung. Hinzugefügte Live-Szenarien `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integration (real Postgres, Testcontainer) — Pass

- `CopyNodeAffinityTests` — Supervisor echten atomaren Anspruch: erster Knoten fordert alle laufenden Profile, zweite fordert **0** (keine doppel-Copy); Pause gibt frei + erneute Forderung.
- `TokenRotationSignatureTests` — Signatur ändert sich nur auf echte Token-Rotation.

## In-Cluster (Kind + Helm) — Pass

Installiert `kind`/`kubectl`/`helm`, laufen `scripts/k8s-e2e.sh` gegen echten Kind-Cluster:

- **Deterministische Job: 101 bestand** in-Cluster.
- **Live Job: 8 bestand** in-Cluster (init-Container `seed-secrets` kopiert Secret → schreibbar emptyDir, echte Demo-Konten).
- Job `Complete 1/1`, Skript Exit 0.

## Fehler gefunden während Überprüfung (behoben + neu überprüft)

- **Ausstehende Ereignisse**: cTrader hängt *nicht-offen Position Platzhalter* an Ruhe Limit/Stop `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` klassifiziert jetzt Platzierung/Stornierung als Bestellungs-Ereignis bevor Position Branche, aber lässt Limit/Stop *Füllung* (z. B. Stop-Loss-ausgelöst Schließung) durch zu Schließungs-Pfad fallen.
- **Einzel-Nutzungs-Aktualisierungs-Token**: cTrader rotiert Aktualisierungs-Token jede Aktualisierung. Lese-nur Cache, der nicht persistieren kann, ungültig gemacht sich selbst. Live K8s Job daher kopiert Secret in **schreibbar** emptyDir; Job Standard zu deterministisch Suite. `SaveTokens` jetzt Best-Effort. Live Symbole erzwungen zu FX (BTCUSD Trailing-Änderungen Broker-abgelehnt).
- Skript Bild-Benennung behoben zu Match Helm `registry/repository` Split + `pullPolicy=Never`.

## Erweiterte Spiegelung + Token-Lebenszyklus + Skalierung Programm (2026-07-10) — deterministische Tierbestehen Pass

Nachfolge-Programm fügt Bestellungstyp-Filterung, Ausstehende-Bestellungs-Ablauf-Kopieren, Marktbereich / Stop-Limit Slippage Spiegelung, SL/TP Copy Umschalter, anmutige In-Place-Token Tausch (einziger gültiger Token pro cID), cTrader-treue Simulator, Selbst-heilender Knoten Anspruch, einheitliche Dev-Anmeldedaten Datei.

- **Unit — 210 bestand** (`dotnet test tests/UnitTests`). Neue Copy-Abdeckung: Bestellungstyp-Filter (offen + ausstehend), Marktbereich Slippage Mirror + Basis-Preis, Ablauf Copy an/aus, Stop-Limit Slippage, Ausstehend Änderung, Start-mit-Master-Offen, Trennung→Master-Gehandelt→Wiederverbindung Resync (offen fehlend + Schließung Waise), In-Place Token Tausch (kein Neustart), Cross-cID Ungültigerklärung, Domain-Invarianten, Anspruch Besitz, Token-Version Bump.
- **Integration (real Postgres, Testcontainer) — Pass**: `CopyNodeAffinityTests` (Atomarer Anspruch, keine doppel-Copy, Pause Freigabe, **abgelaufen-Anspruch Rückforderung von einem anderen Knoten**), `TokenRotationSignatureTests` (Signatur ändert sich auf Token-Version Bump), `OpenApiAuthorizationPersistenceTests` (TokenVersion persistiert + Inkremente auf Aktualisierung).
- **E2E** (`tests/E2ETests`): Ziel-Option Rundfahrt behauptet jetzt Bestellungstyp-Filter, Copy-Ablauf, Copy-Slippage zusammen mit vollständig Lebenszyklus.
- **Build**: sauber unter `TreatWarningsAsErrors`; Rider `get_file_problems` sauber auf geändert Dateien.

Live-Szenarien (echte cTrader Demo-Konten) für Ausstehend-Stop, Marktbereich, Ablauf, Start-mit-Offen, Mid-Run Token-Rotation verfasst gegen gleiche Engine; laufen mit einheitlich `secrets/dev-credentials.local.json` pro [dev-credentials.md](dev-credentials.md).

## Bekannte Nachfolge

In-Cluster Live-Run rotierter Einzel-Nutzungs-Token; regenerieren Sie lokalen Cache mit `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests` (cTrader drosselten seine OAuth-Seite direkt nach Lauf — Wiederholung wenn klärt).
