---
description: "Strategy Health & Alpha Decay — rilevamento decay deterministico che confronta lo Sharpe recente di una strategia con la sua record earlier e localizza il più grande mean-shift (CUSUM change-point), restituendo un verdetto Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Ogni edge decade — la ricerca è schietta che l'half-life di una strategia quant è crollata da anni a mesi,
quindi *adaptation beats discovery*. Il monitor Strategy Health ti dice, dalla propria storia dei rendimenti di
una strategia, se l'edge è ancora lì.

Apri **cBots → Strategy Health** (`/quant/health`).

## Cosa fa

Data una serie di rendimenti (o equity curve, oldest first):

- splitta la storia in una metà **earlier** e **recent** e confronta i loro Sharpe ratios;
- esegue una scansione **CUSUM change-point** per localizzare l'osservazione dove la media più chiaramente
  è shiftata (una rottura di regime), riportata solo quando la deviazione è statisticamente notable;
- restituisce un verdetto:

| Verdetto | Significato |
|---|---|
| **Healthy** | La performance recente è in linea con (o migliore di) la record earlier. |
| **Degrading** | Lo Sharpe recente è materialmente più debole della record earlier — watch closely. |
| **Decayed** | L'edge è effettivamente scomparso nella finestra recente — considera di mettere in pausa. |
| **Unknown** | Non abbastanza storia per giudicare. |

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Perché è affidabile

È codice domain puro, deterministico (`Core.Health`) senza dipendenza infrastructure e senza chiamate esterne
— unit-tested per i casi decayed, degrading, healthy e too-short e per la localizzazione change-point.
È il compagno manuale ai health checks always-on che supportano gli agenti autonomi: le stesse statistiche
guidano il circuit breaker che de-risks una strategia live il cui edge sta svanendo.
