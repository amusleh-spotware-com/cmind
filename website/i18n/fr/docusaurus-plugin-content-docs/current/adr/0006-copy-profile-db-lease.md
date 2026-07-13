---
title: 0006 — L'hébergement de copie est coordonné par un bail atomique DB
description: Pourquoi les profils de copie sont revendiqués via un bail Postgres atomique au lieu d'un coordinateur dédié, et comment cela empêche la double copie.
---

# 0006 — L'hébergement de copie est coordonné par un bail atomique DB

## Contexte

Un profil de copie en cours d'exécution doit être hébergé par **exactement un** nœud — deux hôtes sur le même profil signifie que chaque échange source est réfléchi deux fois (l'argent réel est perdu). Les nœuds viennent et partent (mise à l'échelle, crashes, mises à jour continues), et nous ne voulons pas qu'un service coordinateur séparé s'exécute et reste actif.

## Décision

Chaque `CopyEngineSupervisor` revendique les profils avec un **bail atomique DB** sur la table `CopyProfiles` :

- **Revendication** — un `ExecuteUpdate` atomique (ou `FOR UPDATE SKIP LOCKED` lors de la limitation par nœud) prend les profils qui sont non assignés *ou* dont le bail a expiré. L'atomicité signifie que deux supervisors en compétition ne revendiquent jamais la même ligne.
- **Renouvellement** — un nœud en direct actualise son bail chaque cycle, donc il conserve sa revendication.
- **Réclamation** — le bail d'un nœud défaillant expire, et un survivant récupère le profil lors de son prochain cycle (auto-guérison). À l'arrêt gracieux, le nœud **libère** ses baux immédiatement afin que le basculement soit rapide.
- **Watchdog** — un hôte dont la tâche a quitté alors que le profil est toujours le nôtre est redémarré.
- La réconciliation est dithérée pour éviter un troupeau déferlement d' `UPDATE`s à l'échelle.

## Conséquences

- Pas de coordinateur autonome à déployer ou à maintenir en bonne santé — Postgres est la source unique de vérité.
- La double copie est empêchée par l'atomicité au niveau des lignes, pas par le verrouillage au niveau de l'application.
- La latence de basculement est limitée par le TTL du bail (moins le chemin rapide de libération gracieuse).
- C'est le chemin de l'argent ; il est gardé par la suite de stress déterministe (DST) — ne jamais affaiblir un scénario DST pour le faire réussir.
