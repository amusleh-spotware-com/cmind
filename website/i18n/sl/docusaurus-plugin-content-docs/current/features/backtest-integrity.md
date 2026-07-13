---
description: "Backtest Integrity Lab — deterministični, sklad-razred overfitting statistika (Probabilistična & Deflated Sharpe, t-stat) da se surova backtest v a Robust / Fragile / Overfit sodbo, korišćenja kako mnogi konfiguracije ste poskušali."
---

# Backtest Integrity Lab

Maloprodajne platforme vam pokažejo backtest Sharpe ali neto dobička in ustavi tam. Institucije nikoli zaupati a
surova backtest — sprašujejo se ali rezultat preživi **korišćenja izbir pristranskosti in število
konfiguracije ste poskušali**. Backtest Integrity Lab to preverjajo da cMind. To je **deterministična
matematika** (brez AI, brez zunanjih klicev), zato je sodba ponovljivo in vsako število je razloženo.

Odprite ga pri **cBots → Integrity** (`/quant/integrity`).

## Kaj se izračuna

Glede na vrniti serijo (ali a kapitala/saldo krivulja) in število parametra nastavitve ste poskušali prispeti
pri tem, analizator poročila:

- **Sharpe razmerje** — na-obdobja in letno (kvadrat-koren-časa).
- **Probabilistična Sharpe Razmerje (PSR)** — zaupanje, da se *resnično* Sharpe teče ognjeni,
  učetovanje za track-zapisa dolžina, skewness in kurtosis (Bailey & López de Prado, 2012). A kratka ali
  debelo-repena zapisa spusti ga.
- **Deflated Sharpe Razmerje (DSR)** — PSR izmerjena proti a **deflated ognjeni**: Sharpe bi ste
  pričakoval od *najboljšega od N naključne preskusi* pod null (Neresnično strategijo teoremi). Več
  konfiguracija ste poskušali, višje vrata — to je kaj ujame overfitting.
- **t-statistika** srednja dohodek povračilo. Sledeči Harvey, Liu & Zhu, pravi rob bi moral jasno **t ≥ 3.0**,
  ne učbenika 2.0.
- **Skewness / kurtosis** povračil, ki prehranih PSR/DSR korišćenja.

## Sodba

| Sodba | Pomen | Pravilo |
|---|---|---|
| **Robust** | Robustni rob preživi skušanja ste tekli. | DSR ≥ 95% **in** PSR ≥ 95% **in** \|t\| ≥ 3.0 |
| **Fragile** | Statistično žive vendar ne prepričljivo tako — ne razmerek gor na to sami. | med dva |
| **Overfit** | Večina verjetno artefakt izbir pristranskosti, ne pravi rob. | DSR < 90% |

Vsak rezultat nosi plain-English rationale zato "zakaj" je nikoli skrit.

## Verjetnost od Backtest Overfitting (preko preskusi)

Hranitev a poskusiti *štet* je dobro; hranitev **pravi out-of-sample serije vsake konfiguracije ste
poskušali** je boljše. Lepljenje jih v izbirni **preskus mrežo** (eno serijo na liniji) in cMind teče
**Kombinatorično-Simetrična Križ-Validacija** (Bailey, Borwein, López de Prado & Zhu, 2015): se deli
opažanja v skupine, in za vsak način izbire pol kot v-vzorec izbere v-vzorec
najboljši konfiguracija in preverka ali ta zmagovalec pristane v spodnji pol **out-of-sample**. Na
**Verjetnost od Backtest Overfitting (PBO)** je delček delitev kje zmagovalec ne obsega
uopce. A PBO blizu 0 pomen najboljši konfiguracija je resnično najboljši; a PBO od 0.5 ali več pomen vaše
izbiro proces je izbira zvoka — sodba postane **Overfit** neglede kako dobro zmagovalec
pogledal.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Ko rodno cTrader Console optimizacijo pristane, cMind bo prehranu svoje celoten poskusiti površino tu
avtomatično.

## Preskusi — število, ki se šteje

`Trials` je **kako mnogi parametra nastavitve ste testirali** pred izbiro to. Testiranje ena strategijo in
testiranje deset tisoč in vzdrževanje najboljši so divje drugače stvari: druga izdelava a
visoko v-vzorec Sharpe z naključje. Hranitev pošten poskusiti štet je celoten točka — ga dviguje
deflacija in se lahko premakne a "velika" backtest da **Overfit**. Ko rodno cTrader Console optimizacijo
pristane, cMind prehranu to sweep-a pravi mrežo velikost avtomatično.

## Vnosi

- **Periodična povračil** — eno število na obdobja (npr. `0.01` = +1%). Vsaj dva.
- **Kapitala / saldo krivulja** — cMind izvaja zaporedni prost povračil za vas.
- Ali teči ga neposredno na dokončan backtest: `POST /api/quant/integrity/backtest/{instanceId}` bere ga
  shranjen poročilo kapitala krivulja.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```
