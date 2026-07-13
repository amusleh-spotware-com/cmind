---
description: "Institucionalno dimensioniranje pozicija za retail — volatilnost targeting i frakcioni Kelly exposure za jednu strategiju, plus alokacija sa inverznom volatilnošću risk-parity preko knjige strategija."
---

# Veličina pozicije i portfolio

„Koliko velika treba da bude ova trgovina?" je pitanje koje odlučuje da li se edge kompounduje ili eksplodira.
Institucije odgovaraju sa **volatilnost targeting** i **Kelly kriterijumom**, i grade knjigu sa
**risk parity** umesto jednakih dolara. cMind donosi oba u retail — determinističku matematiku na
return series strategije, sa plain-English preporukom.

Otvorite **cBots → Position Sizing** (`/quant/sizing`).

## Dimensioniranje jedne strategije

S obzirom na strategy returns (ili equity curve), target annual volatilnost, Kelly frakciju i
leverage cap, sizer raportira:

- **Realizovana godišnja volatilnost** — strategijina sopstvena volatilnost, godišnje prilagođena square-root-of-time
  pravilom.
- **Volatilnost-target dimensioniranje** — exposure koji čini realizovanu volatilnost jednakoj vašoj target
  (`target ÷ realized vol`), ograničen leverage limitom. Niža-vol strategija dobija više veličine.
- **Full Kelly** — growth-optimalna frakcija `f* = μ / σ²` (srednja / varijansa returns-a).
- **Frakcioni Kelly** — `f*` skaliran Kelly frakcijom. Half-Kelly (0.5) je uobičajeni siguran izbor;
  full Kelly je čuveno preagresivan za prave, neizvesne edge-ove.
- **Preporučeni exposure** — **manji** (sigurniji) od volatilnost-target i frakcioni Kelly
  dimensioniranja, ograničen. Strategija bez pozitivnog edge-a (full Kelly ≤ 0) je dimenzionisana na **nulu**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio alokacija

Dajte dve ili više strategija (poravnate return series) i on gradi knjigu sa **inverznom-volatlnošću
risk parity** — svaka strategija je weight-ovana sa `1 / volatility`, normalizovano — tako da se risk, ne dolari, deli
ravnomerno. Takođe vraća:

- **korelacionu matricu** preko strategija (uočite koje su zapravo ista opklada);
- **projektovanu portfolio volatilnost** na tim težinama, iz sample covariance;
- **leverage** faktor koji skalira celu knjigu ka vašoj target volatilnosti (ograničen).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Zašto je pouzdano

Sve je to čist, deterministički domen kod (`Core.Portfolio`) bez infrastrukture i bez
eksternih poziva — unit-testiran za vol-target skaliranje, Kelly formulu, equal-risk svojstvo
inverznо-volatility težina, i korelacionu matricu. Advisory podrazumevano: brojevi su
preporuka, nikad automatska narudžbina.
