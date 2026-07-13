---
title: Záznamy rozhodnutí o architektuře
description: Neočividná designová rozhodnutí za cMind — kontext, rozhodnutí a důsledky — kterou si nemůžete přečíst z kódu.
---

# Záznamy rozhodnutí o architektuře

Tyto záznamy zaznamenávají designová rozhodnutí, která **si nelze vyvodit z kódu** — kompromisy, nevybrané cesty a proč. Každý je krátký: *Kontext → Rozhodnutí → Důsledky*. Nové strukturální rozhodnutí → přidejte sem ADR (další číslo) aby příští inženýr (člověk nebo AI) zdědí logiku, ne jen výsledek.

| # | Rozhodnutí |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Přísné DDD s čistým `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | Stav instance je TPH; přechod nahrazuje entitu |
| [0003](./0003-external-nodes-http-jwt.md) | Uzly cTrader CLI jsou HTTP + JWT, bez SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` běží na webovém hostiteli v kontejneru sandbox |
| [0005](./0005-anthropic-raw-http.md) | Klient AI používá surový HTTP, ne SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | Hostování kopírování je koordinováno atomátem pronájmu DB |
