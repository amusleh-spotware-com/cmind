---
title: 0001 — DDD strict avec un Core pur
description: Pourquoi la logique de domaine vit sur les agrégats dans un projet Core sans dépendances infrastructure.
---

# 0001 — DDD strict avec un `Core` pur

## Contexte

Cette app fait circuler de l'argent réel. Les règles métier éparpillées entre les endpoints, les services de fond, et les composants Razor se transforment en comportement non testable et incohérent — exactement où un bug coûte du capital à un utilisateur.

## Décision

La logique de domaine vit **sur les agrégats, les objets de valeur, et les services de domaine** dans `src/Core`, qui compile avec **zéro dépendances infrastructure** (pas d'EF, HttpClient, Docker, ou ASP.NET). Les endpoints, les outils MCP, les composants, et les `BackgroundService`s **orchestrent** — ils ne décident jamais. Règles :

- Pas de setters publics ; les changements d'état passent par des méthodes révélatrices d'intention qui gardent les invariants.
- Les agrégats se référencent entre eux par **ID fort**, pas par propriété de navigation.
- Un `SaveChanges` mute **un** agrégat ; les flux inter-agrégats utilisent les événements de domaine.
- Les primitives franchissant une frontière de domaine sont enveloppées dans des objets de valeur.
- Les violations d'invariants lèvent une `DomainException` Core, pas une exception framework.

## Conséquences

- Les règles de domaine sont testables unitairement sans base de données ou hôte web.
- La pureté de `Core` est appliquée par machine par `ArchitectureGuardTests` et échouerait le build si elle était brisée.
- Il y a plus de cérémonie (objets de valeur, ID forts, événements de domaine) qu'un modèle anémique — c'est le coût délibéré de garder les règles de circulation d'argent correctes et en un seul endroit.
