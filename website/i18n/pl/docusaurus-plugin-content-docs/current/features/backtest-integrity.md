---
description: "Backtest Integrity Lab — deterministyczne, fund-grade statystyki overfitting (Probabilistic & Deflated Sharpe, t-stat) które zamieniają surowy backtest w Robust / Fragile / Overfit verdict, korygując ile konfiguracji spróbowałeś."
---

# Backtest Integrity Lab

Platformy detaliczne pokazują Ci Sharpe lub zysk netto backtestu i tu się kończą. Instytucje nigdy nie ufają
surowemu backtest — pytają czy wynik przetrwa **korekcję bias wyboru i liczbę spróbowanych konfiguracji**. Backtest Integrity Lab
przynosi tę kontrolę do cMind. To **deterministyczne math** (nie AI, nie zewnętrzne wywołania), więc
verdict jest powtarzalny i każda liczba jest wyjaśnialna.

Otwórz na **cBots → Integrity** (`/quant/integrity`).

## Co oblicza

Biorąc serię zwrotów (lub krzywą equity/balance) i liczbę param setów którą spróbowałeś aby do tego dojść, analizator
raportuje:

- **Sharpe ratio** — per-period i annualizowany (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — pewność że *prawdziwy* Sharpe bije benchmark,
  biorąc pod uwagę długość track-record, skewness i kurtosis (Bailey & López de Prado, 2012). Krótki lub
  fat-tailed record go obniża.
- **Deflated Sharpe Ratio (DSR)** — PSR zmierzony przeciw **deflated benchmark**: Sharpe które
  spodziewałbyś się z *best of N random trials* pod null (the False Strategy Theorem). Im więcej
  konfiguracji spróbowałeś, tym wyższy bar — to co łapie overfitting.
- **t-statistic** średniej zwrotu. Idąc za Harvey, Liu & Zhu, rzeczywista edge powinna przejść **t ≥ 3.0**,
  nie textbook 2.0.
- **Skewness / kurtosis** zwrotów, które zasilają korekcje PSR/DSR.

## Verdict

| Verdict | Znaczenie | Reguła |
|---|---|---|
| **Robust** | Edge przetrwa trial'e które uruchomiłeś. | DSR ≥ 95% **i** PSR ≥ 95% **i** \|t\| ≥ 3.0 |
| **Fragile** | Statystycznie żywy ale nie przekonywająco — nie sizuj w górę na tym samym. | między nimi |
| **Overfit** | Najprawdopodobniej artifact bias wyboru, nie rzeczywista edge. | DSR < 90% |

Każdy wynik nosi plain-English rationale więc "dlaczego" nigdy nie jest ukryte.

## Probability of Backtest Overfitting (między trial'ami)

Zasilanie trial *count* jest dobre; zasilanie **rzeczywistej out-of-sample serii każdej konfiguracji którą
spróbowałeś** jest lepsze. Wklej je do opcjonalnej **trial grid** (jeden series per line) i cMind uruchamia
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): dzieli
obserwacje w grupy, i dla każdego sposobu wyboru połowy as in-sample bierze in-sample
best configuration i sprawdza czy ten zwycięzca ląduje w bottom half **out-of-sample**. **Probability of Backtest Overfitting (PBO)** jest frakcją splitów gdzie zwycięzca nie uogólnił. PBO blisko 0 znaczy best configuration
jest rzeczywiście best; PBO 0.5 lub więcej znaczy Twój selection process podnosi noise — verdict staje się
**Overfit** niezależnie od jak dobry zwycięzca wyglądał.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Gdy native cTrader Console optimizer przyjedzie, cMind zasilać będzie jego pełną trial surface tutaj
automatycznie.

## Trials — liczba która liczy

`Trials` to **ile param setów testowałeś** zanim wybrałeś ten. Testowanie jednej strategii i
testowanie dziesięciu tysięcy i utrzymywanie best to całkowicie inne rzeczy: drugi
manufactures wysoką in-sample Sharpe przez przypadek. Zasilanie uczciwym trial count to całe pointu — podnosi
deflation i może przenieść "great" backtest do **Overfit**. Gdy native cTrader Console optimizer
przyjedzie, cMind zasilać będzie mu real grid size automatycznie.

## Wpisy

- **Periodic returns** — jedna liczba per period (np. `0.01` = +1%). Co najmniej dwa.
- **Equity / balance curve** — cMind derives to dla Ciebie consecutive simple returns.
- Lub uruchamia to bezpośrednio na ukończonym backtest: `POST /api/quant/integrity/backtest/{instanceId}` czyta
  stored report's equity curve.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Zwraca verdict, wszystkie metryki i rationale. `POST /api/quant/integrity/backtest/{id}` uruchamia
tę samą analizę na ukończonym backtest którym posiadasz.

## Dlaczego jest niezawodny

Statystyki są czystymi funkcjami w domain core (`Core.Quant`) z zerem infrastruktury
zależności — nie mogą być zabrane przez network blip, i są pinned przez golden-vector unit
testy przeciwko opublikowanym formulom. Normalny CDF/inverse to closed-form approximations
(Abramowitz-Stegun / Acklam), więc te same wpisy zawsze przynosą ten sam verdict.
