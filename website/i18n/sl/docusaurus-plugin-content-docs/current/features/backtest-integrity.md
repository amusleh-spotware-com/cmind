---
description: "Backtest Integrity Lab — deterministične, institucionalne statistike preprilegavanja (Probabilistic & Deflated Sharpe, t-stat), ki spremenijo surove backteste v Robust / Fragile / Overfit sodbo, popravljeno glede na število poskušanih konfigurácij."
---

# Backtest Integrity Lab

Maloprodajne platforme vam pokažejo Sharpe ratio ali neto dobiček backtesta in tu se ustavi. Institucije nikoli ne zaupajo surjem backtestumu — vprašajo se, ali rezultat preživi **popravo za pristranskost izbire in število poskušanih konfigurácij**. Backtest Integrity Lab to preverko prinese v cMind. Gre za **determinističnem matematike** (brez AI, brez zunanjih klicev), zato je sodba ponovljiva in vsaka številka je pojasnjiva.

Odprite ga na **cBots → Integrity** (`/quant/integrity`).

## Kaj izračuna

Glede na vrsto dohodka (ali krivuljo lastniškega kapitala/ravnotežja) in število sklopov parametrov, ki ste jih poskusili, da ste prišli do tega, analizator poroča:

- **Sharpe ratio** — na obdobje in letnik (kvadratni koren časa).
- **Probabilistic Sharpe Ratio (PSR)** — zaupanje, da *pravi* Sharpe premaga referenčno vrednost, upoštevajoč dolžino zgodovine, simetrijo in kurtozo (Bailey & López de Prado, 2012). Kratka ali debela zgodovina ga zniža.
- **Deflated Sharpe Ratio (DSR)** — PSR merjena proti **deflatirani referenčni vrednosti**: Sharpe, ki bi ga pričakovali od *najboljšega N naključnih poskusov* pod nič hipotezo (False Strategy Theorem). Več konfigurácij kot poskusite, višja je omejitev — to je to, kar ujame preprilegovanje.
- **t-statistic** povprečnega dohodka. Po Harvey, Liu & Zhu bi pravi robni kapital jasen **t ≥ 3.0**, ne učbenika 2.0.
- **Simetrija / kurtoznost** dohodkov, ki dajejo popravke PSR/DSR.

## Sodba

| Sodba | Pomen | Pravilo |
|---|---|---|
| **Robust** | Prednost preživi poskuse, ki ste jih opravili. | DSR ≥ 95% **in** PSR ≥ 95% **in** \|t\| ≥ 3.0 |
| **Fragile** | Statistično živ, a ne prepričljivo — ne povečujte velikosti samo na podlagi tega. | med obema |
| **Overfit** | Najverjetnije artefakt pristranskosti izbire, ne pravi robni kapital. | DSR < 90% |

Vsak rezultat je pogosto izrekvaren razlog, tako da se "zakaj" nikoli ne skrije.

## Probability of Backtest Overfitting (v različnih poskusih)

Prehranjivanje števila poskusov je dobro; **hranitev dejanske serije izven vzorca vsake konfiguracije, ki ste jo poskusili**, je boljše. Jih prilepite v izbirno **mrežo poskusov** (ena serija na vrstico) in cMind teče **Kombinatorsko simetrična navzkrižna validacija** (Bailey, Borwein, López de Prado & Zhu, 2015): razdeliti opazovanja v skupine in za vsak način izbire polovice kot vzorca v vzorcu izbere najboljšo konfigurácijo in preveri, ali ta zmagovalec pristane v spodnji polovici **izven vzorca**. **Probability of Backtest Overfitting (PBO)** je delež razdelkov, kjer je zmagovalec ni generaliziral. PBO blizu 0 pomeni, da je najboljša konfiguracija res najboljša; PBO 0,5 ali več pomeni, da vaš postopek izbire naključje — sodba postane **Overfit** ne glede na to, kako dobra je videla zmagovalca.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Ko pride domač optimizator cTrader Console, bo cMind hranil njegove polne površine poskusov tukaj samodejno.

## Trials — številka, ki se šteje

`Trials` je **koliko sklopov parametrov ste testirali** preden ste izbral tega. Testiranje ene strategije in testiranje deset tisoč ter ohranjanje najboljšega je bistveno drugače: drugi ustvari visoko vzorčno Sharpe naključno. Prehranjivanje poštene količine poskusov je smisel — poveča deflacijo in lahko premakne "odličen" backtest v **Overfit**. Ko pride domač optimizator cTrader Console, cMind nahrani pravo velikost mrežo zajemanja samodejno.

## Vnosi

- **Periodični dohodki** — ena številka na obdobje (npr. `0.01` = +1%). Najmanj dve. Polje se preveri med tipkanjem: šteje veljavne številke, označi kateri koli žeton, ki ni številka, in omogoči **Analiziraj** šele po tem, ko sta na voljo najmanj dve čisti vrednosti (mreža poskusov omogoči **Oceni preprilegavanje** po tem, ko sta na voljo dve vrsti štirih ali več številk).
- **Krivulja lastniškega kapitala / ravnotežja** — cMind izpelje zaporedne enostavne dohodke za vas.
- **Naravnost iz backtestnega teka — brez kopiranja in lepljenja.** Vsak zaključeni backtest izpostavi ščit **Preverka celovitosti backtesta** ikono na vrstici seznama **Backtest** in na njegovem podrobnem pogledu; en klik teče laboratorij na shranjeno krivuljo lastniškega kapitala tega teka in prikaže sodbo v dialogu. Ikona je onemogočena, dokler se backtest ne zaključi in ne ustvari poročila, zato nikoli ni mrtve kontrole. Pod pokrovom je to `POST /api/quant/integrity/backtest/{instanceId}`, ki prebere shranjeno krivuljo lastniškega kapitala poročila.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Vrne sodbo, vse metrike in razlog. `POST /api/quant/integrity/backtest/{id}` poganja isto analizo na zaključenem backtestmu, ki ga lastite.

## Zakaj je zanesljivo

Statistika so čiste funkcije v jedru domene (`Core.Quant`) z ničelnimi odvisnostmi infrastrukture — ne morejo jih ozdraviti mrežnega zmešnjanja in jih fiksirajo zlati vektorski preskusi enot proti objavljenim formulam. Normalne CDF/inverzne so zaprte oblike približke (Abramowitz-Stegun / Acklam), zato enaki vnosi vedno dajejo isto sodbo.
