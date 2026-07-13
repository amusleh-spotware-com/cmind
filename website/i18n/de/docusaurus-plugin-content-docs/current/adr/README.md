---
title: Architecture Decision Records
description: Die nicht offensichtlichen Design-Entscheidungen hinter cMind – Kontext, Entscheidung und Konsequenzen – die du nicht aus dem Code lesen kannst.
---

# Architecture Decision Records

Diese dokumentieren die Design-Entscheidungen, die du **nicht aus dem Code ableiten kannst** – die Trade-Offs, die Wege, die nicht gewählt wurden, und warum. Jeder ist kurz: *Kontext → Entscheidung → Konsequenzen*. Neue strukturelle Entscheidung → füge ein ADR hier hinzu (nächste Nummer), damit der nächste Engineer (Mensch oder KI) die Begründung erbt, nicht nur das Ergebnis.

| # | Entscheidung |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Striktes DDD mit reinem `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | Instance-Status ist TPH; ein Übergang ersetzt die Entity |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI Nodes sind HTTP + JWT, keine SSH/Shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` läuft auf dem Web-Host in einem Sandbox-Container |
| [0005](./0005-anthropic-raw-http.md) | Der KI-Client verwendet rohes HTTP, nicht das Anthropic SDK |
| [0006](./0006-copy-profile-db-lease.md) | Copy-Hosting wird durch einen atomaren DB-Lease koordiniert |
