---
description: "Backtest Integrity Lab — deterministické, fond-grade štatistiky prekonania (Probabilistic & Deflated Sharpe, t-stat), ktoré premenia surový backtest na Robust / Fragile / Overfit verdikt, s korekciou na počet konfigurácií, ktoré ste skúsili."
---

# Laboratórium integrity backtestov

Maloobchodné platformy vám ukážu Sharpe alebo čistý zisk backtestov a stanú sa na tom. Inštitúcie nikdy nemajú dôveru k surému backtestovi — pýtajú sa, či výsledok prežije **korektúru pre predpojatosť výberu a počet testovaných konfigurácií**. Laboratórium Backtest Integrity Lab prináša túto kontrolu do cMind. Je to **deterministická matematika** (bez AI, bez externých volaní), takže je verdikt reprodukovateľný a každé číslo je vysvetľujúce.

Otvorte ho na **cBots → Integrity** (`/quant/integrity`).

## Čo sa počítať

Vzhľadom na sériu výnosov (alebo krivku vlastného kapitálu/zostatku) a počet sád parametrov, ktoré ste skúšali, aby ste sa dostali k nemu, analyzátor hlási:

- **Sharpe ratio** — za obdobie a ročne zhodnotené (druhá odmocnina času).
- **Probabilistic Sharpe Ratio (PSR)** — dôvera, že *pravý* Sharpe prekonáva benchmark, berúc do úvahy dĺžku sledovania, šikmosť a špičatosť (Bailey & López de Prado, 2012). Krátky alebo tučný chvost znižuje jeho hodnotu.
- **Deflated Sharpe Ratio (DSR)** — PSR merané proti **deflovanému benchmarku**: Sharpe, ktorý by ste očakávali z *najlepšieho z N náhodných pokusov* pod nulovou hypotézou (False Strategy Theorem). Čím viac konfigurácií ste skúšali, tým vyššia je lišta — to je to, čo zachytáva prepridelenie.
- **t-štatistika** priemerného výnosu. Podľa Harvey, Liu & Zhu by pravá výhoda mala prekonať **t ≥ 3.0**, nie učebnicovú 2.0.
- **Šikmosť / špičatosť** výnosov, ktoré vstupujú do opráv PSR/DSR.

## Verdikt

| Verdikt | Znamenie | Pravidlo |
|---|---|---|
| **Robustný** | Výhoda prežije skúšky, ktoré ste vykonali. | DSR ≥ 95% **a** PSR ≥ 95% **a** \|t\| ≥ 3.0 |
| **Krehký** | Štatisticky živý, ale nie presvedčivo — neverzte zvýšeniu veľkosti len na základe tohto. | medzi dvoma |
| **Prepridelený** | S najväčšou pravdepodobnosťou artefakt predpojatosti výberu, nie skutočná výhoda. | DSR < 90% |

Každý výsledok niesol jasný anglický zdôvodnenie, takže „prečo" nikdy nie je skryté.

## Pravdepodobnosť prepridelenia backtestov (po všetkých pokusoch)

Zadanie počtu pokusov je dobré; zadanie **skutočnej série mimo vzorky každej konfigurácie, ktoré ste skúšali** je lepšie. Vložte ich do voliteľnej **mriežky pokusov** (jedna séria na riadok) a cMind spustí **kombinačnú symetrickú krížovú validáciu** (Bailey, Borwein, López de Prado & Zhu, 2015): rozdelí pozorovania do skupín a pre každý spôsob výberu polovice ako in-sample vyberie in-sample najlepšiu konfiguráciu a skontroluje, či víťazný tím nedopadne v spodnej polovici **mimo vzorky**. **Pravdepodobnosť prepridelenia backtestov (PBO)** je podiel delení, kde víťazný tím zlyhal pri generalizácii. PBO blízko 0 znamená, že najlepšia konfigurácia je skutočne najlepšia; PBO 0,5 alebo viac znamená, že váš výber procesu vyberá šum — verdikt sa stáva **Prepridelený** bez ohľadu na to, ako dobré vyzeralo.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Keď príde natívny optimalizátor cTrader Console, cMind si automaticky dodá jeho úplný povrch pokusov.

## Pokusy — číslo, ktoré sa počítá

`Trials` je **počet sád parametrov, ktoré ste testovali** pred výberom tejto. Testovanie jednej stratégie a testovanie desať tisíc a udržanie najlepšej sú veľmi odlišné veci: druhá vyrába vysoký Sharpe in-sample náhodou. Zadanie čestného počtu pokusov je celý zmysel — zvyšuje to deflácií a môže presunúť „skvelý" backtest na **Prepridelený**. Keď príde natívny optimalizátor cTrader Console, cMind mu automaticky dodá skutočnú veľkosť mriežky zametania.

## Vstupy

- **Periodické výnosy** — jedno číslo za obdobie (napr. `0.01` = +1%). Aspoň dve. Pole sa overuje pri písaní: počítá platné čísla, označuje akýkoľvek token, ktorý nie je číslo, a povoľuje **Analyze** až keď sú prítomné aspoň dve čisté hodnoty (mriežka pokusov povoľuje **Posúďte prepridelenie** keď sú k dispozícií dve série štyroch alebo viacerých čísiel).
- **Krivka vlastného kapitálu / zostatku** — cMind odvodzuje po sebe idúce jednoduché výnosy za vás.
- **Priamo z backtestovacieho behu — bez kopírovania a vloženia.** Každý dokončený backtest vystavuje štít **Skontrolujte integritu backtestov** ikonu na riadku zoznamu **Backtest** a v jeho pohľade podrobností inštancie; jeden klik spustí laboratórium na krivke vlastného kapitálu uloženej na tomto spusti a zobrazí verdikt v dialógu. Ikona je zakázaná, kým sa backtest nevypočíta a nevytvorí správu, takže to nie je nikdy mŕtvy ovládací prvok. Pod kapotou ide o `POST /api/quant/integrity/backtest/{instanceId}`, ktorý čítavymieňa krivku vlastného kapitálu z uloženej správy.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vráti verdikt, všetky metriky a zdôvodnenie. `POST /api/quant/integrity/backtest/{id}` spustí rovnakú analýzu na dokončenom backteste, ktorého ste vlastníkom.

## Prečo je to spoľahlivé

Štatistika sú čisté funkcie v jadre domény (`Core.Quant`) s nulovými závislosťami infraštruktúry — nemôžu byť vypnuté sieťovým problémom a sú pripnuté jednotkovými testami s zlatým vektorom oproti publikovaným vzorcom. Normálna CDF/inverzia sú uzavreté aproximácie (Abramowitz-Stegun / Acklam), takže rovnaké vstupy vždy prinášajú rovnaký verdikt.
