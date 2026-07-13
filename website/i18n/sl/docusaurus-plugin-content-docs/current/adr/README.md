---
title: Zapisi odločitev o arhitekturi
description: Očitne odločitve oblikovanja za cMind — kontekst, odločitev in posledice — ki jih ne boste mogli prebrati v kodi.
---

# Zapisi odločitev o arhitekturi

Ti zapisi beležijo odločitve oblikovanja, ki **jih ne morete sklepati iz kode** — kompromise, ceste,
ki niso vzete, in zakaj. Vsak je kratek: *Kontekst → Odločitev → Posledice*. Nova strukturna
odločitev → dodajte ADR tukaj (naslednja številka), da bo naslednji inženir (človek ali AI) podedoval
sklepanje, ne le rezultat.

| # | Odločitev |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Stroga DDD s čisto `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | Stanje instanci je TPH; prehod nadomesti entiteto |
| [0003](./0003-external-nodes-http-jwt.md) | Vozlišča CLI cTraderja so HTTP + JWT, brez SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` teče na spletnem gostiteljniku v peskovniku kontejnerja |
| [0005](./0005-anthropic-raw-http.md) | Odjemalec AI uporablja surovi HTTP, ne SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | Kopiranje gostovanja je usklajevano z atomskim zakupom DB |
