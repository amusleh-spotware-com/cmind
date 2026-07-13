---
description: "Institutionelle Positionsgröße für Retail — Volatilitäts-Targeting und Fractional-Kelly-Exposition für eine einzelne Strategie, plus Inverse-Volatilität Risk-Parity-Allokation mit einer Korrelations-Matrix über ein Buch von Strategien."
---

# Positionsgröße & Portfolio

"Wie groß sollte dieser Trade sein?" ist die Frage, die entscheidet, ob eine Edge sich verbindet oder explodiert. Institutionen beantworten sie mit **Volatilitäts-Targeting** und dem **Kelly-Kriterium**, und sie erstellen ein Buch mit **Risk Parity** statt gleicher Dollars. cMind bringt beides zu Retail — deterministische Mathematik auf die Rückgabe-Serie einer Strategie, mit einer Klartext-Empfehlung.

Öffnen Sie **cBots → Position Sizing** (`/quant/sizing`).

## Single-Strategy-Sizing

Gegeben die Rückgaben einer Strategie (oder die Eigenkapital-Kurve), eine Ziel-Jahres-Volatilität, eine Kelly-Fraktion und eine Hebelwirkungs-Obergrenze, meldet der Sizer:

- **Realisierte Jahres-Volatilität** — die Volatilität der eigenen Strategie, annualisiert durch die Quadratwurzel-der-Zeit-Regel.
- **Volatilitäts-Ziel-Sizing** — die Exposition, die realisierte Volatilität mit Ihrem Ziel erfüllt (`target ÷ realized vol`), oben bei Ihrer Hebelwirkung-Limit. Niedrigere-Vol-Strategien erhalten mehr Größe.
- **Full Kelly** — der wachstums-optimale Anteil `f* = μ / σ²` (Mittelwert über Varianz der Rückgaben).
- **Fractional Kelly** — `f*` skaliert durch Ihre Kelly-Fraktion. Half-Kelly (0.5) ist die häufige sichere Wahl; Full Kelly ist berüchtigt zu aggressiv für echte, ungewisse Edges.
- **Empfohlene Exposition** — der **kleinere** (sicherer) von den Volatilitäts-Ziel und Fractional-Kelly-Sizings, oben. Eine Strategie mit keiner positiven Edge (Full Kelly ≤ 0) wird auf **null** groß gemacht.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio-Allokation

Geben Sie ihr zwei oder mehr Strategien (ausgerichtete Rückgaben-Serie) und es erstellt ein Buch durch **Inverse-Volatilität Risk Parity** — jede Strategie gewichtet durch `1 / volatility`, normalisiert — sodass Risiko, nicht Dollars, gleichmäßig geteilt wird. Es gibt auch zurück:

- die **Korrelations-Matrix** über Ihre Strategien (spot die, die heimlich die gleiche Wette sind);
- die **Projizierte Portfolio-Volatilität** bei diesen Gewichten, aus der Sample-Covariance;
- ein **Hebelwirkung**-Faktor, der das ganze Buch gegen Ihre Ziel-Volatilität skaliert (oben).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Warum es zuverlässig ist

All diese sind rein, deterministische Domänen-Code (`Core.Portfolio`) mit keiner Infrastruktur-Abhängigkeit und keinen externen Calls — Unit-getestet für das Vol-Ziel-Skalierung, die Kelly-Formel, die Gleich-Risiko-Eigenschaft von Inverse-Volatilität-Gewichten und die Korrelations-Matrix. Advisory standardmäßig: die Zahlen sind eine Empfehlung, nie ein automatischer Order.
