---
title: AI masolasi ajanlo
description: "Az AI elemzi a masolasi szolgaltatok teljesitmenyet, kockazatat es stilusat, es szemelyre szabott ajanlasokat tesz arra, hogy kit erdemes masolni."
---

# AI masolasi ajanlo

Az AI elemzi a másolási szolgáltatók teljesítményét, kockázatát és stílusát, és személyre szabott ajánlásokat tesz arra, hogy kit érdemes másolni.

## Hogyan mukodik

Az ajánló a következő dimenziókban elemzi a szolgáltatókat:

- **Teljesítmény metrikák** - Sharpe ratio, max drawdown, nyerési arány, átlagos R:R
- **Kockázati profil** - volatilitás, expozíció, drawdown minták
- **Kereskedési stílus** - jelenlévő asset osztályok, kereskedési frekvencia, átlagos pozíció méret
- **Múltbeli teljesítmény** - 30/90/180 napos hozamok
- **Felhasználói metrikák** - másolók száma, összes másolt volumen

## Miért megbizhato

A kalkulációk tiszta domain kódon alapulnak (`Core.Ai.CopyRecommender`), nem használ külső AI API-t a metrikák számításához. Az AI csak a narratív magyarázatot generálja, nem változtatja meg az adatokat.

## Kapcsolodo funkciók

- **[Copy Trading](./copy-trading.md)** - a másolási kereskedési rendszer
- **[Copy Performance Fees](./copy-performance-fees.md)** - teljesítménydíjak
- **[Copy Notifications](./copy-notifications.md)** - értesítések
