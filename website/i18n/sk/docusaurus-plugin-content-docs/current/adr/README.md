---
title: Architecture Decision Records
description: Netriviálne návrhy za cMind — kontext, rozhodnutie a dôsledky — ktoré nemôžete čítať z kódu.
---

# Architecture Decision Records

Tieto zaznamenávajú návrhy, ktoré **nemôžete odvodiť z kódu** — trade-offs, cesty nebrané a prečo. Každá je krátka: *Kontext → Rozhodnutie → Dôsledky*. Nový štrukturálny rozhodnutie → pridajte ADR tu (ďalšie číslo), takže ďalší engineer (human alebo AI) dedí rozumovanie, nie len rezultát.

| # | Rozhodnutie |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Striktný DDD s čistým `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | Stav instance je TPH; prechod zamení entitu |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI uzly sú HTTP + JWT, žiadny SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` beží na web host v sandbox kontajneri |
| [0005](./0005-anthropic-raw-http.md) | AI klient používa raw HTTP, nie Anthropic SDK |
| [0006](./0006-copy-profile-db-lease.md) | Copy hosting je koordinovaný atomic DB lease |
