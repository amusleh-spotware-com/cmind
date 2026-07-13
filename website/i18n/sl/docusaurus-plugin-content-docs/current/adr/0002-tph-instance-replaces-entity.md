---
title: 0002 — Stanje instance je TPH; prehod zamenja entiteto
description: Zakaj se ID instance spremeni, ko se premika skozi svoj cikel, in zakaj je ID posode stabilen ključ.
keywords: TPH, Table-Per-Hierarchy, instance, stanje, EF Core
---

# 0002 — Stanje instance je TPH; prehod zamenja entiteto

## Kontekst

Instanca teka/backtesta se premika skozi stanja (pending → scheduled → starting → running → terminal).
Stanje modeliramo z EF Core **Table-Per-Hierarchy (TPH)**: vsako stanje je podtip
(`StartingRunInstance`, `RunningRunInstance`, …). EF-jev TPH diskriminatorski stolpec **se ne more spremeniti** na
obstoječi vrstici.

## Odločitev

Prehod stanja **zamenja entiteto** z novo instanco podtipa namesto mutiranja polja statusa.
Ker je vrstica zamenjana, se **ID instance spremeni** med starting → running → terminal.
**ID posode je stabilen** in se nosi čez prehode; agent HTTP vozlišča je ključen s ključem ID posode
za status/poročilo/stop/dnevnike.

## Posledice

- Vsako stanje je posebna vrsta z le polji in metodami, veljavnimi v tem stanju — neveljavni
  prehodi in nesmiselni dostopi do polj so napake pri prevajanju, ne preverki med izvajanjem.
- Klicatelji **ne smejo** predpomnilnik ID instance čez prehod; uporabite ID posode kot stabilen
  ročaj za karkoli, kar traja med stanji.
- Logika prehoda se nahaja v `InstanceTransitions`; sprememba ID je namenorna, ne napaka.
