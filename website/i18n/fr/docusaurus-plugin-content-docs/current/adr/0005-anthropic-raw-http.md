---
title: 0005 — Le client IA utilise raw HTTP, pas le SDK Anthropic
description: Pourquoi IAiClient appelle l'API Anthropic sur un HttpClient typé au lieu du SDK officiel, et pourquoi l'IA est entièrement contrôlée par une clé.
---

# 0005 — Le client IA utilise raw HTTP, pas le SDK Anthropic

## Contexte

Chaque fonctionnalité IA (génération de stratégie, auto-réparation, garde des risques, post-mortems) appelle l'API Anthropic. Une dépendance SDK ajoute une surface transitive que nous ne contrôlons pas, couple notre cadence de libération à la leur, et cache le contrat exact du fil que nous devons raisonner pour la résilience et le coût.

## Décision

`IAiClient` appelle Anthropic sur **raw HTTP** par un `HttpClient` typé — intentionnellement **pas** le SDK. `AiFeatureService` est l'orchestrateur unique partagé par les endpoints Web, les `AiTools` MCP, et `AiRiskGuard`. Toute la surface est **contrôlée par `AppOptions.Ai.ApiKey`** : sans clé, chaque fonctionnalité renvoie `AiResult.Fail` et l'app fonctionne inchangée.

## Conséquences

- Aucune clé n'est requise pour le build, le test, ou l'E2E — CI et le dev local exécutent l'app complète sans IA.
- Nous possédons la forme de requête/réponse, la politique de retry/timeout, et le compte des tokens explicitement.
- Les nouvelles fonctionnalités Anthropic doivent être câblées à la main ; nous échangeons la commodité pour le contrôle et une surface de dépendance plus petite. Voir la référence `claude-api` pour les id de modèles actuels et les paramètres.
