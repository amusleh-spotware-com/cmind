---
description: "Stratégia Egészsége & Alfa Csökkenés — determinisztikus csökkenésdetektálás, amely összehasonlítja egy stratégia legújabb Sharpe-értékét a korábbi rekorddal és megtalálja a legnagyobb átlageltolódást (CUSUM változáspont), majd egy Egészséges / Romlódó / Elhalványult ítéletet ad vissza."
---

# Stratégia Egészsége & Alfa Csökkenés

Minden előny csökken — a kutatás egyértelműen azt mutatja, hogy a kvantitatív stratégia félélettartama évekről hónapokra romlott, így az *adaptáció felülmúlja a felfedezést*. A Stratégia Egészsége Monitor azt mondja meg a stratégia saját hozam előzménye alapján, hogy az előny még fennáll-e.

Nyissa meg a **cBots → Stratégia Egészsége** (`/quant/health`) oldalt.

## Mit csinál

Egy hozamsor (vagy részvénygörbe, a legrégebbi először) alapján:

- az előzményt egy **korábbi** és egy **újabb** felére osztja és összehasonlítja a Sharpe-rációikat;
- futtat egy **CUSUM változáspontot** keresve, hogy megtalálja azt a megfigyelést, ahol az átlag a legnyilvánvalóbban eltolódott (egy rezsimváltás), amely csak akkor kerül jelentésre, ha az eltérés statisztikailag figyelemre méltó;
- egy ítéletet ad vissza:

| Ítélet | Jelentés |
|---|---|
| **Egészséges** | A közelmúltbeli teljesítmény összhangban van a korábbi rekorddal, vagy jobb annál. |
| **Romlódó** | Az újabb Sharpe-érték lényegesen gyengébb, mint a korábbi rekord — gondosan figyelemmel kísérje. |
| **Elhalványult** | Az előny gyakorlatilag eltűnt az újabb időszakban — fontolja meg a szüneteltetést. |
| **Ismeretlen** | Nincs elegendő előzmény az ítélethez. |

- **Közvetlenül egy backtest futásból — másolás-beillesztés nélkül.** Minden befejezett backtest egy szív **Stratégia egészsége ellenőrzése** ikont tesz elérhetővé a **Backtest** lista során és a példány részletei nézeten; egy kattintás futtatja a Monitort a futás tárolt részvénygörbéjén és megjeleníti az ítéletet egy párbeszédben. Az ikon addig van letiltva, amíg a backtest nem fejeződik be és nem ad ki jelentést, így soha nem egy üres vezérlő. A háttérben ez a `POST /api/quant/health/backtest/{instanceId}`, amely a tárolt jelentés részvénygörbéjét olvassa.

```http
POST /api/quant/health
{ "returns": [...] }   // vagy { "equity": [...] }
```

## Miért megbízható

Ez tiszta, determinisztikus tartományi kód (`Core.Health`) infrastruktúra-függőség nélkül és külső hívások nélkül — tesztelve a csökkent, romlódó, egészséges és túl rövid esetek és a változáspont-lokalizáció tekintetében. Ez az autonóm ügynököket támogató mindig bekapcsolt egészségségellenőrzések kézi kiegészítése: ugyanezek a statisztikák hajtják a körforgalmi szünetet, amely kockázattalanítja az élő stratégiát, amelynek előnye elhalványul.
