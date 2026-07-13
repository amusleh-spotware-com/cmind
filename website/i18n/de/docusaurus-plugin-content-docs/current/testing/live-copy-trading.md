---
description: "Vollständig wiederholbar Copy-Trading-Test-Suite. Zwei Schichten:"
---

# Copy-Trading-Test-Suite (Deterministisch + Live)

Vollständig wiederholbar Copy-Trading-Test-Suite. Zwei Schichten:

1. **Deterministische Tests** (xUnit, kein Netzwerk) — Copy-Mathe + Engine-Logik. Schnell, CI, keine Geheimnisse. Abdeckung jeden Geld-Management-Modus, jeden Filter/Option, Engine-Zuverlässigkeit.
2. **Live E2E Tests** (echte cTrader Demo-Konten) — Produktion `CopyEngineHost` Platzierung + Kopierung echte Bestellungen zwischen echte Konten. Vollständig automatisiert, wiederholbar wie Unit-Test: lesen Cache Anmeldedaten von lokal Gitignore-Dateien, selbst-Aktualisierung Zugriff-Token, Überspringung sauber wenn Geheimnisse fehlend (CI bleibt grün).

Läuft niemals gegen Live-finanziiert Konto — jedes Konto **Demo**, jeder Live-Test schließt Positionen es öffnet.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — jeder Größungs-Modus + Rundung + Min/Max Lot
  CopyDecisionEngineTests.cs     — Richtung/Umkehrung/Slippage/Verzögerung/Symbol-Filter/Größe-Null
  CopyEngineHostTests.cs         — Host Copy-Logik gegen In-Memory Fake-Sitzung
  FakeTradingSession.cs          — Deterministisch IOpenApiTradingSession (zeichnet Bestellungen/Schließungen/Änderungen)
  OpenApiConnectionTests.cs      — Verbindung / Wiederverbindung / Backoff / Tödlicher Fehler (Zuverlässigkeit)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — lädt Gitignore Geheimnisse, speichert Aktualisiert Tokens
  LiveTokenBootstrapTests.cs     — einmalig: Entschlüsselung Tokens von App DB in Token Cache
  LiveCopyFixture.cs             — rotiert Zugriff-Token, verfügbar machen Demo-Konten Liste
  LiveCopyScenario.cs            — läuft eine echte Copy-Szenario von einem Ende zum anderen (offen → Copy → überprüfen → Bereinigung)
  CopyTradingLiveTests.cs        — die Live-Szenarien (1:1, 1:viele, Umkehrung, …)
```

## Geheimnisse (lokal, Gitignore — niemals festgeschrieben)

Alle Anmeldedaten unter `<Repo>/secrets/` (bereits in `.gitignore`). Entwickler schreiben **nur erste zwei Dateien**; dritte (Token) Auto-produziert von Onboarding.

`secrets/openapi-test-app.local.json` — Open API App:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID Login Anmeldedaten zu Autorisierung (ein oder viele):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **Geschrieben von Onboarding**, Multi-cID, Aktualisiert jede Lauf:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Aktualisierungs-Token **läuft nie ab**, daher nach einmaliger Onboarding Live-Tests funktionieren unbegrenzt: jede Lauf austausch jede cID Aktualisierungs-Token für Frisch Zugriff-Token (Rotation) — keine Browser, keine Aufforderungen.

## Einmaliger Onboarding (Vollständig automatisiert — kein Entwickler Wechselwirkung jenseits speichern Anmeldedaten)

Onboarding fahren echte cTrader ID Login in Headless-Browser von gesparte cID Anmeldedaten, erfasst OAuth Callback auf lokal HTTPS Hörer bei App registriert Umleitung (`https://localhost:7080/openapi/callback`), austausch Code für Token, lädt Konten-Liste, schreib Multi-cID Token-Cache. Lauf einmal pro Maschine (oder wenn hinzufügen cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autorisiert jede cID in `openapi-cids.local.json`, schreib `openapi-tokens.local.json`. Nach dem Live Copy-Tests brauchen nichts sonst. (cID cTrader ID Konto muss nein 2FA/Captcha auf Login für Automatisierung zu abschließen.)

**Alternative Bootstrap** (wenn Konten bereits autorisiert in laufend App): Entschlüsseln Sie gespeichert Token direkt aus App Postgres Band anstatt Re-Autorisierung:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Sicherheit — Demo nur

Live-Tests Handel **nur Demo-Konten**: Vorrichtung Filter Token-Cache zu Konten mit `IsLive == false` und verbindet zu Demo-Gateway, daher Bestellung kann nie landen auf Live/finanziiert Konto, selbst wenn Live-Konto autorisiert. Jede Position ein Test öffnet geschlossen in Bereinigung.

