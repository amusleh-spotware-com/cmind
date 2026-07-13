---
title: 0004 — CBotBuilder s'exécute sur l'hôte web dans un conteneur sandbox
description: Pourquoi les builds de cBot non fiables se font sur l'hôte web dans un conteneur SDK jetable plutôt que sur un nœud.
---

# 0004 — `CBotBuilder` s'exécute sur l'hôte web dans un conteneur sandbox

## Contexte

Construire un cBot d'utilisateur signifie exécuter **MSBuild non fiable** — code arbitraire au moment de la construction (cibles, générateurs de sources, scripts de restauration). Il a besoin de la socket Docker pour créer un conteneur SDK. Les nœuds exécutent des conteneurs de trading et ne devraient pas non plus avoir les privilèges de construction.

## Décision

`CBotBuilder` s'exécute **sur l'hôte web** (qui a déjà la socket Docker), à l'intérieur d'un **conteneur SDK jetable** avec :

- un répertoire `/work` en bind-mount (uniquement les entrées/sorties de construction, pas le système de fichiers de l'hôte) ;
- un volume partagé `app-nuget-cache` pour la performance de restauration ;
- pas d'accès au réseau de l'hôte au-delà de ce dont la restauration a besoin.

Donc MSBuild non fiable ne peut pas atteindre le système de fichiers ou le réseau de l'hôte. Les conteneurs Run/backtest, par contrast, s'exécutent sur les nœuds choisis par `NodeScheduler`.

## Conséquences

- Le privilège de construction (socket Docker) est confiné à l'hôte web ; les nœuds exécutent uniquement des images de trading autorisées.
- Chaque construction est isolée dans un conteneur jetable — une construction malveillante ne peut pas persister ou s'échapper.
- L'hôte web doit avoir une socket Docker disponible ; c'est une exigence de déploiement, pas optionnel.
