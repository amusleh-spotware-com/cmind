---
description: "Backtest Integrity Lab — deterministyczne, instytucjonalne statystyki przepełnienia (Probabilistic & Deflated Sharpe, t-stat) które zamieniają surowy backtest na werdykt Robust / Fragile / Overfit, korygując liczbę testowanych konfiguracji."
---

# Backtest Integrity Lab

Platformy detaliczne pokazują Sharpe'a lub czysty zysk z backtestu i na tym się kończą. Instytucje nigdy nie ufają surowym backtestom — pytają czy wynik przetrwa **korektę na błąd selekcji i liczbę testowanych konfiguracji**. Backtest Integrity Lab przynosi to sprawdzenie do cMind. To jest **deterministyczna matematyka** (bez AI, bez zewnętrznych wywołań), więc werdykt jest powtarzalny i każda liczba jest wyjaśnialna.

Otwórz go w **cBots → Integrity** (`/quant/integrity`).

## Co oblicza

Biorąc serię zwrotów (lub krzywę kapitału/salda) oraz liczbę zestawów parametrów, które testowałeś, aby do niego dojść, analizator raportuje:

- **Wskaźnik Sharpe'a** — na okres i annualizowany (pierwiastek-czasu).
- **Probabilistic Sharpe Ratio (PSR)** — pewność, że *prawdziwy* Sharpe bije benchmark, uwzględniając długość historii, skośność i kurtozę (Bailey & López de Prado, 2012). Krótka lub gruba historia obniża go.
- **Deflated Sharpe Ratio (DSR)** — PSR zmierzony względem **zdeflatowanego benchmarku**: Sharpe'a, którego spodziewałbyś się z *najlepszego z N losowych prób* pod nullą (False Strategy Theorem). Im więcej konfiguracji testowałeś, tym wyższa poprzeczka — to co łapie przepełnienie.
- **t-statystyka** średniej zwrotu. Zgodnie z Harvey, Liu & Zhu, prawdziwa krawędź powinna pokonać **t ≥ 3.0**, a nie podręcznik 2.0.
- **Skośność / kurtoza** zwrotów, które zasilają korekty PSR/DSR.

## Werdykt

| Werdykt | Znaczenie | Reguła |
|---|---|---|
| **Robust** | Krawędź przetrwa testy które uruchomiłeś. | DSR ≥ 95% **and** PSR ≥ 95% **and** \|t\| ≥ 3.0 |
| **Fragile** | Statystycznie żywa ale nie przekonująco — nie skaluj na podstawie tego samego. | między dwoma |
| **Overfit** | Najprawdopodobniej artefakt błędu selekcji, a nie prawdziwa krawędź. | DSR < 90% |

Każdy rezultat nosi wyraźne wyjaśnienie w języku angielskim, więc "dlaczego" nigdy nie jest ukryte.

## Probability of Backtest Overfitting (across trials)

Podanie liczby prób *count* jest dobre; podanie **rzeczywistej serii out-of-sample każdej konfiguracji którą testowałeś** jest lepsze. Wklej je w opcjonalną **siatkę prób** (jedna seria na linię) i cMind uruchamia **Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): dzieli obserwacje na grupy i dla każdego sposobu wyboru połowy jako in-sample wybiera in-sample najlepszą konfigurację i sprawdza czy ta zwycięzca ląduje w dolnej połowie **out-of-sample**. **Probability of Backtest Overfitting (PBO)** to frakcja podziałów gdzie zwycięzca nie uogólnił się. PBO bliskie 0 oznacza że najlepsza konfiguracja jest naprawdę najlepsza; PBO 0.5 lub więcej oznacza że proces selekcji wybiera szum — werdykt staje się **Overfit** niezależnie od tego jak dobry wyglądał zwycięzca.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Gdy natywny optimizer cTrader Console pojawi się, cMind automatycznie zasilić go tutaj pełną powierzchnią prób.

## Trials — the number that matters

`Trials` to **ile zestawów parametrów testowałeś** przed wybraniem tego. Testowanie jednej strategii i testowanie dziesięciu tysięcy i utrzymanie najlepszego to zupełnie różne rzeczy: drugi produkuje wysokiego in-sample Sharpe'a przypadkowo. Podanie uczciwej liczby prób to całe znaczenie — podnosi deflatę i może przesunąć "wspaniały" backtest do **Overfit**. Gdy natywny optimizer cTrader Console pojawi się, cMind automatycznie zasilać go rzeczywistą wielkością gridu.

## Inputs

- **Periodic returns** — jedna liczba na okres (np. `0.01` = +1%). Co najmniej dwa. Pole sprawdza się podczas pisania: liczy ważne liczby, flaguje każdy token który nie jest liczbą i tylko włącza **Analyze** gdy co najmniej dwie czystych wartości są obecne (siatka prób włącza **Assess overfitting** gdy dwie serie czterech lub więcej liczb każda są gotowe).
- **Equity / balance curve** — cMind wyprowadza kolejne proste zwroty dla ciebie.
- **Straight from a backtest run — no copy-paste.** Każdy ukończony backtest udostępnia tarczę **Check backtest integrity** ikonę na liście **Backtest** i na widoku szczegółów instancji; jeden klik uruchamia Lab na przechowywane krzywej kapitału tego przebiegu i pokazuje werdykt w dialogu. Ikona jest wyłączona dopóki backtest się nie ukończy i nie wyprodukuje raportu, więc nigdy nie jest martwą kontrolą. Pod maską to jest `POST /api/quant/integrity/backtest/{instanceId}`, która czyta przechowywane krzywę kapitału raportu.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Zwraca werdykt, wszystkie metryki i uzasadnienie. `POST /api/quant/integrity/backtest/{id}` uruchamia tę samą analizę na ukończonym backteście którym posiadasz.

## Why it is reliable

Statystyki to czyste funkcje w domenie core (`Core.Quant`) z zerową zależnością infrastruktury — nie mogą być przecięte przez zawirowania sieci i są przypięte przez testy jednostkowe golden-vector względem opublikowanych formuł. Normalna CDF/inverse to zamknięte przybliżenia formy (Abramowitz-Stegun / Acklam), więc te same wejścia zawsze dają ten sam werdykt.