## Lauf

```bash
# Deterministisch Copy-Tests nur (schnell, keine Geheimnisse, CI-sicher)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live Copy-Tests gegen echten Demo-Konten (braucht die zwei Geheimnisse-Dateien)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Alles
dotnet test
```

Ohne Geheimnisse-Dateien Live-Tests drucken Überspringungs-Grund + Bestanden als Noop, daher Suite sauber zu laufen überall.

## Abdeckung

### Geld-Management / Größung (Deterministisch — `CopySizingCalculatorTests`)

FixedLot · LotMultiplier · NotionalMultiplier (Vertrag-Größe / Währung) · ProportionalBalance · ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage · Skalierung **auf** und **ab** für Balance/Hebelwirkung/Kapazität Mismatch (die "goldene Regel") · Lot-Schritt Rundung · Min-Lot Überspringung vs Force-zu-Min · Max-Lot Kappe · Enger-von Grenze-vs-Spezifikation Min & Max · Null Master-Balance Überspringung.

### Entscheidungs-Filter (Deterministisch — `CopyDecisionEngineTests`)

Symbol Whitelist / Blacklist / Erlauben · LongOnly / ShortOnly · Umkehrung Flip die effektiv Seite · Slippage über Limit Überspringung + exakt-bei-Limit erlaubt · Stale-Signal (Max Verzögerung) Überspringung · Größe-Null Überspringung · Wiederverbindung Abstimmung (Offen-fehlen Dedupe, Schließung-Waise).

### Copy Engine Host (Deterministisch — `CopyEngineHostTests`, In-Memory Sitzung)

Offen Spiegel ein Markt-Bestellung (Seite / Volumen / Label) · **Umkehrung** Flip Seite und **Tausch SL/TP** · **Symbol Abbildung** lösen die Ziel-Symbol · **Bestellung-Misserfolg auf einer Sklave noch Copy zu den andere** · Quelle Schließung schließt die gespiegelt Copy · Wiederverbindung Resync schließt Waise Copies.

### Verbindungs-Zuverlässigkeit (Deterministisch — `OpenApiConnectionTests`)

Erreicht Verbunden nach App Auth · Abgelöst Verbindung Wiederverbindung und Re-Auth · Tödlicher Auth Fehler Fehler · Exponentiell Backoff.

### Live, echte cTrader Demo-Konten (`CopyTradingLiveTests`)

Token-Aktualisierung + Konten-Listing · **1:1** Copy führt aus · **1:viele** Copy Spiel zu jedem Sklave · **Umkehrung** dreht Master Kauf zu Sklave Verkauf · **Cross-cID** Copy (Master unter einer cID Spiel zu Sklave unter ein andere, jede Authentifizierung mit sein Token). Jede öffnet echten Min-Lot Position auf Master, warte für Engine zu Spiegel es (Passen durch Quell-Position-ID Label auf Sklave), behaupten, schließe alles. Geschlossen Markt berichtet **Inconclusive**, nicht fehlschlag.

## Protokollierung & Audit-Fähigkeit

Jede Copy Trading Operation protokolliert via Quelle-generiert strukturiert Ereignisse (`Core/Logging/LogMessages.cs`, Ereignis IDs 1043–1055), vollständig Trail Audit-Fähig:

| Ereignis | ID | Bedeutung |
|-------|----|---------|
| CopyHostStarted | 1046 | ein Profil Engine kam oben (Quelle + Ziel Anzahl) |
| CopySourceOpen | 1047 | Master öffnen ein Position (Symbol / Seite / Lots) |
| CopyOrderPlaced | 1048 | Copy Bestellung sendet zu ein Sklave (Symbol / Seite / Volumen / Quell-ID) |
| CopySkipped | 1049 | ein Copy wurde übersprungen und warum (Slippage / Richtung / Symbol_Filter / Größe_Null / …) |
| CopyProtectionApplied | 1050 | SL/TP angewendet zu ein Sklave Copy |
| CopyOpenFailed | 1051 | ein Sklave Copy-Offen fehlgeschlagen (Isoliert — andere Sklaven fortsetzen) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | Master geschlossen → Sklave Copy geschlossen |
| CopyCloseFailed | 1054 | ein Sklave Copy-Schließung fehlgeschlagen |
| CopyResync | 1055 | Wiederverbindung Abstimmung (Quelle offen Anzahl, Waisen geschlossen) |
| CopyPartialClose | 1056 | Master Teilschließung gespiegelt — Proportional Scheibe geschlossen auf ein Sklave |
| CopyScaleIn | 1057 | Master Scale-in gespiegelt (Opt-in) — hinzugefügt Volumen kopiert zu ein Sklave |
| CopyPendingOrderPlaced | 1058 | Ausstehend Limit/Stop gespiegelt zu ein Sklave (Opt-in) |
| CopyPendingOrderCancelled | 1059 | Quelle Ausstehend storniert → Sklave Ausstehend storniert |
| CopyTrailingApplied | 1060 | Trailing Stop angewendet zu ein Sklave Copy (Opt-in) |
| CopyStopLossAmended | 1061 | ein Quelle SL verschieben Re-Änderung die Sklave Copy |
| CopyHostTokenRotated | 1062 | Supervisor Neustart ein laufend Host nach sein Zugriff-Token rotiert |

