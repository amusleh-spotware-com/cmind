---
description: "Backtest Integrity Lab — deterministické, institucijní statistiky overfittingu (Probabilistic & Deflated Sharpe, t-stat), které změní surový backtest na Robust / Fragile / Overfit verdikt, korekce pro počet vyzkoušených konfigurací."
---

# Backtest Integrity Lab

Retailové platformy vám ukážou Sharpe nebo čistý zisk backtestu a skončí. Instituce nikdy nedůvěřují surému backtestu — ptají se, zda výsledek přežije **korekci pro selekční zkreslení a počet vyzkoušených konfigurací**. Backtest Integrity Lab přináší tuto kontrolu do cMind. Jedná se o **deterministickou matematiku** (bez AI, bez externích hovorů), takže verdikt je reprodukovatelný a každé číslo je vysvětlitelné.

Otevřete jej na **cBots → Integrity** (`/quant/integrity`).

## Co počítá

Vzhledem k řadě výnosů (nebo křivce vlastního kapitálu/zůstatku) a počtu sad parametrů, které jste vyzkoušeli, aby jste jej dosáhli, anylyzátor hlásí:

- **Sharpe ratio** — za období a anualizované (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — důvěra, že *skutečný* Sharpe překonává benchmark, zohledňující délku stopy, šikmost a špičatost (Bailey & López de Prado, 2012). Krátký nebo tlustý záznam to snižuje.
- **Deflated Sharpe Ratio (DSR)** — PSR měřeno proti **deflovanému benchmarku**: Sharpe, který byste očekávali od *nejlepšího z N náhodných pokusů* pod nulou (False Strategy Theorem). Čím více konfigurací jste vyzkoušeli, tím vyšší je lišta — to je to, co chytá overfitting.
- **t-statistic** průměrného výnosu. Podle Harveye, Liu & Zhu, skutečná výhoda by měla překonat **t ≥ 3.0**, ne v učebnicích 2.0.
- **Šikmost / špičatost** výnosů, které krmí opravy PSR/DSR.

## Verdikt

| Verdikt | Význam | Pravidlo |
|---|---|---|
| **Robust** | Výhoda přežije pokusy, které jste spustili. | DSR ≥ 95% **a** PSR ≥ 95% **a** \|t\| ≥ 3.0 |
| **Fragile** | Statisticky naživu, ale ne přesvědčivě — nezvyšujte velikost jen na základě tohoto. | mezi těmito dvěma |
| **Overfit** | Nejpravděpodobněji artefakt selekčního zkreslení, ne skutečná výhoda. | DSR < 90% |

Každý výsledek obsahuje jasně napsaný důvod, takže "proč" nikdy není skryt.

## Pravděpodobnost backtestu Overfittingu (v rámci pokusů)

Podání počtu pokusů je dobré; podání **skutečné out-of-sample série každé konfigurace, kterou jste vyzkoušeli** je lepší. Vlepte je do volitelné **trial grid** (jedna řada na řádek) a cMind spustí **Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): rozdělí pozorování do skupin, a pro každý způsob výběru poloviny jako in-sample vybere nejlepší konfiguraci v in-sample a zkontroluje, zda vítěz dopadá v dolní polovině **out-of-sample**. **Probability of Backtest Overfitting (PBO)** je frakce dělení, kde vítěz selhal zobecnit. PBO blízko 0 znamená, že nejlepší konfigurace je skutečně nejlepší; PBO 0,5 nebo více znamená, že váš proces výběru vybírá šum — verdikt se stane **Overfit** bez ohledu na to, jak dobrý vítěz vypadal.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Když přijde nativní cTrader Console optimizer, cMind sem automaticky vloží svůj úplný povrch pokusů.

## Pokusy — číslo, které má smysl

`Trials` je **kolik sad parametrů jste testovali** před výběrem tohoto. Testování jedné strategie a testování deset tisíc a udržení nejlepších jsou divoce odlišné věci: druhá vyrábí vysoký in-sample Sharpe náhodou. Podání upřímného počtu pokusů je celý bod — zvyšuje to deflaci a může přesunout "skvělý" backtest na **Overfit**. Když přijde nativní cTrader Console optimizer, cMind mu automaticky podá skutečnou velikost mřížky tahu.

## Vstupy

- **Periodické výnosy** — jedno číslo na období (např. `0.01` = +1%). Alespoň dva.
- **Vlastní kapitál / zůstatkový graf** — cMind pro vás odvozuje postupné jednoduché výnosy.
- Nebo jej spusťte přímo na dokončeném backtestu: `POST /api/quant/integrity/backtest/{instanceId}` čte zůstatkový graf uloženého reportu.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vrátí verdikt, všechny metriky a důvod. `POST /api/quant/integrity/backtest/{id}` spustí stejnou analýzu na dokončeném backtestu, který vlastníte.

## Proč je spolehlivý

Statistiky jsou čisté funkce v domén jádru (`Core.Quant`) s nulovou závislostí infrastruktury — nemohou být sraženy síťovým poruchou a jsou připraveny golden-vector testy jednotky proti publikovaným vzorcům. Normální CDF/inverse jsou aproximace v uzavřené formě (Abramowitz-Stegun / Acklam), takže stejné vstupy vždy produkují stejný verdikt.
