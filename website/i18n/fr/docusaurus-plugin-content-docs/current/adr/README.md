---
title: Architecture Decision Records
description: Les décisions de conception non évidentes derrière cMind — contexte, décision et conséquences — que vous ne pouvez pas lire hors du code.
---

# Architecture Decision Records

Celles-ci enregistrent les décisions de conception que vous **ne pouvez pas déduire du code** — les compromis, les chemins non empruntés, et pourquoi. Chacun est court : *Contexte → Décision → Conséquences*. Nouvelle décision structurelle → ajouter un ADR ici (numéro suivant) afin que le prochain ingénieur (humain ou IA) hérite du raisonnement, pas seulement du résultat.

| # | Décision |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | DDD strict avec un `Core` pur |
| [0002](./0002-tph-instance-replaces-entity.md) | L'état de l'instance est TPH ; une transition remplace l'entité |
| [0003](./0003-external-nodes-http-jwt.md) | Les nœuds cTrader CLI sont HTTP + JWT, pas de SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` s'exécute sur l'hôte web dans un conteneur sandbox |
| [0005](./0005-anthropic-raw-http.md) | Le client IA utilise raw HTTP, pas le SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | L'hébergement de copie est coordonné par un bail atomique DB |