Protokolle emittiert als Serilog kompakt JSON (strukturiert Props: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), versendet zu OTLP wenn `OTEL_EXPORTER_OTLP_ENDPOINT` gesetzt. **Vollständig konfigurierbar** pro Kategorie via Standard-Konfiguration — z. B. erhöhen/Senken Copy-Engine Ausführlichkeit ohne Berührung Code:

```jsonc
// appsettings.json — Serilog Level Außerkraftsetzung
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // die CopyEngineHost Audit-Trail
  "Nodes.CopyTrading": "Information"        // Supervisor / Token-Aktualisierung
} } }
```

`Audit_log_records_every_trading_operation` Host Test behauptet Trail Feuer für offen, Bestellung, Schutz, Schließung.

## Rand-Fälle (Validiert gegen wie echte Copy/MAM Plattformen fehlschlag)

Slippage & Latenz, Symbol Suffix/Mismatch, Duplikat Trades auf Wiederverbindung, Hebelwirkung Mismatch & Spielraum-sauber Größung, Einzahlungs-Währung/Vertrag-Größe Differenzen, Min/Max Lot & Rundung, Abgelöst Bestellungen, Richtungs-Filter, Waise Bereinigung nach Trennung — alle oben abgedeckt. Quellen: [Hebelwirkung Mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) · [Cross-Broker Kopieren](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) · [Copier Fallstricke](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) · [Slippage & Latenz](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) · [Warum Copy Trading fehlschlag](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) · [Risiko Parameter](https://www.mt4copier.com/risk-parameters/).

## Erweitert Spiegelung Abdeckung (Teilschließung · Ausstehend Bestellungen · SL-Trailing)

Host Spiel mehr als Markt Offen/Schließung. Jede Verhalten = Pro-Ziel Opt-in Flag auf `CopyDestination` (`MirrorPartialClose` Standard an, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` Standard aus), bewacht durch Intention Methoden, Jsonb-persistiert (Migration `CopyAdvancedMirroringAndNodeAffinity`).

| Verhalten | Deterministisch Test (`CopyEngineHostTests`) | Live Test |
|-----------|--------------------------------------------|-----------|
| Teilschließung → Proportional Scheibe | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 schließt 60%) + deaktiviert Pfad | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Ausstehend Limit/Stop platziert | `Pending_order_is_placed_on_the_slave_when_enabled` (Theorie: Limit+Stop) + deaktiviert Pfad | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Ausstehend Stornierung | `Source_pending_cancel_cancels_the_slave_pending` | (gleich Live Test — Stornierung auf Master, behauptet Sklave Stornierung) ✅ |
| Gefüllt Ausstehend nein Doppel-Offen | `Filled_pending_does_not_double_open` (Bestellungs-ID → Position-ID Dedupe) | — |
| Trailing Stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Quelle SL Verschiebung Re-Änderung | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit Ereignisse Feuer | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Alle Live Tests oben **Überprüft grün gegen echte cTrader Demo-Konten** (1:1, 1:viele, Umkehrung, Cross-cID, Teilschließung, Ausstehend+Stornierung, Trailing).

Draht-Ergänzung in `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, Trailing Flag auf `AmendPositionSltpAsync`, Bestellung/Ausstehend Felder auf `ExecutionEvent`, `LoadSpotPriceAsync` (Spot Abonnement → Gebot/Angebot, verwendet durch Live Ausstehend/Trailing Tests zu Platz Ruhe Bestellungen weg von Markt), `StopLoss`/`TrailingStopLoss` auf `OpenPositionSnapshot` (Copy Trailing-Status beobachtbar via Abstimmung). Ziel Copies bleiben gekennzeichnet durch **Quell-Position-ID** (Ausstehend Copies durch Quelle **Bestellungs-ID**) daher Wiederverbindung Abstimmung bleibt ID-basiert, Duplicate nie Handel.

**cTrader Ereignis Gotcha (Überprüft Live):** Ruhe Ausstehend Bestellung `ORDER_ACCEPTED`/`ORDER_CANCELLED` Ausführungs-Ereignis trägt **nicht-offen `Position` Platzhalter** plus die `Order`. Strom muss klassifizieren es als *Bestellung* Ereignis **Bevor** Position Branche (bewacht auf Position nicht `OPEN`), sonst Ausstehend Platzierung Verlesung als Position Schließung. `SourceExecutionsAsync` tut dies; fehlend es Stille löscht alle Ausstehend Spiegelung.

## Token-Rotation + Knoten-Affinität

- **Rotation in laufend Hosts.** `CopyEngineSupervisor` Datensätze Token-Signatur auf jede laufend Host und, jeden Abstimmung, Neuaufbau Plan von DB (Frisch rotiert durch `OpenApiTokenRefreshService`). Geändert Signatur Neustart Host (`CopyHostTokenRotated`, 1062); Neu Host `ResyncAsync` Neuaufbau Status ohne Duplikat Trades. Force-Rotation Mid-Run via `IOpenApiTokenClient.RefreshAsync` zu Überprüfung Live Host trägt Kopieren fort.
- **Knoten-Affinität (kein Doppel-Copy).** Beide Web lokal Knoten und `CopyAgent` Worker laufen ein Supervisor. Jede laufend Profil Anspruch durch genau einer Knoten (`CopyProfile.AssignedNode`, Atomare `ExecuteUpdate` Anspruch Schlüssel aus `CopyOptions.NodeName`, Standard Maschinen-Name). Supervisor Hosts nur Profile es besitzt; Stopp/Pause gibt Anspruch frei. Abdeckung:
  - Domäne (Unit): `AssignToNode_makes_profile_hosted_by_only_that_node`, `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (echten Postgres, Testcontainer)**: `CopyNodeAffinityTests` fährt Supervisor echten `ClaimUnassignedProfilesAsync` — behauptet erster Knoten Anspruch alle 3 laufend Profile, zweiter Anspruch **0** (nein Doppel-Host), Pause→Neustart Freiheiten Anspruch für anderer Knoten.
  - Rotation Erkennung (`TokenRotationSignatureTests`): Supervisor `TokenSignature` ändert wenn Quelle oder Ziel Token rotiert, Stabil sonst (laufend Host Neustart nur auf echte Rotation).

### Einzel-Nutzungs-Aktualisierungs-Token (wichtig)

cTrader **Aktualisierungs-Token sind Einzel-Nutzungs** — jede Aktualisierung Rückkehr *Neu* Aktualisierungs-Token, ungültig macht Alt. Live Vorrichtung Aktualisierungs auf Start, persistiert rotiert Token zu `secrets/openapi-tokens.local.json`. Folgen:

- Wenn laufen Aktualisierungs aber **kann nicht persistieren** Neu Token (z. B. Lese-nur Montage), Cache Token tot, nächst laufen fehlschlag `ACCESS_DENIED`. Neu-generieren mit Headless Onboarding: `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` Schlucke Schreib-Misserfolg daher Lese-nur Cache nicht Crash laufen, aber **Live** In-Cluster-Suite noch braucht **schreibbar** Cache (K8s Job kopiert Secret zu emptyDir — siehe Bereitstellung Doc).

## Lauf die Suite in ein Kubernetes-Cluster

Ganze Suite läuft In-Cluster gegen Helm-Bereitgestellt App, daher Regressions gefangen In-Cluster gleich wie lokal. Siehe [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # Kind-Cluster, deterministisch Suite (keine Geheimnisse)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # Live
```

`Dockerfile.tests` Aufbau Runner Bild; Helm `tests-job.yaml` (Gated `tests.enabled=false`) läuft es gegen In-Cluster Postgres + Web. **Standard = deterministisch Copy-Suite** (keine Geheimnisse, nein rotiert Token). Für Live-Suite, set `tests.copySecret` zu Secret holdend Gitignore `openapi-*.local.json`; init-Container kopiert es zu **schreibbar** emptyDir bei `/app/secrets` (erforderlich — Einzel-Nutzungs Aktualisierungs-Token müssen sein persistierbar). Copy-Tests brauchen nur Web + Postgres + Token-Cache — nein privilegiert Knoten-Agenten. Skript behauptet Job Entzug 0 und Protokolle enthalten `Passed!`.

**Überprüft hier (Docker, nein Cluster):** Test Bild läuft deterministisch Suite (`101 bestand`) und, mit schreibbar `secrets/` Montage, voll **Live** Suite (`8 bestand`) — exakt Job Pfad Minus Kubernetes. `kind`/`kubectl`/`helm` nicht verfügbar in Verfassung Umwelt, daher vollständig `k8s-e2e.sh` Cluster laufen ist die ein Schritt nicht ausgeführt hier.

## Live Option-Matrix + Chaos (LiveCopyMatrix / LiveCopyChaos)

Zwei Daten-getrieben Live-Suiten bauen auf `LiveCopyScenario` / `LiveCopyFixture`, Live-Gegenstück zu deterministisch DST-Stress-Suite:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` Option-Matrix: ein echten Master offen pro Reihe gegen Demo-Konten, jede mit verschieden-konfiguriert Ziel, Behauptung golden Ergebnis. Reihen: `one_to_one`, `half_multiplier`, `reverse` (entgegengesetzt Seite), `manage_only` (öffnen nichts), `trading_hours_closed` (Fenster ausschließ jetzt → nein Copy), `source_label_block` (Label Filter → nein Copy), `lot_sanity_block` (Decke → nein Copy).
- **`LiveCopyChaos`** — Copy Engine gegen feindselig Start: Master bereits hält Position bevor Host startet, daher Konvergenz kann nur kommen von Start-up Resync (`Sync-Öffnung-on-Start`), plus negativ Fall mit Umschalter aus. Deterministisch Socket-Flap / Token-Rotation / Ablehnung Chaos abgedeckt erschöpfend durch DST-Suite; dies behauptet Live Resync Pfad.

Beide **Überspringung sauber** ohne Anmeldedaten, berichtet **Inconclusive** bei geschlossenen Markt, jede Reihe Bereinigung Positionen es öffnet (Demo-Konten nur).

**Vorrichtung-Robustheit:** `LiveCopyFixture` jetzt degradiert zu sauber Überspringung wenn Cache Tokens können nicht Aktualisierung (cTrader Aktualisierungs-Token Einzel-Nutzungs — Stale Cache oder Offline laufen nein Längere Fehler ganz Live Sammlung; Re-laufen OAuth Onboarding zu Aktualisierung Cache).

## Lauf die Live + Cluster-Suite sich selbst (KEINE Ausreden — alles ist Self-Serviceable)

Alles unten verfügbar in dieser Umwelt. Tun Sie **nicht** Anspruch "Anmeldedaten nicht verfügbar", "Token Stale", oder "kind/helm nicht installiert" — set sie oben und laufen sie.

### 1. Bekommen ein Frisch Open API Token (Headless OAuth, nein Browser Wechselwirkung)

cTrader Aktualisierungs-Token Einzel-Nutzungs, daher Cache Token WIRD Stale gehen. Re-Prägung es sich selbst von gesparte cID Benutzernamen/Passwort (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, oder einheitlich `secrets/dev-credentials.local.json`). Onboarding Test-Laufwerk **Headless Edge** OAuth via Playwright, schreib `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; Autorisiert jede cID, Cache Frisch Token. Re-laufen wann immer Live-Suite berichtet Vorrichtung nicht verfügbar wegen Aktualisierung Misserfolg.

### 2. Lauf die Live Copy-Suite (echten cTrader Demo-Konten)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # Kern Spiegelung (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # Option-Matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # Resync Chaos (2)
```

Platz + Bereinigung echten DEMO Bestellungen (nein Live-Konten), berichtet **Inconclusive** bei geschlossenen Markt. Überprüft grün von einem Ende zum anderen.

### 3. Bootstrap Token von ein laufend App Band (Alternativ)

Wenn App laufen + cID verlinkt In-App, Entschlüsseln Sie App neuesten Aktualisierungs-Token direkt aus `app-pg-data` Postgres-Band anstatt Re-Autorisierung — siehe `LiveTokenBootstrapTests`, set `CMIND_VOLUME_CONN`.

### 4. Kubernetes-Cluster E2E

`kind`, `helm`, Docker verfügbar (Installieren kind/helm via `go install`/Freigabe Binare oder `choco install kind kubernetes-helm` wenn nicht auf PFAD). Einmalig Skript baut+lädt Bilder, Bereitstellung Diagramm, läuft In-Cluster Test Job, behauptet Entzug 0:

```bash
scripts/k8s-e2e.sh                                 # Deterministisch Copy-Suite (keine Geheimnisse)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # Live In-Cluster
```

Siehe [../deployment/kubernetes.md](../deployment/kubernetes.md).
