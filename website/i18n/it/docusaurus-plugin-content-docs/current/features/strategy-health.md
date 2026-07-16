---
description: "Strategy Health & Alpha Decay — rilevamento deterministico del decadimento che confronta lo Sharpe recente di una strategia con il suo precedente track record e localizza il più grande shift medio (change-point CUSUM), restituendo un verdetto Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Ogni edge decade — la ricerca è esplicita sul fatto che l'emivita di una strategia quant è crollata da anni a mesi, quindi *l'adattamento batte la scoperta*. Strategy Health ti dice, dalla cronologia dei rendimenti della strategia stessa, se l'edge è ancora lì.

Apri **cBots → Strategy Health** (`/quant/health`).

## What it does

Data una serie di rendimenti (o curva di equità, dalla più vecchia alla più recente), essa:

- divide la cronologia in una metà **precedente** e una **recente** e confronta i loro rapporti Sharpe;
- esegue una scansione **change-point CUSUM** per localizzare l'osservazione dove la media si è spostata più chiaramente (una rottura di regime), riportata solo quando la deviazione è statisticamente notevole;
- restituisce un verdetto:

| Verdetto | Significato |
|---|---|
| **Healthy** | Le prestazioni recenti sono in linea con (o migliori di) il track record precedente. |
| **Degrading** | Lo Sharpe recente è materialmente più debole del record precedente — osserva attentamente. |
| **Decayed** | L'edge è effettivamente scomparso nella finestra recente — considera di mettere in pausa. |
| **Unknown** | Non è disponibile una cronologia sufficiente per giudicare. |

- **Direttamente da una esecuzione di backtest — niente copia-incolla.** Ogni backtest completato espone un'icona di controllo **Check strategy health** nella riga dell'elenco **Backtest** e nella visualizzazione dettagli dell'istanza; un clic esegue il monitor sulla curva di equità memorizzata di quel run e mostra il verdetto in una finestra di dialogo. L'icona è disabilitata fino a quando il backtest non ha completato e prodotto un report, quindi non è mai un controllo morto. Dietro le quinte questo è `POST /api/quant/health/backtest/{instanceId}`, che legge la curva di equità del report memorizzato.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Why it is reliable

È codice di dominio puro e deterministico (`Core.Health`) senza dipendenza dall'infrastruttura e senza chiamate esterne — sottoposto a unit test per i casi decayed, degrading, healthy e too-short e per la localizzazione change-point. È il companion manuale ai controlli di salute always-on che supportano gli agenti autonomi: le stesse statistiche guidano il circuit breaker che de-rischia una strategia live il cui edge sta svanendo.
