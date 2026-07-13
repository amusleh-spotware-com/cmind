---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = In-Memory IOpenApiTradingSession alle Copy-Trading-Unit-Tests laufen gegen. Job: nachahmen **echten cTrader Open…"
---

# FakeTradingSession — cTrader Open API Treue-Vertrag

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = In-Memory `IOpenApiTradingSession` alle Copy-Trading-Unit-Tests laufen gegen. Job: nachahmen Sie **echten cTrader Open API Server** nah genug, dass Unit-Tests Verhalten nur Live-Tier verwendet zu fangen. Dieses Dokument = Treue-Vertrag: was Fake-Modelle, wie treu, und Regel, das es ehrlich halten.

> **Bindende Regel (CLAUDE.md):** Fake bleibt cTrader-treu. **Erweitern Sie es, schwächen Sie es nie**, um Test zu bestehen. Jedes neues echtes Verhalten Sie verlassen sich darauf bekommt hier modelliert, gepinnt durch Treue-Test.

## Treue-Matrix (F1–F13)

Verfolgt Plan `plans/copy-trading-overhaul.md` §7.6. Legende: ✅ Modelliert · ◑ Teils (Opt-in / Erweiterung) · ⬜ Noch nicht modelliert.

| # | Echtes Open API Verhalten | Fake-Status | Wie es modelliert wird |
|---|------------------------|-------------|-------------------|
| F1 | Markt-Bestellung kann **Teilfüllung** | ◑ | `PartialFillFractionForCtid[ctid] = f` füllt nur `f×Volume`; Abstimmung zeigt dann Lücke Phase‑1 Wahr‑auf (G5) schließt. Akzeptieren→Füllung Ereignis Paar noch zu kommen. |
| F2 | Volumen normalisiert zu **Schritt**, abgelehnt unter **Min** / über **Max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rundet ab zu Schritt, wirft `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Ungültig SL/TP** abgelehnt (Seite + Ziffern) | ⬜ | Geplant Phase 0a/1 (Paare mit M6 SL/TP Präzisions-Normalisierung). |
| F4 | Preise **Ganzzahl-skaliert nach Ziffern**; `pipPosition` | ◑ | `SymbolDetails` jetzt trägt `Digits` (und `MaxVolume`), gefüllt aus echtem Symbol; `PipPosition` Laufwerke Marktbereich Toleranz, `Digits` Laufwerke SL/TP Präzisions-Normalisierung (M6). Vollständig Ganzzahl-Preis-Skalierung noch ausstehend. |
| F5 | **Marktbereich** füllt nur wenn Spot innerhalb `Basis ± Slippage`, sonst ablehnen | ✅ | `IsMarketRangeRejected` vergleicht Live Spot (`SetSpot`) zu `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` Flag noch erzwingt ablehnen. |
| F6 | **Ausstehend Trigger→Füllung** Dual-Ereignis (Bestellung trägt `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reproduziert Füllung‑ausstehend Ereignis; FX‑Blue/cMAM Doppel‑Copy Dedupe abgedeckt in `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-getrieben Schließungen** (SL/TP Treffer, Stop-out) | ⬜ | Heute Schließungen Test-gepusht (`PushClose`); Preis-getrieben SL/TP-Treffer + Stop-out Schließungen geplant. |
| F8 | **Pro-Konto** Symbol-Tabellen / Details | ◑ | Symbol-Namen/IDs pro-Fake; pro-Konto Divergent-Tabellen (Cross-Broker) ausstehend. |
| F9 | Vollständig **Konten-Status** (Balance, Eigenkapital, Spielraum, frei Spielraum) | ◑ | `Balance` + `LoadPositionValuationsAsync` (Eintritt/Tausch/Provision via `SetPositionValuation`) + `SetSpot` Futter echten Eigenkapital in proportional-Eigenkapital-Größung (G2, Unit-Test in `CopyEquitySizingTests`). Verwendeter Spielraum nicht verfügbar gemacht von Abstimmungs-API, daher frei-Spielraum berichtet als Eigenkapital. |
| F10 | Ereignisse tragen **Server-Zeitstempel** | ✅ | `ExecutionEvent.ServerTimestamp` (Unix ms) — echte Sitzung liest von Geschäft `ExecutionTimestamp`; `PushOpen`/`PushPending` akzeptieren `serverTimestamp:` daher `FakeTimeProvider`-getrieben Test läuft echte Copy-Latenz (G1). |
| F11 | **Handels-Modus / Zeitplan** (Deaktiviert / Nur-Schließung / Geschlossen) | ⬜ | Geplant Phase 2b. |
| F12 | **Getypter Fehler-Taxonomie** (`ProtoOAErrorRes` Codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` wirft einmalig `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token-Ungültigerklärung** — Stale Token → Auth Fehler | ✅ | `InvalidateToken(ctid)` markiert Angebracht-Token Stale; Handels-Aufrufe werfen **echten** `OpenApiException` mit `OpenApiErrorKind.TokenInvalid` (Code `CH_ACCESS_TOKEN_INVALID`), exakt wie Live-Server, bis `SwapAccessTokenAsync` installiert Frisch-Token. Füttert M1 Token-Robustheit Test. |

Treue-Tests leben in `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, Standard bewahrt Legacy Verhalten

Jeder Treue-Knopf **Standard aus**, daher Fake einfach bleibt immer-füllen Verhalten für Tests die nicht Pflege. Test opt-in pro Konto:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (einmalig)
session.InvalidateToken(slave);                                             // F13
```

## Charakterisierung + Konformanz (Geplant, halten die Fake ≡ Echte)

Zwei Mechanismen halten Fake ehrlich gegen beweglich echten Server (verfolgt, Landung über Phase 0a):

1. **Live-Charakterisierung** (`LiveApiCharacterization`, Demo-Konten, Geheimnisse-Gated, `Inconclusive` bei geschlossenen Markt): fahren echte Open API, Datensatz exakt Draht-Wahrheit (Ereignis-Sequenzen, Skalierung, ablehnen Codes) in Golden-Vorrichtungen eingecheckt in Test-Projekt. Keine Geheimnisse in Vorrichtungen — nur beobachtet Formen.
2. **Konformanz-Geschirr**: laufen *gleich* Szenario-Suite zweimal — einmal gegen `FakeTradingSession`, einmal gegen Live-Sitzung (wenn Geheimnisse vorhanden) — behaupten identisch beobachtbar Ergebnisse. Echter Server ändert → Live-Schenkel fehlschlag → Update Fake. Dies macht "Unit-Tests Abdeckung alles" vertrauenswürdig.

Live-Anmeldedaten: `secrets/dev-credentials.local.json` (oder Legacy Split-Dateien) — siehe `docs/testing/dev-credentials.md`.
