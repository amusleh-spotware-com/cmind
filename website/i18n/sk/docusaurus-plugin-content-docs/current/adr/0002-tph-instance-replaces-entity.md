---
title: 0002 — Stav inštancie je TPH; prechod nahrádza entitu
description: Prečo sa ID inštancie mení, keď sa pohybuje v rámci jej životného cyklu, a prečo je ID kontajnera stabilný kľúč.
---

# 0002 — Stav inštancie je TPH; prechod nahrádza entitu

## Kontext

Inštancia spustenia/backtestingu prechádza stavmi (pending → scheduled → starting → running → terminal).
Modelujeme stav s EF Core **Table-Per-Hierarchy (TPH)**: každý stav je podtyp
(`StartingRunInstance`, `RunningRunInstance`, …). Stĺpec diskriminátora EF TPH **nemôže zmeniť** na
existujúcom riadku.

## Rozhodnutie

Prechod stavu **nahrádza entitu** novou inštanciou podtypu, skôr ako mutuje pole stavu.
Keďže sa riadok nahrádza, **ID inštancie zmení** naprieč starting → running → terminal.
**ID kontajnera je stabilné** a je prenášané naprieč prechodmi; HTTP uzol agenta je kľúčený podľa
ID kontajnera pre status/report/stop/logs.

## Dôsledky

- Každý stav je odlišný typ, iba s poľami a metódami platnými v tom stave — nelegálne
  prechody a nezmyselný prístup k poľu sú chyby kompilácie, nie kontroly za beh.
- Volajúci **nesmú** vložiť do vyrovnávacej pamäte ID inštancie naprieč prechodom; používajte ID kontajnera ako stabilný
  kľúč pre čokoľvek, čo sa rozprestiera stavy.
- Logika prechodu žije v `InstanceTransitions`; zmena ID je zámyselná, nie chyba.
