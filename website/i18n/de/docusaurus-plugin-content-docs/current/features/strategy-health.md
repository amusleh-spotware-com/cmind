---
description: "Strategy Health & Alpha Decay — deterministische Decay-Erkennung, die den neuesten Sharpe einer Strategie mit ihrer früheren Bilanz vergleicht und die größte Mittelwertverschiebung (CUSUM Change-Point) lokalisiert, um ein Urteil Healthy / Degrading / Decayed zurückzugeben."
---

# Strategy Health & Alpha Decay

Jeder Edge zerfällt – die Forschung ist deutlich, dass sich die Halbwertszeit einer Quant-Strategie von Jahren auf Monate verkürzt hat, daher *schlägt Anpassung Entdeckung*. Der Strategy-Health-Monitor zeigt dir anhand der Renditehistorie einer Strategie, ob der Edge noch vorhanden ist.

Öffne **cBots → Strategy Health** (`/quant/health`).

## Was es tut

Gegeben eine Renditeserie (oder Eigenkapitalkurve, chronologisch), funktioniert es so:

- teilt die Historie in eine **frühere** und eine **neuere** Hälfte auf und vergleicht deren Sharpe-Verhältnisse;
- führt einen **CUSUM-Change-Point-Scan** durch, um die Beobachtung zu lokalisieren, bei der sich der Mittelwert am deutlichsten verschoben hat (ein Regime-Wechsel), berichtet nur, wenn die Abweichung statistisch bemerkenswert ist;
- gibt ein Urteil zurück:

| Urteil | Bedeutung |
|---|---|
| **Healthy** | Die aktuelle Leistung steht im Einklang mit (oder ist besser als) der früheren Bilanz. |
| **Degrading** | Der neuere Sharpe ist wesentlich schwächer als der frühere Wert – beobachte genau. |
| **Decayed** | Der Edge ist im neueren Zeitfenster faktisch verschwunden – ziehe ein Pausieren in Betracht. |
| **Unknown** | Nicht genug Historie zum Bewerten. |

- **Direkt aus einem Backtest-Durchlauf – ohne Copy-Paste.** Jeder abgeschlossene Backtest zeigt ein Herz-Symbol **Check strategy health** auf der **Backtest**-Listen-Zeile und in seiner Instanz-Detail-Ansicht an; ein Klick führt den Monitor auf der gespeicherten Eigenkapitalkurve des Durchlaufs aus und zeigt das Urteil in einem Dialog. Das Symbol ist deaktiviert, bis der Backtest abgeschlossen ist und einen Bericht erzeugt hat, daher ist es nie eine tote Steuerung. Dahinter steht `POST /api/quant/health/backtest/{instanceId}`, das die Eigenkapitalkurve des gespeicherten Berichts ausliest.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Warum es zuverlässig ist

Es ist reiner, deterministischer Domain-Code (`Core.Health`) ohne Infrastruktur-Abhängigkeit und ohne externe Aufrufe – getestet für die Fälle Decayed, Degrading, Healthy und zu-kurze-Historie sowie für Change-Point-Lokalisierung. Es ist der manuelle Begleiter zu den immer aktiven Health-Checks, die die autonomen Agenten sichern: die gleiche Statistik treibt den Circuit Breaker an, der eine Live-Strategie entwässert, deren Edge schwächer wird.
