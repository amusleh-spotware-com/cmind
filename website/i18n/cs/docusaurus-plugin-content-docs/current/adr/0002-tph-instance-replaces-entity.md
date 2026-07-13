---
title: 0002 — Stav instance je TPH; přechod nahrazuje entitu
description: Proč se ID instance mění při průchodu svým životním cyklem a proč je ID kontejneru stabilním klíčem.
---

# 0002 — Stav instance je TPH; přechod nahrazuje entitu

## Kontext

Instance spuštění/backtestování prochází stavy (pending → scheduled → starting → running → terminal).
Modelujeme stav s EF Core **Table-Per-Hierarchy (TPH)**: každý stav je podtyp
(`StartingRunInstance`, `RunningRunInstance`, …). Sloupec TPH diskriminátoru EF **se nemůže změnit** na
existujícím řádku.

## Rozhodnutí

Přechod stavu **nahrazuje entitu** novou instancí podtypu spíše než mutace pole stavu.
Protože se řádek nahradí, **ID instance se mění** přes starting → running → terminal.
**ID kontejneru je stabilní** a je přeneseno při přechodech; agent uzlu HTTP je klíčen podle
ID kontejneru pro status/report/stop/logs.

## Důsledky

- Každý stav je odlišný typ pouze s poli a metodami, které jsou v tomto stavu platné — ilegální
  přechody a nesmyslný přístup k poli jsou chyby kompilace, ne kontroly za běhu.
- Volající **nesmí** ukládat ID instance do mezipaměti přes přechod; použijte ID kontejneru jako stabilní
  handle pro cokoli, co sahá přes stavy.
- Logika přechodu se nachází v `InstanceTransitions`; změna ID je záměrná, ne chyba.
