---
title: Pozicio-meretezes es Portfolio
description: "Institucialis pozicio-meretezesi technikak a retail szamara - volatilit targetiras es frakcionalis-Kelly kitevodes egy strategiara, plusz inverz-volatilitas risk-parity allokacio egy korrelicios matrix-szal strategia konyv felett."
---

# Pozicio Meretezes es Portfolio

"Mekkora kellene ennek a kereskedesnek lennie?" Ez a kerdes donti el, hogy egy élő összetett vagy felrobban. Az intézmények ezt **volatilitás targetálással** és a **Kelly kritériummal** válaszolják meg, és könyvet építenek **risk parity**-val a egyenlő dollárok helyett. A cMind mindkettőt elhozza a retailnek - determinisztikus matek egy stratégia hozamsorozatán,Plain-English ajánlással.

Nyisd meg a **cBots → Pozicio Meretezes** (`/quant/sizing`)-t.

## Egy-stratégia meretezes

Egy stratégia hozamait (vagy egyenleg görbéjét), egy cél éves volatilitást, egy Kelly törtet és egy tőkeáttételi sapkát adva, a méretező jelenti:

- **Realizált éves volatilitás** - a stratégia saját volatilitása, évesítve a négyzetgyök-of-time szabállyal.
- **Volatilitás-cél méretezes** - az expozíció, ami a realizált volatilitást a célodhoz igazítja (`cél / realizált vol`), sapkázva a tőkeáttételi korlátodig. Az alacsonyabb-vol stratégiák több méretet kapnak.
- **Teljes Kelly** - a növekedés-optimális hányad `f* = μ / σ²` (hozamok átlaga / variancia).
- **Frakcionális Kelly** - `f*` skálázva a Kelly törteddel. Fél-Kelly (0.5) a gyakori biztonságos választás; a teljes Kelly hírhedten túl agresszív a valódi, bizonytalan élőkhöz.
- **Javasolt expozíció** - a **kisebb** (biztonságosabb) a volatilitás-cél és a frakcionális-Kelly méretezések közül, sapkázva. Egy pozitív élő nélküli stratégia (teljes Kelly <= 0) nullára van méretezve.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio allokacio

Adj kettő vagy több stratégiát (illeszkedő hozamsorozatok) és egy könyvet épít **inverz-volatilitás risk parity**-val - minden stratégia `1 / volatilitás` szerint súlyozva, normalizálva - így a kockázat, nem a dollár, egyenlően megosztva. Visszaadja még:

- a **korrelációs mátrixot** a stratégiáid között (lásd, melyikek titokban ugyanaz a fogadás);
- a **projektált portfólió volatilitást** ezeknél a súlyoknál, a minta kovarianciából;
- egy **tőkeáttételi** faktort, ami skálázza a teljes könyvet a cél volatilitásod felé (sapkázva).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Miért megbizhato

Mind tiszta, determinisztikus domain kod (`Core.Portfolio`) nincs infrastruktura függőséggel és nincs külső hívás - unit-tesztelt a vol-cél méretezésre, a Kelly képletre, az inverz-volatilitás súlyok egyenlő-kockázat tulajdonságára, és a korrelációs mátrixra. Tanácsadói alapon: a számok egy ajánlás, sosem automatikus megbízás.
