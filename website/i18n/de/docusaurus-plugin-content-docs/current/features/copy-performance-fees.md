---
description: "Money-Manager-Performancegebühren auf High-Water-Mark, das Standard-Copy-Trading-Modell (cTrader Copy, Darwinex, ZuluTrade Gewinnbeteiligung): ein Provider berechnet…"
---

# Copy-Performancegebühren (Phase 4)

Money-Manager **Performancegebühren auf High-Water-Mark**, das Standard-Copy-Trading-Modell (cTrader Copy, Darwinex, ZuluTrade-Gewinnbeteiligung): Ein Anbieter berechnet einen Prozentsatz des *neuen* Gewinns über dem Spitzeneigenkapital jedes Followers — niemals auf dem eröffnenden Bilanz, und nie zweimal für bereits erholten Grund. **Opt-in** via `App:Copy:FeesEnabled` (Standard aus).

## Das Modell (High-Water-Mark)

Pro Ziel (Follower-Konto), jede Abrechnung:

1. **Erste Abrechnung** säht die High-Water-Mark (HWM) am aktuellen Eigenkapital → keine Gebühr (ein Follower wird nie auf ihre Einzahlung abgerechnet).
2. **Neue Höhe** (Eigenkapital > HWM): `Gebühr = PerformanceGebührProzent × (Eigenkapital − HWM)`, dann `HWM ← Eigenkapital`.
3. **Bei oder unter dem Peak**: keine Gebühr, HWM unverändert — der Follower muss zunächst über den alten Peak erholen, daher werden sie nie zweimal für die gleichen Gewinne berechnet.

Die Gebührenarithmetik ist eine Domain-Invariante auf `CopyDestination.SettleFee(equity)` — das Aggregate besitzt es; der Abservice liefert nur das abgerufene Eigenkapital und zeichnet die zurückgegebene Menge auf. `PerformanceFee` ist ein Value-Objekt, das auf 50% begrenzt ist, daher kann eine Fehlkonfiguration nicht den ganzen Gewinn eines Followers abrechnen.

## Wie es abgerechnet wird

```
CopyFeeSettlementService (BackgroundService, nur wenn FeesEnabled)
   │  jedes App:Copy:FeeSettlementInterval
   ├─ lade laufende Profile mit gebühren-konfiguriertem Ziel
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader öffnet eine Sitzung,
   │                                               berechnet Balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM Logik auf dem Aggregate
   └─ persistiere erweiterte HWM + hänge CopyFeeAccrual an (nur bei neuem High)
```

- `ICopyEquityReader` ist eine Core-Abstraktion; die Live-Implementierung (`OpenApiCopyEquityReader`) ist das einzige Infra-Stück — daher werden die Abrechnung + HWM-Logik in Tests mit einem Fake-Reader ausgeübt, kein Live-Broker.
- `CopyFeeAccrual` ist ein Append-only Log (HWM-vorher, Eigenkapital, Gebührenprozent, Gebührenbetrag, abgerechnet-bei) — ein Fakt-Log für den Gebührenbericht und Abrechnung, nicht ein Aggregate.

## Konfiguration & API

| `App:Copy` Einstellung | Standard | Effekt |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Führe Abservice aus. |
| `FeeSettlementInterval` | `1h` | Wie oft Eigenkapital abgerufen und Gebühren abgerechnet werden. |

Pro-Ziel: `PerformanceGebührProzent` (0–50) ist auf dem Ziel gesetzt (Ziel hinzufügen/bearbeiten Anfrage).

- `GET /api/copy/profiles/{id}/fees` — des Profils Gebühren-Abschreibungen + gesamt berechnet.

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — die HWM-Invariante: erste Abrechnung säht + berechnet nichts; eine neue Höhe berechnet nur den Gewinn über dem Peak; bei/unter dem Peak berechnet nichts und der Peak zieht sich nie zurück; nach einem Drawdown wird nur die Wiederherstellung über dem alten Peak berechnet; 0% berechnet nie; das VO lehnt Prozentsätze außerhalb des Bereichs ab.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, Fake-Eigenkapital-Reader) — Saat→10k (keine Gebühr, Marke besamt), 12k (berechnet 400, Marke fortgeschritten), 11k (keine Gebühr, Marke gehalten); Abschreibung persistiert mit richtigem Owner/Betrag.

Der Copy-Host ist von Gebühren unberührt (Abrechnung ist ein separater DB-Job), daher ist die Copy-DST-Stress-Suite unbeeinträchtigt (23/23).
