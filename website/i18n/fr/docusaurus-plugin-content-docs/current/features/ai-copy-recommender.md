---
description: "Aide IA. Recommande des paramètres de destination de copie commerciale sûrs à partir du profil de risque du follower + description du compte source (maître). Exposé via REST API, MCP…"
---

# Recommandeur de profil de copie IA

Aide IA. Recommande des paramètres de destination de copie commerciale sûrs à partir du profil de risque du follower + description du compte source (maître). Exposé via REST API, outil MCP, page Copy Trading. Consultatif uniquement — ne crée/mute jamais le profil ; l'humain (ou l'appel MCP ultérieur) applique les paramètres.

## Modèle

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — construit une requête à partir
  du message d'invite `AiPrompts.CopyProfileSystem`, retourne `AiResult` dont le texte = objet JSON de paramètres
  suggérés : `riskMode` (un nom `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, courte `rationale`.
- Comme toute feature IA, contrôlée par `App:Ai:ApiKey` : pas de clé → l'appel retourne
  `AiResult.Fail(disabled)`, l'app ne change pas.

## Surfaces

| Surface | Entrée |
|---------|--------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, rôle User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, délègue au service IA) |
| UI | Page Copy Trading → bouton **AI suggest** ; la recommandation s'affiche dans une alerte en ligne |

La recommandation n'est pas auto-appliquée à dessein : le follower examine, puis crée un profil /
destination via la boîte de dialogue Copy Trading normale (ou le client MCP analyse le JSON + appelle les endpoints de création).

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs` : profil de risque + description source
  transféré au client IA sous le message d'invite du profil de copie (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs` : pas de clé API → `AnthropicAiClient`
  réel + `AiFeatureService` se dégradent en résultat d'échec (l'app fonctionne sans clé).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs` : le bouton **AI suggest** appelle l'endpoint + affiche
  le résultat (message gracieux "not configured" en env de test), prouvant le chemin UI → endpoint → IA.
