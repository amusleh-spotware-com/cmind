---
title: Backtest Integritas Lab
description: Determinisztikus, fund-grade tuloptimalizalas statisztikak (Probabilistic & Deflated Sharpe, t-stat), amelyek egy nyers backtestet Robust / Fragile / Overfit iteletre forditanak, korrigalva a probalt konfiguraciok szamara.
---

# Backtest Integritas Lab

A retail platformok megmutatjak a backtest Sharpe-jat vagy net profit-jat es ott abbahagyjak. Az intézmények soha nem bíznak egy nyers backtestben - megkérdezik, hogy az eredmény túlélja-e **a szelekciós bias korrekcióját és a kipróbált konfigurációk számát**. A Backtest Integritas Lab ezt a vizsgálatot hozza a cMind-hez. Ez **determinisztikus matek** (nincs AI, nincs külső hívás), igy az ítélet reprodukálható és minden szám magyarázható.

Nyisd meg a **cBots → Integritas** (`/quant/integrity`)-nál.

## Mit szamit

Egy hozamsorozat (vagy egy equity/egyenleg görbe) és a paraméterkészletek száma, amit kipróbáltál, hogy eljuss hozzá, az analyzer jelenti:

- **Sharpe ratio** - per-időszak és évesített (négyzetgyök-of-time).
- **Probabilistic Sharpe Ratio (PSR)** - a bizalom, hogy az *igazi* Sharpe túlszárnyalja a benchmarkot, figyelembe véve a nyomvonal hosszát, aszimmetriát és Kurtosis-t (Bailey & López de Prado, 2012). Egy rövid vagy vastag-farkú rekord alacsonyabbra csökkenti.
- **Deflated Sharpe Ratio (DSR)** - PSR a **deflált benchmark ellen mérve**: az a Sharpe, amit az *N véletlenszerű próba legjobbjától* várnál a null alatt (a False Strategy Theorem). Minél több konfigurációt próbáltál ki, annál magasabb a mérce - ez az, ami az overfittinget elkapja.
- **t-statistic** az átlagos hozamból. Harvey, Liu & Zhu követve, egy valódi élőnek t ≥ 3.0-at kell tisztáznia, nem a tankönyvi 2.0-t.
- **Aszimmetria / kurtosis** a hozamoknak, amelyek a PSR/DSR korrekciókat táplálják.

## Az itélet

| Itélet | Jelentés | Szabály |
|---|---|---|
| **Robust** | Az élő túlélte a futtatott próbákat. | DSR ≥ 95% **es** PSR ≥ 95% **es** |t| ≥ 3.0 |
| **Fragile** | Statisztikailag életben de nem meggyőzően - ne méretezz fel ezen egyedül. | a kettő között |
| **Overfit** | Valószínűleg a szelekciós bias artifaktuma, nem valódi élő. | DSR < 90% |

Minden eredmény magában foglal egy egyszerű angol nyelvű indoklást, így a "miért" soha nincs elrejtve.

## A Backtest Overfitting Valószínűsége (próbák felett)

Egy próba *szám* etetése jó; a ** tényleges out-of-sample sorozat minden kipróbált konfigurációhoz** etetése jobb. Illeszd be őket az opcionalis **trial grid**-be (egy sorozat per sor) és a cMind futtatja a **Combinatorially-Symmetric Cross-Validation**-t (Bailey, Borwein, López de Prado & Zhu, 2015): felosztja a megfigyeléseket csoportokra, és minden módját annak, hogy felet válasszon in-sample-ként, kiválasztja az in-sample legjobb konfigurációt és ellenőrzi, hogy ez a győztes a bottom fele **out-of-sample**-re esik-e. A **Probability of Backtest Overfitting (PBO)** a split-ek azon hányada, ahol a győztes nem általánosított. Egy PBO közel 0 azt jelenti, hogy a legjobb konfiguráció tényleg a legjobb; egy PBO 0.5 vagy több azt jelenti, hogy a kiválasztási folyamatod zajt választ - az ítélet **Overfit** lesz, függetlenül attól, mennyire jól nézett ki a győztes.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Amikor a natív cTrader Console optimizer landol, a cMind itt automatikusan eteti a teljes trial felületet.

## Próba - a szám, ami számít

`Trials` az, **hány paraméterkészletet tesztáltél**, mielőtt ezt választottad. Egy stratégia tesztelése és tíz ezernyi tesztelése és a legjobb megtartása radikálisan különböző dolgok: a második véletlenszerűen gyárt egy magas in-sample Sharpe-t. Az őszinte próba szám etetése a lényeg - felemeli a defláció és átviheti az "remek" backtestet **Overfit**-be. Amikor a natív cTrader Console optimizer landol, a cMind automatikusan eteti a sweep valós grid méretét.

## Bemenetek

- **Periodikus hozamok** - egy szám per időszak (pl. `0.01` = +1%). Legalább kettő.
- **Equity / egyenleg görbe** - a cMind lehozza a konzekutív egyszerű hozamokat helyetted.
- Vagy futtasd közvetlenül egy befejezett backtest-en: `POST /api/quant/integrity/backtest/{instanceId}` beolvassa a tárolt jelentés equity görbéjét.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Visszaadja az ítéletet, minden metrikát és az indoklást. `POST /api/quant/integrity/backtest/{id}` ugyanazt az elemzést futtatja egy befejezett backtest-en, amit birtokolsz.

## Miért megbizható

A statisztikák tiszta függvények a domain magban (`Core.Quant`) null infrastruktúra függőséggel - nem tudja őket hálózati blip lehozni, és arany-vektor unit tesztek vannak a publikált formulák ellen. A normál CDF/inverz closed-form közelítések (Abramowitz-Stegun / Acklam), igy ugyanazok a bemenetek mindig ugyanazt az ítéletet adják.
