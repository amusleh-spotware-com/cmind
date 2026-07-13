---
title: 0004 — A CBotBuilder a web-gazdagépen futtatási konténerben futnak
description: Miért nem megbízható cBot-építések a web-gazdagépen fordulnak elő egy eldobható SDK-konténeren belül, nem pedig egy node-on.
---

# 0004 — `CBotBuilder` a web-gazdagépen futtatási konténerben futnak

## Kontextus

Egy felhasználó cBot-jának építése azt jelenti, hogy **nem megbízható MSBuild**-et futtatunk — tetszőleges kód a fordítási időpontban (célok, forrás-generátorok, helyreállítási szkriptek). Szüksége van a Docker sockethoz egy SDK-konténer létrehozásához. A node-ok üzletkötési konténereket futtatnak, és nem kellene építési jogosultságuk is legyen.

## Döntés

A `CBotBuilder` **a web-gazdagépen futtatódik** (amely már rendelkezik a Docker sockettal), egy **eldobható SDK-konténeren belül**, amely:

- egy kötött `/work` könyvtárral rendelkezik (csak az építési bemenetek/kimenetek, nem a gazdagép fájlrendszere);
- egy megosztott `app-nuget-cache` kötet a helyreállítás teljesítményéhez;
- nincs gazdagép-hálózati hozzáférés, amely a helyreállításon túlmutat.

Így a nem megbízható MSBuild nem érheti el a gazdagép fájlrendszerét vagy hálózatát. A futtatási/backtest-konténerek ezzel szemben a `NodeScheduler` által kiválasztott node-okon futnak.

## Következmények

- Az építési jogosultság (Docker socket) a web-gazdagéphez korlátozódik; a node-ok csak az engedélyezett üzletkötési lemezképeket futtatják.
- Minden építés egy eldobható konténerben izolálódik — egy rosszindulatú építés nem maradhat meg vagy szökhetnek meg.
- A web-gazdagépnek rendelkeznie kell egy elérhető Docker socket-tal; ez egy telepítési követelmény, nem választható.
