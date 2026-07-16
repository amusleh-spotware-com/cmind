---
description: "Backtest Integrity Lab — deterministické, fund-grade statistiky overfittingu (Probabilistic & Deflated Sharpe, t-stat), které změní surový backtest na Robust / Fragile / Overfit verdikt, s korekcí na počet vyzkoušených konfigurací."
---

# Backtest Integrity Lab

Maloobchodní platformy vám ukážou Sharpe poměr backtestu nebo čistý zisk a skončí tam. Instituce nikdy
nedůvěřují surovému backtestu — ptají se, zda výsledek přežije **korekci na sklon výběru a počet
vyzkoušených konfigurací**. Backtest Integrity Lab tuto kontrolu přináší do cMind. Je to **deterministická
matematika** (bez AI, bez externích volání), takže je verdikt reprodukovatelný a každé číslo je vysvětlitelné.

Otevřete jej na **cBots → Integrity** (`/quant/integrity`).

## Co počítá

Vzhledem k řadě výnosů (nebo křivce vlastního kapitálu/zůstatku) a počtu souprav parametrů, které jste
vyzkoušeli, aby jste na ní dospěli, analyzátor hlásí:

- **Sharpe poměr** — za období a anualizovaný (druhá odmocnina času).
- **Probabilistic Sharpe Ratio (PSR)** — spolehlivost, že *skutečný* Sharpe překoná benchmark,
  zohledňuje délku historických dat, skewness a kurtosis (Bailey & López de Prado, 2012). Krátký nebo
  tlustoocasý záznam to snižuje.
- **Deflated Sharpe Ratio (DSR)** — PSR měřený proti **deflovanému benchmarku**: Sharpe, který byste
  očekávali od *nejlepšího z N náhodných pokusů* pod nulovou hypotézou (False Strategy Theorem). Čím
  více konfigurací jste vyzkoušeli, tím vyšší je latinka — to je to, co odhaluje overfit.
- **t-statistica** průměrného výnosu. Podle Harvey, Liu & Zhu by skutečná výhoda měla překonat **t ≥ 3.0**,
  ne učebnicový 2.0.
- **Skewness / kurtosis** výnosů, které informují PSR/DSR korekce.

## Verdikt

| Verdikt | Význam | Pravidlo |
|---|---|---|
| **Robust** | Výhoda přežije pokusy, které jste spustili. | DSR ≥ 95% **a** PSR ≥ 95% **a** \|t\| ≥ 3.0 |
| **Fragile** | Statisticky živá, ale ne přesvědčivě — nezvětšujte jen na základě toho. | mezi těmito dvěma |
| **Overfit** | Velmi pravděpodobně artefakt skonu výběru, ne skutečná výhoda. | DSR < 90% |

Každý výsledek obsahuje vysvětlení v běžné angličtině, takže důvod není nikdy skryt.

## Probability of Backtest Overfitting (across trials)

Zadat počet pokusů je dobré; zadat **skutečnou řadu mimo vzorek každé konfigurace, kterou jste vyzkoušeli**
je lepší. Vložte je do volné **tabulky pokusů** (jednu řadu na řádek) a cMind spustí
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): rozdělí
pozorování do skupin a pro každý způsob výběru poloviny jako in-sample vybere nejlepší konfiguraci in-sample
a zkontroluje, zda tento vítěz skončí v dolní polovině **out-of-sample**. **Probability of Backtest
Overfitting (PBO)** je podíl rozdělení, kde si vítěz nezachoval generalizaci. PBO blízko 0 znamená, že
nejlepší konfigurace je opravdu nejlepší; PBO 0,5 nebo více znamená, že váš proces výběru sbírá šum —
verdikt se stane **Overfit** bez ohledu na to, jak dobře se vítěz jevil.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Když přijde nativní optimalizátor cTrader Console, cMind automaticky zašle svůj celý povrch pokusů sem.

## Trials — číslo, které záleží

`Trials` je **kolik sad parametrů jste otestovali** předtím, než jste si vybrali tuto. Testování jedné
strategie a testování deseti tisíc a udržování nejlepší jsou diametrálně odlišné věci: druhý vyrábí
vysoké in-sample Sharpe náhodou. Zadat čestný počet pokusů je celý smysl — zvyšuje deflaci a může
posunout "skvělý" backtest do **Overfit**. Když přijde nativní optimalizátor cTrader Console, cMind mu
automaticky zašle skutečnou velikost mřížky sweepů.

## Vstupy

- **Periodické výnosy** — jedno číslo za období (např. `0.01` = +1%). Nejméně dvě. Pole se ověřuje při
  psaní: počítá platná čísla, označuje jakýkoli token, který není číslem, a povoluje **Analyze** až poté,
  co jsou přítomny nejméně dvě čisté hodnoty (tabulka pokusů povoluje **Assess overfitting** až poté, co
  jsou připraveny dvě řady čtyř nebo více čísel).
- **Křivka vlastního kapitálu / zůstatku** — cMind odvozuje po sobě jdoucí jednoduché výnosy za vás.
- **Přímo z běhu backtestu — bez kopírování a vkládání.** Každý dokončený backtest odhaluje štít **Check
  backtest integrity** ikonu na řádku seznamu **Backtest** a na jeho zobrazení podrobností instance; jeden
  klik spustí Lab na uložené křivce vlastního kapitálu daného běhu a v dialogu zobrazí verdikt. Ikona je
  deaktivována, dokud se backtest nedokončí a nevytvoří zprávu, takže nikdy není mrtvý ovládací prvek.
  Pod pokličkou se jedná o `POST /api/quant/integrity/backtest/{instanceId}`, který čte křivku vlastního
  kapitálu uložené zprávy.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vrátí verdikt, všechny metriky a zdůvodnění. `POST /api/quant/integrity/backtest/{id}` spustí stejnou
analýzu na dokončeném backtestu, který vlastníte.

## Why it is reliable

Statistiky jsou čistými funkcemi v jádru domény (`Core.Quant`) bez závislostí na infrastruktuře — nemohou
být vypnuty síťovým selháním a jsou připnuty testy zlatých vektorů proti publikovaným vzorcům. Normální
CDF/inverse jsou aproximace v uzavřené formě (Abramowitz-Stegun / Acklam), takže stejné vstupy vždy
poskytují stejný verdikt.
