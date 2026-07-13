---
title: 0003 — Vozlišča cTrader CLI so HTTP + JWT, brez SSH/shell
description: Zakaj daljinska vozlišča agent izpostavljajo le HTTP API s kratkoživimi JWT-ji in nikoli shell.
keywords: HTTP, JWT, varnost, vozlišče, cTrader CLI
---

# 0003 — Vozlišča cTrader CLI so HTTP + JWT, brez SSH/shell

## Kontekst

Posode backtest/run se izvršujejo na oddaljenih gostiteljih. Očitni pristop — SSH in zaženite docker — daje
glavnemu programu poljubno oddaljeno izvajanje kode in dolgotrajne poverilnice na vsakem vozlišču. To je
velik obseg pri sistemu, ki teče nezaupane uporabniške cBote.

## Odločitev

Vsak oddaljeni gostitelj teče samostojnega `CtraderCliNode` **HTTP agenta** z **brez SSH in brez shell**. Glavni program
klice agenta preko HTTP; vsaka zahteva nosi kratkoživji **HS256 JWT** (5 minut,
`iss=app-main` / `aud=app-node`) podpisan s skrivnostjo tega vozlišča. Agent:

- teče le slike, ki se ujemajo z `AllowedImagePrefix` (z omejitve poti, tako da `ghcr.io/spotware` ne more
  ujemati `ghcr.io/spotware-evil/...`);
- izvršuje docker preko `ArgumentList` — nikoli string shell;
- je **brez stanja**, iskanje posod po oznaki `app.instance`;
- se samoregistrira in bije srce do `POST /api/nodes/register`; glavni program vstavlja `CtraderCliNode`
  **po imenu**, zato vozlišče preživi spremembe IP.

## Posledice

- Puščeni žeton zahteve se poteka v nekaj minutah; ni trajne poverilnice shell, ki bi jo bilo mogoče ukrasti.
- Sposobnost agenta je omejena na "teči dovoljeno sliko" — ne more biti spremenjena v splošni
  oddaljeni shell.
- Istovetnost vozlišča je imena temelječe, zato ponovno pripravo vozlišča z novim IP ne ločarijo njegove zgodovine.
