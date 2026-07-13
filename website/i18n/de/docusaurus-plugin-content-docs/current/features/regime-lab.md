---
description: "Regime Lab — beschriftet eine Rückgaben-Serie in Calm / Normal / Turbulent-Volatilitäts-Regime und meldet Pro-Regime-Leistung, plus den Hurst-Exponent (Trend-Persistenz vs Mean-Reversion). Deterministisch."
---

# Regime Lab

Ein einziges Sharpe-Verhältnis verbirgt die Wahrheit, dass die meisten Edges bedingt sind: großartig in ruhigen, trendenden Märkten und tot in Turbulenzen (oder umgekehrt). Das Regime Lab zerlegt die Geschichte einer Strategie in Volatilitäts-Regime und zeigt, wie sie in jedem war — sodass Sie wissen, *wann* Ihre Edge tatsächlich funktioniert.

Öffnen Sie **cBots → Regime Lab** (`/quant/regimes`).

## Was es tut

Gegeben eine Rückgaben-Serie (oder Eigenkapital-Kurve, älteste zuerst), tut sie:

- berechnet eine **Trailing realisierte Volatilität** bei jedem Punkt und teilt die Geschichte in **Calm**, **Normal** und **Turbulent**-Regime durch die Drittel dieser Volatilität;
- meldet **Pro-Regime-Leistung** — Beobachtungen, Mittelwert-Rückgabe, Volatilität und Sharpe — sodass Sie sehen können, wo die Edge lebt;
- schätzt den **Hurst-Exponent** via Rescaled-Range (R/S)-Analyse: über ~0.55 die Serie ist **trendierend / persistent**, unter ~0.45 ist es **Mean-Reverting**, und um 0.5 herum ist es nah bei einem Random-Walk.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // oder { "equity": [...] }
```

## Warum es zuverlässig ist

Pure, deterministische Domänen-Code (`Core.Regimes`) mit keiner Infrastruktur-Abhängigkeit und keinen externen Calls — Unit-getestet für Regime-Separation (Calm vs Turbulent-Volatilität) und für Hurst-Richtung (Anti-Persistent-Serie Scores unter 0.5, eine Persistent-Trend-Scores über). Das gleiche Regime-Signal speist die autonomen Agenten's Reflexions-Schleife, sodass ein Agent sich in die Regime lehnen kann, wo seine Edge wirklich ist.
