---
title: 0001 — Stroga DDD s čisto Core
description: Zakaj je poslovna logika postavljena na agregaty v Core projektu brez infrastrukturnih odvisnosti.
keywords: DDD, Domain-Driven Design, arhitektura, Core, čista logika
---

# 0001 — Stroga DDD s čisto `Core`

## Kontekst

Ta aplikacija premika pravi denar. Poslovni pravila, raztresena po koncnih točkah, storitve na ozadju in Razor
komponente, se počasi slabšajo v netestabilno, nedosledna obnašanja — točno tam, kjer napaka stane uporabnika kapital.

## Odločitev

Poslovna logika se nahaja **na agregatih, objektih vrednosti in domenskih storitvah** v `src/Core`, ki
se prevaja z **ničnimi infrastrukturnimi odvisnostmi** (brez EF, HttpClient, Docker ali ASP.NET). Koncne točke,
MCP orodja, komponente in `BackgroundService`s **orkestrata** — nikoli ne odločajo. Pravila:

- Brez javnih nastavitejev; spremembe stanja se izvršijo preko metod, ki so jasno namensko orientirane in varujejo invariante.
- Agregati se sklicujejo drug na drugega prek **močnih ID-jev**, ne preko svojstva navigacije.
- Ena `SaveChanges` mutira **en** agregat; tokovi med agregati uporabljajo domenske dogodke.
- Primitivi, ki prečkajo domenske meje, so oviti v objekte vrednosti.
- Kršitve invariant sprožijo Core `DomainException`, ne okvirnega izjema.

## Posledice

- Domenski pravili so testljiva brez podatkovne baze ali spletnega gostitelja.
- Čistost `Core` je strojno vsiljeno s strani `ArchitectureGuardTests` in bi neuspešno zgradila, če bi bila prekršena.
- Obstaja več ceremonije (objekti vrednosti, močni ID-ji, domenski dogodki) kot pri anEmičnem modelu — to je
  namenski strošek, da se pravila premikanja denarja ohranijo pravilna in na enem mestu.
