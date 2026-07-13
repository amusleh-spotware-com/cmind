---
title: Kontrainszsz szaraz pozicionalas
description: "A retail kereskedok long szazalek-at egy kontrainssz indicaatorra forditja (fade the crowd amikor loiposult), plusz pont-idoben szignal ertekek, amelyek guardolnak a look-ahead bias ellen."
---

# Kontrainsszsz Retil Pozicionalas

A retail tömeg az egyik leghasznosabb szentiment szignal a FX-ben - mint **kontrainssz** indikator. Amikor a retail kereskedők többsége long, az ár történetesen hajlamos esni, és fordítva. Ez az eszköz a tömeg pozicionalasat egy cselekvésképess szöveggé alakítja.

Nyisd meg a **cBots → Kontrainssz Pozicionalas** (`/quant/positioning`)-t.

## Mit csinal

Add meg a **% of retail traders long**-ot (a brokered sentiment oldalarol vagy egy feed-bol, mint FXSSI) es visszaadja:

- **Kontrainssz bias** - **Medve** ha >= 60% long (tomeg tul hosszu), **Bika** ha <= 40% long (tomeg tul rövid), **Semleges** a 40-60% döntési sávban.
- **Erősség** - mennyire loiposult a tomeg (0 = kiegyensulyozott, 1 = teljesen egy oldalon), hogy súlyozza a szignalt.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Pont-idoben konstrukcio altal

A szignal réteg (`Core.Signals`) egy `PointInTimeSignal`-t modellez, amely **bélyegzve van azzal a pillanattal, amikor megismerhető volt**, és megtagadja, hogy nélküle épüljönön. Bármely backtest vagy autonóm ügynök, amely fogyaszt egy szignalt, ellenőrzi az `IsKnownAt(decisionTime)`-t - így a jövőbeli adatok sosem szivároghatnak egy historikus döntésbe. A look-ahead bias a legfőbb reprodukálhatósági gyilkos a kvantitatív pénzügyekben; a domain modell strukturálisan lehetetlenné teszi.

## Miért megbizhato

Tiszta, determinisztikus domain kod nincs infrastruktura fuggoseggel - a kontrainssz küszöbök és a pont-idő őr tiszta, determinisztikus matek a 40/60 hatarokkal és out-of-range elutasítással unit-tesztelt.
