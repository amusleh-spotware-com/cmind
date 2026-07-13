---
description: "Backtest-Integritäts-Labor — deterministische, institutionelle Überanpassungs-Statistiken (Probabilistic & Deflated Sharpe, t-stat), die einen Raw-Backtest in ein Robust / Fragile / Overfit-Urteil umwandeln, korrigiert für wie viele Konfigurationen Sie versucht haben."
---

# Backtest-Integritäts-Labor

Retail-Plattformen zeigen Ihnen die Sharpe oder den Nettogewinn eines Backtests und hören dort auf. Institutionen vertrauen nie einem Raw-Backtest — sie fragen, ob das Ergebnis **korrigiert für Selektionsbias und die Anzahl der versucht Konfigurationen** überlebt. Das Backtest-Integritäts-Labor bringt diese Überprüfung zu cMind. Es ist **deterministische Mathematik** (keine KI, keine externen Calls), sodass das Urteil reproduzierbar ist und jede Zahl erklärbar.

Öffnen Sie es unter **cBots → Integrity** (`/quant/integrity`).

## Was es berechnet

Gegeben eine Rückgabeserie (oder eine Eigenkapital-/Saldokurve) und die Anzahl der Parameter-Sätze, die Sie versucht haben, um sie zu erreichen, meldet der Analyzer:

- **Sharpe-Verhältnis** — pro Periode und annualisiert (Quadratwurzel-der-Zeit).
- **Probabilistic Sharpe Ratio (PSR)** — das Vertrauen, dass das *echte* Sharpe die Benchmark übertrifft, Kontenführung für Track-Record-Länge, Schiefe und Kurtose (Bailey & López de Prado, 2012). Ein kurzer oder fetter-tailed Record senkt es.
- **Deflated Sharpe Ratio (DSR)** — PSR gemessen gegen eine **abgeflachte Benchmark**: das Sharpe, das Sie von dem *besten der N zufälligen Versuche* unter Null erwarten würden (die False-Strategy-Theorem). Je mehr Konfigurationen Sie versucht haben, desto höher die Messlatte — dies ist das, was Überanpassung fängt.
- **t-Statistik** des mittleren Rückgabe. Nach Harvey, Liu & Zhu sollte ein echter Vorteil **t ≥ 3.0** löschen, nicht das Lehrbuch 2.0.
- **Schiefe / Kurtose** der Rückgaben, die die PSR/DSR-Korrektionen speisen.

## Das Urteil

| Urteil | Bedeutung | Regel |
|---|---|---|
| **Robust** | Der Vorteil überlebt die Versuche, die Sie durchgeführt haben. | DSR ≥ 95% **und** PSR ≥ 95% **und** \|t\| ≥ 3.0 |
| **Fragil** | Statistisch lebendig, aber nicht überzeugend — keine Vergrößerung allein auf dieser Basis. | zwischen den beiden |
| **Überanpassung** | Wahrscheinlich ein Artefakt von Selektionsbias, nicht ein echter Vorteil. | DSR < 90% |

Jedes Ergebnis trägt eine Klartext-Begründung, so dass das "Warum" nie verborgen ist.

## Wahrscheinlichkeit der Backtest-Überanpassung (über Versuche)

Das Füttern eines Versuch-*Zählers* ist gut; das Füttern der **echten Out-of-Sample-Serie jeder Konfiguration, die Sie versucht haben** ist besser. Fügen Sie sie in das optionale **Trial-Grid** ein (eine Serie pro Zeile) und cMind führt **Kombinatorisch-Symmetrische Cross-Validierung** aus (Bailey, Borwein, López de Prado & Zhu, 2015): Es teilt die Beobachtungen in Gruppen auf und wählt für alle Arten, die Hälfte als In-Sample zu wählen, die In-Sample-beste Konfiguration aus und prüft, ob dieser Gewinner in der **Out-of-Sample**-Hälfte landet. Die **Wahrscheinlichkeit der Backtest-Überanpassung (PBO)** ist der Anteil der Splits, bei denen der Gewinner nicht verallgemeinert wurde. Ein PBO nahe 0 bedeutet, dass die beste Konfiguration wirklich am besten ist; ein PBO von 0,5 oder mehr bedeutet, dass Ihr Auswahlprozess Rauschen auswählt — das Urteil wird **Überanpassung** unabhängig davon, wie gut der Gewinner aussah.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Wenn der native cTrader Console Optimizer landet, wird cMind seine vollständige Trial-Oberfläche hier automatisch füttern.

## Versuche — die Zahl, die zählt

`Trials` ist **wie viele Parameter-Sätze Sie getestet haben**, bevor Sie diesen gewählt haben. Das Testen einer Strategie und das Testen von zehntausend und das Halten des besten sind wildly unterschiedliche Dinge: das zweite produziert ein hohes In-Sample-Sharpe rein zufällig. Das ehrliche Trial-Zählen zu füttern ist der ganze Punkt — es hebt die Deflation an und kann einen "großartigen" Backtest zu **Überanpassung** verschieben. Wenn der native cTrader Console Optimizer landet, füttert cMind die echte Rastergröße des Sweeps automatisch.

## Eingaben

- **Periodische Rückgaben** — eine Zahl pro Periode (z. B. `0.01` = +1%). Mindestens zwei.
- **Eigenkapital- / Saldokurve** — cMind leitet die aufeinanderfolgenden einfachen Rückgaben für Sie ab.
- Oder führen Sie es direkt auf einem abgeschlossenen Backtest aus: `POST /api/quant/integrity/backtest/{instanceId}` liest die gespeicherte Berichtskurve.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Gibt das Urteil, alle Metriken und die Begründung zurück. `POST /api/quant/integrity/backtest/{id}` führt die gleiche Analyse auf einem abgeschlossenen Backtest aus, den Sie besitzen.

## Warum es zuverlässig ist

Die Statistiken sind pure Funktionen im Domain-Core (`Core.Quant`) mit null Infrastruktur-Abhängigkeiten — sie können von einem Netzwerk-Flop nicht heruntergenommen werden, und sie werden durch Golden-Vector-Unit-Tests gegen die veröffentlichten Formeln angeheftet. Der normale CDF/inverse sind geschlossene Näherungen (Abramowitz-Stegun / Acklam), sodass die gleichen Eingaben immer das gleiche Urteil liefern.
