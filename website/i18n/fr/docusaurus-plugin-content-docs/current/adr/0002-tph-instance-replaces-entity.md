---
title: 0002 — L'état de l'instance est TPH ; une transition remplace l'entité
description: Pourquoi l'id d'une instance change à mesure qu'elle progresse dans son cycle de vie, et pourquoi l'id du conteneur est la clé stable.
---

# 0002 — L'état de l'instance est TPH ; une transition remplace l'entité

## Contexte

Une instance run/backtest se déplace à travers les états (pending → scheduled → starting → running → terminal). Nous modélisons l'état avec EF Core **Table-Per-Hierarchy (TPH)** : chaque état est un sous-type (`StartingRunInstance`, `RunningRunInstance`, …). La colonne de discriminateur TPH d'EF **ne peut pas changer** sur une ligne existante.

## Décision

Une transition d'état **remplace l'entité** par une nouvelle instance de sous-type plutôt que de muter un champ de statut. Parce que la ligne est remplacée, l'**id de l'instance change** entre starting → running → terminal. L'**id du conteneur est stable** et est porté entre les transitions ; l'agent de nœud HTTP est indexé par id de conteneur pour le statut/rapport/arrêt/logs.

## Conséquences

- Chaque état est un type distinct avec seulement les champs et méthodes valides dans cet état — les transitions illégales et l'accès à des champs dénués de sens sont des erreurs de compilation, pas des vérifications à l'exécution.
- Les appelants ne doivent **pas** mettre en cache un id d'instance entre une transition ; utilisez l'id du conteneur comme poignée stable pour tout ce qui s'étend sur les états.
- La logique de transition vit dans `InstanceTransitions` ; le changement d'id est intentionnel, pas un bug.
