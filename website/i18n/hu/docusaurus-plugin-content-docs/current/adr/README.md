---
title: Architekturális Döntési Feljegyzések
description: Az nem nyilvánvaló tervezési döntések mögött a cMind — kontextus, döntés és következmények — amit nem olvashat le a kódról.
---

# Architekturális Döntési Feljegyzések

Ezek a tervezési döntéseket rögzítik, amelyeket **nem lehet levezetni a kódról** — az kompromisszumokat, az utak nem vehetik, és miért. Mindegyik rövid: *Kontextus → Döntés → Következmények*. Új szerkezeti döntés → adjon egy ADR-et ide (következő szám), így a következő mérnök (ember vagy AI) örökli az érvelést, nem csak az eredményt.

| # | Döntés |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Szigorú DDD egy tiszta `Core`-ral |
| [0002](./0002-tph-instance-replaces-entity.md) | Az instancia állapota TPH; az átmenet helyettesíti az entitást |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI node-jai HTTP + JWT, nincs SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` futtatódik a web hostnon egy sandbox konténerben |
| [0005](./0005-anthropic-raw-http.md) | Az AI kliens nyers HTTP-t használ, nem az Anthropic SDK-t |
| [0006](./0006-copy-profile-db-lease.md) | Másolási üzemeltetés egy atomi DB bérleten keresztül koordinálódik |
