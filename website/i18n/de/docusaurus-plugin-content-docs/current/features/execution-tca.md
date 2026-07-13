---
description: "Transaktionkostenanalyse — misst Ausführungsqualität (Slippage in Basis-Punkten und Implementierungs-Shortfall) einer Order gegen ihren Ankunftspreis, die Verbund-Ausführungs-Edge, auf der Banken leben. Deterministisch."
---

# Transaktionkostenanalyse (TCA)

Ausführungs-Alpha ist winzig pro Trade und enorm über Tausende von ihnen — es ist ein großer Teil davon, wie Banken und Prop-Desks ihre Edge behalten. TCA misst, wie weit der Preis, den Sie tatsächlich erzielt haben, vom Preis abweicht, als Sie *entschieden* haben, zu handeln.

Öffnen Sie **cBots → Execution Cost** (`/quant/tca`).

## Was es misst

Gegeben den **Ankunftspreis (Entscheidungspreis)**, die **Seite** und Ihre **Fills** (Preis × Menge), werden gemeldet:

- **Durchschnittlicher Fill-Preis (VWAP)** — der volumengewichtete Preis, den Sie tatsächlich bekommen haben.
- **Slippage (bps)** — die Drift von Ankunft zu VWAP in Basis-Punkten, **signiert so, dass eine positive Zahl ein Kostenpreis ist** (Kauf über Ankunft oder Verkauf darunter) und eine negative Zahl Preis-Verbesserung ist.
- **Implementierungs-Shortfall** — dieser Kostenpreis in Preis × Mengen-Begriffen ausgedrückt: das Geld, das die Drift Sie in dieser Order kostet.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Intelligentes Slicing (Almgren-Chriss)

Jenseits der Messung von Kosten kann cMind eine große Order planen, um sie zu **minimieren**. **cBots → Execution Schedule** (`/quant/execution`) erstellt einen **Almgren-Chriss optimalen Ausführungs-Schedule**: gegeben die Gesamtmenge, eine Anzahl von Slices, Ihre Risikoaversion, Volatilität und temporärer Markt-Impact, gibt es die Größe an, in jedem Slice zu handeln. Höhere Risikoaversion **Front-Loads** den Schedule (Timing-Risiko-Schnitt); Null-Risikoaversion flacht zu einem gerade **TWAP** ab. Die Slices summieren sich immer zur Gesamtmenge.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Warum es zuverlässig ist

Pure, deterministische Domänen-Code (`Core.Execution`) mit keiner Infrastruktur-Abhängigkeit und keinen externen Calls — Unit-getestet für den Kauf/Verkauf-Kosten-Zeichen, Preis-Verbesserung, Null-Slippage, VWAP-Aggregation und Input-Wachen. Dies ist die Mess-Hälfte der Ausführungsqualität; es ist der gleiche Shortfall-Metriken, die die Copy-Engine verwendet, um den Kostenpreis der gespiegelten Orders zu beurteilen (und mit intelligenter Slicing zu reduzieren).
