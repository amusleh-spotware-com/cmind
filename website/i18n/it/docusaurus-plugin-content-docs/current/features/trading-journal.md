---
description: "Trading Journal & Coach — analizza i tuoi run e backtest per behavioural leaks (over-concentration, repeated failures, a losing bias) e ti allena sulla strategia che hai già. Deterministic, with optional AI narrative."
---

# Trading Journal & Coach

La categoria più nuova genuinamente utile di AI-for-trading non sta prediciendo il mercato — sta analizzando
*il tuo proprio* comportamento. Il Trading Journal trasforma la tua storia di run e backtest in feedback
onesto così puoi migliorare la strategia che hai già.

Apri **AI → Trading Journal** (`/journal`).

## Cosa surfacce

Dai tuoi instance (run e backtest) computa, deterministicamente:

- **Win / loss / failure counts e win rate** attraverso i tuoi backtest;
- **Behavioural insights** — i leak che silenziosamente costano ai trader retail:
  - **Over-concentration** — la maggior parte della tua attività è in un simbolo;
  - **Repeated failures** — un alto share di run falliti nel build o configurazione;
  - **Losing bias** — più losing che winning backtest (con un nudge per eseguire l'Integrity Lab e
    verificare che l'edge è reale);
  - un clean bill of health quando nessuno dei precedenti si applica.

```http
GET /api/journal
```

## Perché è affidabile

L'analisi comportamentale è codice domain puro, deterministico (`Core.Journal`) senza dipendenza infrastructure
— unit-tested per over-concentration, repeated failures, losing bias, il caso bilanciato e l'account vuoto.
I fatti vengono prima; l'AI coach (Portfolio Digest) è un layer narrativo opzionale sopra,
gated sulla Anthropic API key, così il journal funziona completamente senza AI configurata.
