---
description: "Režim Lab — oznaki a povračilo niz v Mirno / Normai / Turbulenten volatility režimi in poroča na-režim zmogljivost, plus na Hurst eksponent (trend-trajnost vs srednja-povračilo). Deterministična."
---

# Režim Lab

A ena Sharpe razmerje skrije resnico, ki je na večino robovi so pogojnega: velika v mirno, trend trge
in mrtav v turbulenci (ali obratno). Na Režim Lab prekiniti a strategija-ov zgodovina v volatility
režimi in kaže kako ga je naredil v vsak — tako znate *ko* tvoj rob dejansko deluje.

Odprite **cBots → Režim Lab** (`/quant/regimes`).

## Kaj naredi

Glede na a povračilo niz (ali kapitala krivulja, najstarejši prvi), ga:

- izračuna a **zaostaja realizacija volatility** na vsak točka in deli zgodovina v **Mirno**,
  **Normalno** in **Turbulenten** režimi z na tercile od ga volatility;
- poroča **na-režim zmogljivost** — opažanja, srednja povračilo, volatility in Sharpe — tako bi ste lahko videti
  kje na rob živi;
- oceni na **Hurst eksponent** preko ponovno-lestvico-razpon (R/S) analiza: nad ~0.55 na niz je
  **trend / trajno**, spodaj ~0.45 ga je **srednja-povračilo**, in okrog 0.5 ga je blizu a
  naključne sprehod.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // ali { "equity": [...] }
```

## Zakaj je zanesljivo

Čisti, deterministična domeni koda (`Core.Regimes`) z brez infrastrukture odvisnosti in brez zunanjih klicev
— enota-testirani za režim separacija (mirno vs turbulenten volatility) in za Hurst smer
(anti-trajno niz rezultat spodaj 0.5, a trajno trend rezultat nad). Na isti režim signal dovodijo
na avtonomna agenti-ov razmislek zanko, tako a agent lahko se opira v na režimi kje njegov rob je pravi.
