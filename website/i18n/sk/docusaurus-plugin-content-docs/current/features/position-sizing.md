---
description: "Inštitucionálne position sizing pre retail — volatility targeting a fractional-Kelly expozícia pre jednu stratégiu, plus inverse-volatility risk-parity alokácia s korelačnou maticou naprieč knihou stratégií."
---

# Position Sizing & Portfolio

"Ako veľká by mala byť táto obchod?" je otázka, ktorá rozhoduje, či sa výhoda zloží alebo vybuchne.
Inštitúcie na ňu odpovedajú **volatility targeting** a **Kelly criterion**, a budujú knihu s
**risk parity** namiesto rovnakých dolárov. cMind prináša oboje retailu — deterministická matematika na
sérii výnosov stratégie, s plain-English odporúčaním.

Otvorte **cBots → Position Sizing** (`/quant/sizing`).

## Sizing jednej stratégie

Pri danej sérii výnosov stratégie (alebo equity curve), cieľovej anualizovanej volatilite, Kelly frakcii a
leverage cap, sizer hlási:

- **Realizovaná anualizovaná volatilita** — vlastná volatilita stratégie, anualizovaná pravidlom odmocniny z času.
- **Volatility-target sizing** — expozícia, ktorá robí realizovanú volatilitu rovnú vášmu cieľu
  (`target ÷ realized vol`), capped na váš leverage limit. Nižšie-volatile stratégie získavajú viac veľkosti.
- **Full Kelly** — growth-optimálna frakcia `f* = μ / σ²` (priemer nad rozptylom výnosov).
- **Fractional Kelly** — `f*` škálované vašou Kelly frakciou. Half-Kelly (0.5) je bežná bezpečná voľba;
  full Kelly jefamózne príliš agresívny pre reálne, neisté výhody.
- **Odporúčaná expozícia** — **menšia** (bezpečnejšia) z volatility-target a fractional-Kelly
  sizings, capped. Stratégia bez pozitívnej výhody (full Kelly ≤ 0) je sized na **nulu**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio alokácia

Dajte mu dve alebo viac stratégií (aligned return series) a postaví knihu **inverse-volatility
risk parity** — každá stratégia vážená `1 / volatility`, normalizovaná — takže risk, nie doláre, sú zdieľané
rovnomerne. Vráti aj:

- **korelačnú maticu** naprieč vašimi stratégiami (nájdite tie, čo sú tajne rovnaký stávka);
- **projektovanú portfolio volatilitu** pri týchto váhach, zo sample covariance;
- **leverage** faktor, ktorý škáluje celú knihu smerom k vašej cieľovej volatilite (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Prečo je spoľahlivý

Všetko je čistý, deterministický doménový kód (`Core.Portfolio`) bez infraštruktúrnej závislosti a bez
externých volaní — unit-testovaný pre vol-target scaling, Kelly formulu, equal-risk property of
inverse-volatility weights a korelačnú maticu. Advisory predvolene: čísla sú odporúčanie, nikdy
automatická objednávka.
