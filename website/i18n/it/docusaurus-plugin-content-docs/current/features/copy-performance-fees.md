---
description: "Commissioni di performance dei money-manager su un high-water-mark, il modello standard di copy-trading (cTrader Copy, Darwinex, ZuluTrade profit-share): un provider addebbita…"
---

# Commissioni di performance del copy (Fase 4)

Commissioni di performance dei **money-manager su un high-water-mark**, il modello standard di copy-trading (cTrader Copy,
Darwinex, ZuluTrade profit-share): un provider addebbita una percentuale del *nuovo* profitto al di sopra del picco di equity di ogni follower —
mai sul bilancio di apertura, e mai due volte per il terreno già recuperato. **Opt-in** tramite
`App:Copy:FeesEnabled` (disattivato per impostazione predefinita).

## Il modello (high-water-mark)

Per destinazione (account follower), ogni regolamento:

1. **Primo regolamento** semina l'high-water-mark (HWM) all'equity attuale → nessuna commissione (un follower non
   viene mai addebitato sul suo deposito).
2. **Nuovo picco** (equity > HWM): `commissione = performanceFeePercent × (equity − HWM)`, quindi `HWM ← equity`.
3. **Al o sotto il picco**: nessuna commissione, HWM invariato — il follower deve prima recuperare oltre il vecchio picco, quindi
   non viene mai addebitato due volte per lo stesso guadagno.

L'aritmetica della commissione è un invariante di dominio su `CopyDestination.SettleFee(equity)` — l'aggregato lo possiede; il
servizio di regolamento fornisce solo l'equity sottoposto a polling e registra l'importo restituito. `PerformanceFee` è un
oggetto di valore limitato al 50% quindi una cattiva configurazione non può addebitare via tutto il guadagno di un follower.

## Come si regola

```
CopyFeeSettlementService (BackgroundService, solo quando FeesEnabled)
   │  ogni App:Copy:FeeSettlementInterval
   ├─ carica profili in esecuzione con una destinazione configurata con commissioni
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader apre una sessione,
   │                                               calcola bilancio + P&L fluttuante (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← logica HWM sull'aggregato
   └─ persisti HWM avanzato + allega CopyFeeAccrual (solo su un nuovo picco)
```

- `ICopyEquityReader` è un'astrazione Core; l'implementazione live (`OpenApiCopyEquityReader`) è l'unico
  pezzo infra — quindi la logica di regolamento + HWM viene esercitata nei test con un lettore fake, nessun broker live.
- `CopyFeeAccrual` è un log append-only (HWM-prima, equity, fee %, importo commissione, regolato-a) — un log dei fatti per
  il rapporto di commissioni e fatturazione, non un aggregato.

## Configurazione e API

| Impostazione `App:Copy` | Predefinito | Effetto |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Esegui il servizio di regolamento. |
| `FeeSettlementInterval` | `1h` | Quanto spesso l'equity viene sottoposto a polling e le commissioni regolate. |

Per-destinazione: `PerformanceFeePercent` (0–50) è impostato sulla destinazione (richiesta di aggiunta/modifica destinazione).

- `GET /api/copy/profiles/{id}/fees` — gli accantonamenti delle commissioni del profilo + totale addebitato.

## Test

- **Unità** (`CopyPerformanceFeeTests`) — l'invariante HWM: il primo regolamento semina + non addebbita nulla; un nuovo
  picco addebbita solo il guadagno al di sopra del picco; al/sotto il picco non addebbita nulla e il picco non retrocede mai;
  dopo un drawdown solo il recupero oltre il vecchio picco viene addebitato; lo 0% non addebbita mai; il VO rifiuta
  percenti fuori intervallo.
- **Integrazione** (`CopyFeeSettlementTests`, Postgres reale, lettore di equity fake) — seed→10k (no charge, mark
  seeded), 12k (addebita 400, mark avanza), 11k (no charge, mark mantenuto); accantonamento persistito con il
  proprietario/importo corretto.

L'host di copia non è toccato dalle commissioni (il regolamento è un lavoro DB separato), quindi la suite di stress DST di copia è
inalterata (23/23).
