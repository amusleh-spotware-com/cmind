---
title: 0004 — CBotBuilder beží na webovom hostiteľovi v sandboxovanom kontajneri
description: Prečo sa nedôveryhodné zostavenia cBot uskutočňujú na webovom hostiteľovi vo vyhoditeľnom SDK kontajneri namiesto na uzle.
---

# 0004 — `CBotBuilder` beží na webovom hostiteľovi v sandboxovanom kontajneri

## Kontext

Budovanie cBotu používateľa znamená spustenie **nedôveryhodného MSBuild** — ľubovoľný kód v čase zostavenia (ciele,
generátory zdrojov, skripty obnovenia). Potrebuje zástrčku Docker na spustenie SDK kontajnera. Uzly
spúšťajú obchodné kontajnery a nemali by ani vlastniť oprávnenia k budovaniu.

## Rozhodnutie

`CBotBuilder` beží **na webovom hostiteľovi** (ktorý už má zásuvku Docker), vo vnútri **vyhoditeľného SDK
kontajnera** s:

- adresárom `/work` viazaným na bind (iba vstupy/výstupy budovania, nie hostiteľský súborový systém);
- zdieľaným zväzkom `app-nuget-cache` pre výkon obnovenia;
- bez prístupu k sieťovému hostiteľovi okrem toho, čo obnovenie potrebuje.

Takže nedôveryhodné MSBuild nemôže dosiahnuť hostiteľský súborový systém alebo sieť. Kontajnery spustenia/backtestingu na
rozdiel od toho bežia na uzloch vybratých `NodeScheduler`.

## Dôsledky

- Oprávnenie na budovanie (zásuvka Docker) je obmedzené na webový hostiteľ; uzly iba spúšťajú povolené obchodné obrázky.
- Každé budovanie je izolované v jednorazovom kontajneri — škodlivé budovanie nemôže pretrvávať alebo uniknúť.
- Webový hostiteľ musí mať k dispozícii zásuvku Docker; toto je požiadavka na nasadenie, nie voliteľné.
