---
title: 0006 — Gostovanje kopije je usklajeno z atomsko DB zakupo
description: Zakaj so kopije profilov zahtevane preko atomske Postgres zakupe namesto namenske koordinatorja in kako to preprečuje dvojno kopiranje.
keywords: zakupa, kopiranje, Postgres, koordinacija, atomska
---

# 0006 — Gostovanje kopije je usklajeno z atomsko DB zakupo

## Kontekst

Tekoči kopijski profil mora biti gostovan s **točno enim** vozliščem — dve vozlišči na istem profilu pomeni
vsak vir trgovanja se ogleda dvakrat (pravi denar izgubljen). Vozlišča prihajajo in grejo (skaliranje, sesutja, drseče
posodobitve), in ne želimo ločene storitve koordinatorja, ki teče in ostane živa.

## Odločitev

Vsak `CopyEngineSupervisor` zahteva profile z **atomsko DB zakupo** na tabeli `CopyProfiles`:

- **Zahtevek** — atomski `ExecuteUpdate` (ali `FOR UPDATE SKIP LOCKED` pri omejevanju na vozlišče) vzame
  profile, ki niso dodeljeni *ali* katerih zakupa je potekla. Atomskost pomeni dve tekmovalki nadzorniku
  nikoli ne zahtevata iste vrstice.
- **Obnova** — živega vozlišča osvežuje svojo zakupo vsak cikel, zato ohrani svojo zahtevo.
- **Ponovno zahtevanje** — zakupa sesutega vozlišča poteče in preživelec izbere profil na naslednjem ciklu
  (samo-popravljanje). Na gracioznem zaustavitvi vozlišče **sprosti** svoje zakupe takoj, zato je selitev hitra.
- **Čuvar** — gostitelj, katerega nalogo je zapustil, medtem ko je profil še naš, se ponovno zažene.
- Usklajitev je tresena, da se izogne gromu posodobitev `UPDATE` pri obsegu.

## Posledice

- Nobenega samostojnega koordinatorja, ki bi ga bilo treba uvrstiti ali vzdrževati - Postgres je edini vir resnice.
- Dvojno kopiranje je preprečeno z atomskostjo ravni vrst, ne z zaklepanjem na ravni aplikacije.
- Latenca selitve je omejena z TTL zakupe (minus hitra-pot graciozna sprostitev).
- To je prava denarni pot; varuje jo deterministična stresna test (DST) — nikoli ne oslabite scenarija DST
  da bi ga prepeljali.
