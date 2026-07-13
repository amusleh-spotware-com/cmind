---
description: "Regime Lab — labelizza una serie di rendimenti in regimi di volatilità Calm / Normal / Turbulent e riporta la performance per-regime, più l'esponente di Hurst (trend-persistence vs mean-reversion). Deterministic."
---

# Regime Lab

Un singolo Sharpe ratio nasconde la verità che la maggior parte degli edge sono condizionali: fantastici in
mercati calm, trending e morti in turbolenza (o il contrario). Il Regime Lab rompe la storia di una
strategia in regimi di volatilità e mostra come ha performato in ciascuno — così sai *quando* il tuo edge
effettivamente funziona.

Apri **cBots → Regime Lab** (`/quant/regimes`).

## Cosa fa

Data una serie di rendimenti (o equity curve, oldest first):

- computa una **trailing realized volatility** a ogni punto e splitta la storia in regimi **Calm**,
  **Normal** e **Turbulent** per i terzili di quella volatilità;
- riporta **performance per-regime** — osservazioni, mean return, volatilità e Sharpe — così puoi vedere
  dove vive l'edge;
- stima l'**Hurst exponent** via rescaled-range (R/S) analysis: sopra ~0.55 la serie è
  **trending / persistent**, sotto ~0.45 è **mean-reverting**, e attorno a 0.5 è vicina a un random walk.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // or { "equity": [...] }
```

## Perché è affidabile

Codice domain puro, deterministico (`Core.Regimes`) senza dipendenza infrastructure e senza chiamate esterne
— unit-tested per la separazione dei regimi (calm vs turbulent volatility) e per la direzione Hurst
(una serie anti-persistent segna sotto 0.5, un trend persistent segna sopra). Lo stesso segnale regime
alimenta il loop di reflection degli agenti autonomi, così un agente può inclinarsi nei regimi dove il suo
edge è reale.
