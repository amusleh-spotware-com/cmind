---
title: 0004 — CBotBuilder běží na webovém hostiteli v kontejneru sandbox
description: Proč se nedůvěryhodné stavby cBot vyskytují na webovém hostiteli uvnitř jednorázového SDK kontejneru místo na uzlu.
---

# 0004 — `CBotBuilder` běží na webovém hostiteli v kontejneru sandbox

## Kontext

Vytváření cBot uživatele znamená spuštění **nedůvěryhodného MSBuild** — libovolného kódu v čase kompilace (cíle,
generátory zdrojů, skripty obnovení). Potřebuje soket Docker k otevření SDK kontejneru. Uzly
spouštějí obchodní kontejnery a neměly by také mít práva k sestavení.

## Rozhodnutí

`CBotBuilder` běží **na webovém hostiteli** (který již má soket Docker), uvnitř **jednorázového SDK
kontejneru** s:

- adresářem připojeným `/work` (pouze vstupů/výstupů stavby, ne systém souborů hostitele);
- sdíleným svazkem `app-nuget-cache` pro výkon obnovení;
- bez přístupu k síti hostitele kromě toho, co obnovení potřebuje.

Takže nedůvěryhodný MSBuild nemůže dosáhnout systému souborů nebo sítě hostitele. Kontejnery spuštění/backtestování
naopak běží na uzlech vybraných `NodeScheduler`.

## Důsledky

- Právo na stavbu (soket Docker) je omezeno na webový hostitel; uzly spouštějí pouze povolené obchodní image.
- Každá stavba je izolována v jednorazovém kontejneru — škodlivá stavba se nemůže zachovat nebo uniknout.
- Webový hostitel musí mít dostupný soket Docker; toto je požadavek nasazení, není volitelné.
