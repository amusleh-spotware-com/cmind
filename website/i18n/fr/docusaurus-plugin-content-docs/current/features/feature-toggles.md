---
description: "Le déploiement white-label fournit rarement toutes les capacités. Les bascules de feature permettent à l'opérateur de activer/désactiver les features du produit principal — au moment du déploiement via config, ou plus tard à…"
---

# Bascules de feature

Le déploiement white-label fournit rarement toutes les capacités. Les bascules de feature permettent à l'opérateur de activer/désactiver les features du produit principal — au moment du déploiement via config, ou plus tard lors de l'exécution, pas de redéploiement. **Toutes les features sont activées par défaut** ; le déploiement ne liste que celles qu'il change.

## Modèle

- `Core.Features.FeatureFlag` — énumération de features gérées : `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Les surfaces d'administration Core (dashboard, utilisateurs, nœuds, auth) ne sont jamais gérées, pas ici.
- `Core.Options.FeaturesOptions` — ligne de base de config, liée de `App:Features`. Chaque propriété
  est par défaut `true`.
- `Core.Features.IFeatureGate` — résout l'état **effectif** : ligne de base de config surposée
  avec remplacement d'exécution optionnel défini par le propriétaire. Implémenté par `Infrastructure.Features.FeatureGate`,
  met en cache brièvement les remplacements (`FeatureSettings.OverrideCacheTtl`), invalide au changement.

Les remplacements d'exécution sont stockés en tant que rangées `AppSetting` clés `feature.<FeatureFlag>` (valeur `true`/`false`).
Pas de rangée = "utiliser la ligne de base de config".

## Deux façons de désactiver une feature

### 1. Configuration de déploiement (ligne de base)

Définissez l'indicateur `false` sous `App:Features`. Exemple `appsettings.json` :

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

Ou via les variables env (double trait de soulignement) :

```
App__Features__CopyTrading=false
```

La ligne de base contrôle l'**enregistrement au démarrage** des workers d'arrière-plan (`Nodes.AddNodes`) et des outils MCP
(`serveur Mcp`), donc la feature désactivée dans la config ne démarre jamais ses services hébergés ni n'expose ses
outils MCP.

### 2. Remplacement d'exécution (propriétaire)

Le propriétaire peut basculer n'importe quelle feature en direct depuis **Paramètres → Features** (`/settings/features`) ou API :

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> définir le remplacement             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> effacer le remplacement (revenir)  (Owner)
```

Les changements d'exécution prennent effet immédiatement pour les portes de temps de requête (navigation, API). Les workers
d'arrière-plan et les outils MCP sont contrôlés au démarrage, recueillent le changement d'exécution au prochain redémarrage
du processus.

## Ce que chaque porte applique

| Couche | Mécanisme | Synchronisation |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` désactivé | Exécution |
| Navigation | `NavMenu` masque les liens via `IFeatureGate.IsEnabled` | Exécution |
| Workers d'arrière-plan | `AddHostedService` conditionnel dans `Nodes.AddNodes` | Démarrage (config) |
| Outils MCP | `WithTools<>` conditionnel dans le serveur MCP | Démarrage (config) |

Feature atteinte par lien profond tandis que désactivée rend une page vide — son API retourne `404` ;
la nav ne le surface plus.

## Indicateur → carte de surface

| Indicateur | Groupes API | Nav | Workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | groupe cBots → cBots (param sets par boîte de dialogue cBot) | MCP `CBotTools` |
| Backtesting | (partage `/api/instances`) | groupe cBots → Backtest | — |
| Execution | `/api/instances` | groupe cBots → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | groupe AI → AI ; Paramètres → AI (clé) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | groupe AI → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | groupe AI → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | groupe Prop → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | groupe Prop → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Paramètres → Open API | — |
| Mcp | `/api/mcp-keys` | groupe AI → MCP Keys | — |
| Compliance | `/api/compliance` | Paramètres → Legal & Privacy | — |

## Tests

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs` : ligne de base par défaut, mappage par indicateur.
- **Integration** — `IntegrationTests/FeatureGateTests.cs` : ligne de base de config, le remplacement d'exécution bat
  la config et persiste en tant que `AppSetting`, l'effacement revient à la ligne de base (Postgres réel).
- **E2E** — `E2ETests/FeatureToggleTests.cs` : désactiver `CopyTrading` lors de l'exécution masque son lien nav et
  `404`s `/api/copy`, le réactiver restaure les deux.
