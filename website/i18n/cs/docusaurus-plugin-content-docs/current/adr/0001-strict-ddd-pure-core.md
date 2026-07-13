---
title: 0001 — Přísné DDD s čistým Core
description: Proč se doménová logika nachází na agregátech v projektu Core bez infrastrukturních závislostí.
---

# 0001 — Přísné DDD s čistým `Core`

## Kontext

Tato aplikace pohybuje skutečným penězi. Obchodní pravidla rozptýlená po koncových bodech, službách na pozadí a Razor komponentách se rozpadávají do netestovatelného, nekonzistentního chování — přesně tam, kde chyba stojí uživatele kapitál.

## Rozhodnutí

Doménová logika se nachází **na agregátech, objektech hodnot a doménových službách** v `src/Core`, který se kompiluje s **nulou infrastrukturních závislostí** (bez EF, HttpClient, Docker nebo ASP.NET). Koncové body, nástroje MCP, komponenty a `BackgroundService`s **orchestrují** — nikdy nerozhodují. Pravidla:

- Žádné veřejné settery; změny stavu prostřednictvím metod s jasným záměrem, které chrání invarianty.
- Agregáty se navzájem odkazují podle **silného ID**, nikoli navigační vlastnosti.
- Jeden `SaveChanges` zmutuje **jednu** agregaci; toky mezi agregáty používají doménové události.
- Primitivní prvky překračující doménovou hranici jsou zabaleny v objektech hodnot.
- Porušení invariantů vyvolá `DomainException` z Core, nikoli výjimku frameworku.

## Důsledky

- Doménová pravidla jsou testovatelná bez databáze nebo webového hostitele.
- Čistotu `Core` vynucuje `ArchitectureGuardTests` — selhalo by vytváření, pokud by se porušila.
- Je zde více ceremonie (objekty hodnot, silná ID, doménové události) než v anemickém modelu — to je záměrná cena za uchování správnosti pravidel, která pohybují penězi, a jejich umístění na jednom místě.
