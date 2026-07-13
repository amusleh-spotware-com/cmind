---
title: Tranzakcios Koltseg Analizis (TCA)
description: "Merok az vegheritesi minoseget (csuszás bps-ben es implementation shortfall) egy megbizsnak az erkezesi ara ellen, az osszehasonlithato execute edge, amit a bankok elnek. Determinisztikus."
---

# Tranzakcios Koltseg Analizis (TCA)

Az execute alpha kicsi per kereskedes es hatalmas sok ezer felett - ez az, amit a bankok es prop deskek megtartanak az élőből. A TCA méri, mennyire driftelt el az ara, amit ténylegesen elértél, attól az ártól, amikor *döntöttél* kereskedni.

Nyisd meg a **cBots → Execute Koltseg** (`/quant/tca`)-t.

## Mit mer

Az **erkezesi (döntési) árat**, az **oldalt**, és a **fills**-t (ár × mennyiség) adva, jelenti:

- **Atlagos fill ara (VWAP)** - a volume-súlyozott ara, amit ténylegesen kaptál.
- **Csuszás (bps)** - a drift az erkezesitol a VWAP-ig basisz pontokban, **előjeles, igy egy pozitiv szám költség** (vásárlás az erkezesi felett vagy eladas alatta) es egy negativ szám az arajavulas.
- **Implementation shortfall** - a költség árat × mennyiség kifejezésben: a drift mennyibe került ezen a megbizason.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart szeletelés (Almgren-Chriss)

A költség méren túl, a cMind képes egy nagy megbizást *minimalizálni* azt. **cBots → Execute Schedule** (`/quant/execution`) épít egy **Almgren-Chriss optimális-végrehajtási ütemtervet**: az össz mennyiség, szeletek száma, kockázatkerülés, volatilitás és átmeneti piaci hatás adva, visszaadja a méretet minden szeletben. Nagyobb kockázatkerülés **előre tölti** az ütemtervet (időzítési kockázatot csökkentve); nulla kockázatkerülés laposít egyenletes **TWAP**-ra. A szeletek mindig összeadnak a teljeshez.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Miért megbizhato

Tiszta, determinisztikus domain kod (`Core.Execution`) nincs infrastruktura függőség és nincs külső hívás - unit-tesztelt a buy/sell költség előjelre, arajavulásra, nulla csuszásra, VWAP aggregációra és bemeneti őrökre. Ez a mérés fele a végrehajtási minőségnek; ez ugyanaz a shortfall metrika, amit a másolási motor használ a mirrored megbízások költségének megítélésére (és az intelligens szeleteléssel csökkentésére).
