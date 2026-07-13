---
title: 0003 — Les nœuds cTrader CLI sont HTTP + JWT, pas SSH/shell
description: Pourquoi les agents de nœud distant exposent uniquement une API HTTP avec des JWT de courte durée et jamais un shell.
---

# 0003 — Les nœuds cTrader CLI sont HTTP + JWT, pas SSH/shell

## Contexte

Les conteneurs Backtest/run s'exécutent sur des hôtes distants. L'approche évidente — SSH et exécuter docker — donne à l'app principale l'exécution de code distant arbitraire et des identifiants de longue durée sur chaque nœud. C'est un rayon de blast large pour un système qui exécute les cBots d'utilisateurs non fiables.

## Décision

Chaque hôte distant exécute un agent **HTTP** `CtraderCliNode` autonome **sans SSH et sans shell**. L'app principale appelle l'agent via HTTP ; chaque requête porte un **JWT HS256** de courte durée (5 minutes, `iss=app-main` / `aud=app-node`) signé avec le secret de ce nœud. L'agent :

- n'exécute que les images correspondant à `AllowedImagePrefix` (avec une limite de chemin afin que `ghcr.io/spotware` ne peut pas correspondre à `ghcr.io/spotware-evil/...`) ;
- exécute docker via `ArgumentList` — jamais une chaîne de shell ;
- est **sans état**, trouvant les conteneurs par le label `app.instance` ;
- s'enregistre automatiquement et envoie des heartbeats vers `POST /api/nodes/register` ; l'app principale fait un upsert du `CtraderCliNode` **par nom**, donc un nœud survit aux changements d'IP.

## Conséquences

- Un token de requête fui expire en minutes ; il n'y a pas d'identifiant de shell permanent à voler.
- La capacité de l'agent est limitée à « exécuter une image autorisée » — il ne peut pas être transformé en un shell distant général.
- L'identité du nœud est basée sur le nom, donc re-provisionner un nœud avec une nouvelle IP n'orpheline pas son historique.
