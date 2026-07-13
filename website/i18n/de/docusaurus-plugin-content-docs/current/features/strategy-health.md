---
description: "Strategie-Gesundheit & Alpha-Verfall — deterministische Verfalls-Erkennung, die den aktuellen Sharpe einer Strategie mit seiner früheren Bilanz vergleicht und die größte Mittelwert-Verschiebung (CUSUM-Change-Point) lokalisiert, die ein Healthy / Degrading / Decayed-Urteil zurückgibt."
---

# Strategie-Gesundheit & Alpha-Verfall

Jede Edge zerfällt — die Forschung ist direkt, dass die Halbwertzeit einer quantitativen Strategie von Jahren zu Monaten kollabiert ist, also *Anpassung schlägt Entdeckung*. Der Strategie-Gesundheits-Monitor sagt Ihnen, aus einer Strategie's eigen Rückgaben-Geschichte, ob die Edge noch da ist.

Öffnen Sie **cBots → Strategy Health** (`/quant/health`).

## Was es tut

Gegeben eine Rückgaben-Serie (oder Eigenkapital-Kurve, älteste zuerst), tut sie:

- teilt die Geschichte in eine **frühere** und eine **aktuelle** Hälfte und vergleicht ihre Sharpe-Verhältnisse;
- führt einen **CUSUM-Change-Point**-Scan durch, um die Beobachtung zu lokalisieren, wo der Mittelwert sich am deutlichsten verschoben hat (ein Regime-Bruch), berichtet nur wenn die Abweichung statistisch bemerkenswert ist;
- gibt ein Urteil zurück:

| Urteil | Bedeutung |
|---|---|
| **Healthy** | Die aktuelle Leistung ist in line mit (oder besser als) der frühere Bilanz. |
| **Degrading** | Der aktuelle Sharpe ist wesentlich schwächer als der frühere Bilanz — beobachten Sie eng. |
| **Decayed** | Die Edge ist effektiv in der aktuellen Fenster verschwunden — erwägen Sie, zu pausieren. |
| **Unknown** | Nicht genug Geschichte zum Beurteilen. |

```http
POST /api/quant/health
{ "returns": [...] }   // oder { "equity": [...] }
```

## Warum es zuverlässig ist

Es ist reiner, deterministischer Domänen-Code (`Core.Health`) mit keiner Infrastruktur-Abhängigkeit und keinen externen Calls — Unit-getestet für die abgelösten, verschlechterten, gesunden und zu-kurzen Fälle und für Change-Point-Lokalisierung. Es ist der manuelle Begleiter zu den immer-an Gesundheits-Checks, die die autonomen Agenten unterstützen: die gleichen Statistiken treiben den Leistungsschalter an, der eine Live-Strategie deren Edge verblasst, de-risked.
