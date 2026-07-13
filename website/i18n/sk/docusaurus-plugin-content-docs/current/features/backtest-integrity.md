---
description: "Backtest Integrity Lab — deterministické, fond-grade štatistiky prekonania (Probabilistic & Deflated Sharpe, t-stat), ktoré premenia surový backtest na Robust / Fragile / Overfit verdikt, s korekciou na počet konfigurácií, ktoré ste skúsili."
---

# Backtest Integrity Lab

Retailové platformy vám ukážu Sharpe alebo čistý zisk backtestu a zastavia sa tam. Inštitúcie nikdy neveria
raw backtestu — pýtajú sa, či výsledok prežije **korekciu na selection bias a počet
konfigurácií, ktoré ste skúsili**. Backtest Integrity Lab prináša túto kontrolu do cMind. Je to **deterministická
matematika** (žiadna AI, žiadne externé volania), takže verdikt je reprodukovateľný a každé číslo je vysvetliteľné.

Otvorte ho na **cBots → Integrity** (`/quant/integrity`).

## Čo počíta

Pri danej série výnosov (alebo krivke equity/zostatku) a počte parametrových setov, ktoré ste skúsili na jej
dosiahnutie, analyzer hlási:

- **Sharpe ratio** — per-period a anualizovaný (odmocnina z času).
- **Probabilistic Sharpe Ratio (PSR)** — dôvera, že *skutočný* Sharpe prekoná benchmark,
  zohľadňujúc dĺžku track-recordu, šikmosť a špičatosť (Bailey & López de Prado, 2012). Krátky alebo
  hrubý-chvostý záznam ho znižuje.
- **Deflated Sharpe Ratio (DSR)** — PSR merané voči **deflovanému benchmarku**: Sharpe, ktorý by ste očakávali od
  *najlepšieho z N náhodných pokusov* za nulovej hypotézy (False Strategy Theorem). Čím viac
  konfigurácií ste skúsili, tým vyšší štandard — toto zachytáva prekonfigurovanie.
- **t-statistic** priemerného výnosu. Podľa Harvey, Liu & Zhu, skutočná výhoda by mala prekonať **t ≥ 3.0**,
  nie učebnicové 2.0.
- **Šikmosť / špičatosť** výnosov, ktoré sa používajú v PSR/DSR korekciách.

## Verdikt

| Verdikt | Význam | Pravidlo |
|---|---|---|
| **Robust** | Výhoda prežíva pokusy, ktoré ste robili. | DSR ≥ 95% **a** PSR ≥ 95% **a** \|t\| ≥ 3.0 |
| **Fragile** | Štatisticky životaschopné, ale nie presvedčivo — na tomto samotnom nezväčšujte pozíciu. | medzi dvoma |
| **Overfit** | Pravdepodobne artefakt selection bias, nie skutočná výhoda. | DSR < 90% |

Každý výsledok nesie plain-English odôvodnenie, takže "prečo" nikdy nie je skryté.

## Probability of Backtest Overfitting (naprieč pokusmi)

Zadanie počtu pokusov je dobré; zadanie **skutočného out-of-sample series každej konfigurácie, ktorú ste
skúsili** je lepšie. Vložte ich do voliteľnej **trial grid** (jedna séria na riadok) a cMind spustí
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): rozdelí
pozorovania do skupín, a pre každý spôsob výberu polovice ako in-sample vyberie najlepšiu in-sample
konfiguráciu a skontroluje, či víťaz pristál v spodnej polovici **out-of-sample**. **Probability of Backtest Overfitting (PBO)** je
frankcia rozdelení, kde víťaz zlyhal na generalizáciu. PBO blízke 0 znamená, že najlepšia konfigurácia je skutočne najlepšia; PBO 0.5
alebo viac znamená, že váš výberový proces vyberá šum — verdikt sa stáva **Overfit** bez ohľadu na to, ako dobre
víťaz vyzeral.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Keď natívny cTrader Console optimizer pristane, cMind sem automaticky naženie jeho plnú trial plochu.

## Trials — číslo, na ktorom záleží

`Trials` je **koľko parametrových setov ste otestovali** pred výberom tohto. Testovanie jednej stratégie a
testovanie desiatich tisíc a udržiavanie najlepšej sú diametrálne odlišné veci: druhé vyrába
vysoký in-sample Sharpe náhodou. Zadanie čestného počtu pokusov je celý zmysel — zvyšuje defláciu a môže
presunúť "skvelý" backtest na **Overfit**. Keď natívny cTrader Console optimizer pristane, cMind automaticky
načrtne reálnu veľkosť gridu.

## Vstupy

- **Periodické výnosy** — jedno číslo na obdobie (napr. `0.01` = +1%). Minimálne dve.
- **Equity / krivka zostatku** — cMind z nich odvodí po sebe idúce jednoduché výnosy za vás.
- Alebo ho spustite priamo na dokončenom backteste: `POST /api/quant/integrity/backtest/{instanceId}` prečíta
  uloženú správu equity krivky.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vráti verdikt, všetky metriky a odôvodnenie. `POST /api/quant/integrity/backtest/{id}` spustí rovnakú
analýzu na dokončenom backteste, ktorý vlastníte.

## Prečo je spoľahlivý

Štatistiky sú čisté funkcie v doménovom jadre (`Core.Quant`) s nulovými infraštruktúrnymi
závislosťami — nemôžu byť zlyhané sieťovým výpadkom a sú pripnuté golden-vector unit testami voči publikovaným formulám. Normálne CDF/inverzie sú closed-form aproximácie
(Abramowitz-Stegun / Acklam), takže rovnaké vstupy vždy dajú rovnaký verdikt.
