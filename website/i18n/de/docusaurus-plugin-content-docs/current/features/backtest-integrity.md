---
description: "Backtest Integrity Lab — deterministische, Bankqualitäts-Überanpassungsstatistiken (Probabilistic & Deflated Sharpe, t-Statistik), die einen rohen Backtest in ein Robust / Fragile / Overfit Urteil umwandeln und für die Anzahl der versuchten Konfigurationen korrigieren."
---

# Backtest Integrity Lab

Einzelhandelplattformen zeigen dir die Sharpe oder den Nettogewinn eines Backtests und hören dort auf. Institutionen vertrauen einem rohen Backtest niemals — sie fragen, ob das Ergebnis **Korrektur für Selektionsbias und die Anzahl der versuchten Konfigurationen** überlebt. Das Backtest Integrity Lab bringt diese Überprüfung zu cMind. Es ist **deterministische Mathematik** (kein KI, keine externen Aufrufe), daher ist das Urteil reproduzierbar und jede Zahl ist erklärbar.

Öffne es unter **cBots → Integrity** (`/quant/integrity`).

## Was es berechnet

Bei einer Renditeserie (oder einer Equity/Balance-Kurve) und der Anzahl der Parametersätze, die du versucht hast, um sie zu erreichen, meldet der Analyzer:

- **Sharpe-Ratio** — pro Periode und annualisiert (Quadratwurzel der Zeit).
- **Probabilistic Sharpe Ratio (PSR)** — das Vertrauen, dass die *wahre* Sharpe die Benchmark schlägt, unter Berücksichtigung der Länge der Erfolgsbilanz, Schiefe und Kurtosis (Bailey & López de Prado, 2012). Ein kurzer oder fetter Auslauf senkt es.
- **Deflated Sharpe Ratio (DSR)** — PSR gemessen gegen eine **deflationierte Benchmark**: die Sharpe, die du vom *Besten von N zufälligen Versuchen* unter der Null (dem False Strategy Theorem) erwarten würdest. Je mehr Konfigurationen du versucht hast, desto höher ist die Messlatte — das ist, was Überanpassung aufdeckt.
- **t-Statistik** des durchschnittlichen Returns. Nach Harvey, Liu & Zhu sollte ein echter Vorteil **t ≥ 3,0** überwinden, nicht das Lehrbuch 2,0.
- **Schiefe / Kurtosis** der Returns, die die PSR/DSR-Korrektionen speisen.

## Das Urteil

| Urteil | Bedeutung | Regel |
|---|---|---|
| **Robust** | Der Vorteil überlebt die Versuche, die du durchgeführt hast. | DSR ≥ 95% **und** PSR ≥ 95% **und** \|t\| ≥ 3,0 |
| **Fragile** | Statistisch am Leben, aber nicht überzeugend — vergrößere dich nicht allein auf dieser Grundlage. | zwischen den beiden |
| **Overfit** | Höchstwahrscheinlich ein Artefakt von Selektionsbias, kein echter Vorteil. | DSR < 90% |

Jedes Ergebnis enthält eine Begründung in klarer Sprache, sodass das "Warum" nie verborgen ist.

## Probability of Backtest Overfitting (across trials)

Die Eingabe eines Trial *Count* ist gut; die Eingabe der **tatsächlichen Out-of-Sample-Serie jeder Konfiguration, die du versucht hast** ist besser. Füge sie in das optionale **Trial-Raster** ein (eine Serie pro Zeile) und cMind führt **Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015) durch: Es teilt die Beobachtungen in Gruppen auf und für jede Möglichkeit, die Hälfte als In-Sample zu wählen, wählt es die beste In-Sample-Konfiguration aus und prüft, ob dieser Gewinner in der Bottom-Hälfte **Out-of-Sample** landet. Die **Probability of Backtest Overfitting (PBO)** ist der Anteil der Splits, bei denen der Gewinner nicht verallgemeinert werden konnte. Eine PBO nahe 0 bedeutet, dass die beste Konfiguration wirklich die beste ist; eine PBO von 0,5 oder mehr bedeutet, dass dein Auswahlprozess Rauschen aufgreift — das Urteil wird unabhängig davon, wie gut der Gewinner aussah, **Overfit**.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Wenn der native cTrader Console Optimizer ankommt, wird cMind seine vollständige Trial-Oberfläche hier automatisch einspeisen.

## Trials — die Zahl, die zählt

`Trials` ist **wie viele Parametersätze du getestet hast**, bevor du diesen ausgewählt hast. Das Testen einer Strategie und das Testen von zehntausend und die Beibehaltung der besten sind völlig unterschiedliche Dinge: Das zweite erzeugt eine hohe In-Sample-Sharpe zufällig. Die Eingabe der ehrlichen Versuchszahl ist der ganze Sinn — sie erhöht die Deflation und kann einen "großartigen" Backtest zu **Overfit** bewegen. Wenn der native cTrader Console Optimizer ankommt, speist cMind automatisch die echte Rastergröße des Sweep ein.

## Inputs

- **Periodische Returns** — eine Zahl pro Periode (z. B. `0.01` = +1%). Mindestens zwei. Das Feld wird bei der Eingabe überprüft: Es zählt die gültigen Zahlen, kennzeichnet alle Token, die keine Zahlen sind, und aktiviert **Analyze** nur, sobald mindestens zwei saubere Werte vorhanden sind (das Trial-Raster aktiviert **Assess overfitting**, sobald zwei Serien mit vier oder mehr Zahlen bereit sind).
- **Equity / Balance-Kurve** — cMind leitet die aufeinanderfolgenden einfachen Returns für dich ab.
- **Direkt aus einem Backtest-Lauf — kein Copy-Paste.** Jeder abgeschlossene Backtest zeigt ein Shield **Check backtest integrity** Symbol auf der **Backtest**-Listenzeile und auf seiner Instance-Detail-Ansicht; ein Klick führt das Labor auf dieser Laufs gespeicherten Equity-Kurve aus und zeigt das Urteil in einem Dialog. Das Symbol ist deaktiviert, bis der Backtest abgeschlossen und ein Bericht erstellt wurde, daher ist es nie ein totes Steuerelement. Im Hintergrund ist dies `POST /api/quant/integrity/backtest/{instanceId}`, das die Equity-Kurve des gespeicherten Berichts liest.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Gibt das Urteil, alle Metriken und die Begründung zurück. `POST /api/quant/integrity/backtest/{id}` führt die gleiche Analyse auf einem abgeschlossenen Backtest durch, den du besitzt.

## Warum es zuverlässig ist

Die Statistiken sind pure Funktionen im Domain-Kern (`Core.Quant`) ohne Infrastruktur-Abhängigkeiten — sie können nicht durch einen Netzwerkhiccup ausfallen, und sie sind durch Golden-Vector-Unit-Tests gegen die veröffentlichten Formeln verankert. Die normale CDF/Inverse sind geschlossene Näherungsformeln (Abramowitz-Stegun / Acklam), daher führen die gleichen Eingaben immer zum gleichen Urteil.
