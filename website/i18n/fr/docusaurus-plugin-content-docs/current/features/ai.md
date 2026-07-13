---
description: "cMind AI est agnostique des fournisseurs — Anthropic, OpenAI, Azure OpenAI, Google Gemini, et n'importe quel endpoint compatible OpenAI incluant les modèles locaux (Ollama, LM Studio, vLLM). Choisissez un fournisseur, un modèle, et un endpoint ; chaque fonctionnalité IA fonctionne inchangée."
---

# Fonctionnalités IA

La couche IA de cMind est **agnostique des fournisseurs**. Chaque fonctionnalité communique via une couche neutre unique en fournisseur (`IAiClient.CompleteAsync`) ; un **client de routage** résout les identifiants actifs du fournisseur et dispatche à l'adaptateur de fil correspondant. Vous choisissez un fournisseur + un modèle + un endpoint (et, si le fournisseur en a besoin, une clé) ; chaque fonctionnalité existante fonctionne inchangée avec le même contrôle, chiffrement, résilience, et dégradation.

**Batteries incluses :** un **LLM local intégré est livré avec l'app et est activé par défaut** (Microsoft.ML.OnnxRuntimeGenAI, p.ex. Phi-3-mini) — donc chaque déploiement a l'IA fonctionnelle **sans clé API et sans service externe**. Un déploiement white-label peut le retirer et restreindre quels fournisseurs les utilisateurs peuvent ajouter. Au-delà de l'intégré, connectez n'importe quel fournisseur externe.

Fournisseurs supportés :

- **IA locale intégrée** (`BuiltInOnnx`) — modèle GenAI ONNX en-process, sans clé, livré + activé par défaut.
- **Anthropic** (Claude — API Messages)
- **OpenAI** et **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **N'importe quel endpoint compatible OpenAI**, incluant **modèles locaux** (Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) et clouds compatibles OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek) — tous via l'adaptateur compatible OpenAI unique, différant uniquement par l'URL de base + modèle + clé.

Exactement **un** fournisseur est actif à la fois. Les identifiants sont stockés **chiffrés** (agrégat `AiProviderCredential` + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`) ; un endpoint local n'a **besoin d'aucune clé**. Avec **aucun** fournisseur actif, chaque fonctionnalité renvoie le résultat désactivé et le reste de l'app fonctionne inchangé (aucune clé nécessaire pour construire, tester, ou exécuter la plateforme).

**Rétro-compat :** la clé `App:Ai:ApiKey` héritée d'un déploiement existant (ou l'ancien paramètre `ai.api_key` chiffré) est honorée automatiquement comme fournisseur **Anthropic** actif par défaut — aucune action nécessaire.

IA non configurée → les pages IA estompent les actions et affichent une bannière plus une invite ponctuelle pour ajouter un fournisseur dans **Paramètres → IA** (`AiFeatureNotice`). Statut à `GET /api/ai/status` (`{ enabled, kind, model }`) ; fournisseurs gérés (propriétaire uniquement) via `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}`, et une vérification de connectivité `POST /api/ai/providers/test`.

## Défaut de déploiement vs fournisseur personnel d'un utilisateur

Les identifiants IA ont deux portées :

- **Défaut de déploiement (géré par le propriétaire).** Le propriétaire configure un fournisseur (ou en livre un via `App:Ai:Providers[]` / la clé héritée `App:Ai:ApiKey`). Il devient le **défaut partagé pour chaque utilisateur** — donc un courtier ou un fournisseur d'hébergement peut financer l'IA pour tous leurs utilisateurs sans **configuration par utilisateur et sans limite par utilisateur**. Géré via les routes propriétaire uniquement `/api/ai/providers` ci-dessus.
- **Fournisseur personnel d'un utilisateur (auto-service).** N'importe quel utilisateur connecté peut ajouter son propre fournisseur sous `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}`. Quand présent, leur **propre fournisseur actif remplace le défaut de déploiement pour leurs propres fonctionnalités IA** ; le retirer revient au défaut.

**Ordre de résolution** (dans `AiProviderStore`, par utilisateur de requête) : l'identifiant actif personnel de l'utilisateur → le défaut de déploiement → la clé de config héritée → aucun (IA désactivée). Exactement un identifiant est actif **par portée** (un index unique partiel par `OwnerUserId`), et chaque portée est résolue indépendamment, donc un utilisateur activant sa propre clé ne perturbe jamais le défaut partagé. Les contextes non-Web/de fond (aucun utilisateur de requête) résolvent toujours le défaut de déploiement.

## Matrice de capacité des fournisseurs

Les capacités par défaut par fournisseur et sont remplaçables par le propriétaire. Quand une capacité est désactivée, la fonctionnalité **se dégrade, ne lance jamais** : la recherche web est silencieusement abandonnée ; la vision renvoie un échec typé non supporté de capacité.

| Fournisseur | Type | URL de base par défaut | Clé requise | Recherche web | Vision | Notes |
|---|---|---|---|---|---|---|
| IA locale intégrée | `BuiltInOnnx` | n/a (en-process) | non | ✖ | ✖ | modèle ONNX GenAI livré, activé par défaut |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | oui | ✅ | ✅ | API Messages, outil `web_search` |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | oui | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | oui | ✅ | ✅ | chemin de déploiement + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | oui | ✅ | ✅ | `generateContent`, ancrage `google_search` |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | non | ✖ | dépendant du modèle | via adaptateur compatible OpenAI |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | non | dépendant du modèle | dépendant du modèle | via adaptateur compatible OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | votre URL servie | non | ✖ | dépendant du modèle | via adaptateur compatible OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL du fournisseur | oui | ✖ | dépendant du modèle | via adaptateur compatible OpenAI |

Les guides de configuration complets par fournisseur (clés, URLs, id de modèles, étapes UI) : voir [Fournisseurs IA — catalogue de configuration](../deployment/ai-providers.md).

## IA locale intégrée (livrée, activée par défaut)

cMind livre un **vrai LLM local qui s'exécute en-process** via [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (un modèle instruct compact comme Phi-3-mini). Il n'a **besoin d'aucune clé API et d'aucun service externe**, et au premier démarrage — quand aucun fournisseur n'est configuré et que la porte white-label l'autorise — il est **ensemencé et activé automatiquement**, donc chaque déploiement a l'IA fonctionnelle hors de la boîte.

- Le répertoire du modèle (`genai_config.json` + tokenizer + poids) est configuré par `App:Ai:BuiltIn:ModelPath` (défaut `models/onnx`, relatif au répertoire de base de l'app). Quand les fichiers du modèle sont absents, le fournisseur **se dégrade en échec typé avec une astuce d'installation** — il ne lance jamais, et le reste de l'app n'est pas affecté.
- Il alimente chaque fonctionnalité IA textuelle. Étant un modèle compact, il est texte-seulement (pas de recherche web côté serveur ou vision) et la génération est sérialisée (une instance de modèle, réutilisée après un chargement lazy).
- Acquérir/regrouper le modèle : voir [Fournisseurs IA → intégré](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Contrôles white-label

Un déploiement white-label restreint l'IA via `App:Branding` (appliqué côté serveur sur chaque upsert de fournisseur) :

- `AllowBuiltInAi` (défaut `true`) — défini `false` pour **retirer entièrement le modèle intégré**.
- `AllowLocalProviders` (défaut `true`) — défini `false` pour interdire les endpoints locaux/auto-hébergés (loopback / compatible OpenAI privé, p.ex. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (défaut vide = tous) — listez uniquement les types que le déploiement approuve (p.ex. `["Anthropic","OpenAiCompatible"]`) pour verrouiller quels fournisseurs les utilisateurs peuvent ajouter.

## Extension : modèles intégrés futurs

La couche IA est **basée sur les adaptateurs et construite pour grandir**. Chaque fournisseur est un `IAiProvider` sélectionné par `AiProviderKind` ; la couche orientée vers les fonctionnalités (`IAiClient`/`AiFeatureService`) ne change jamais. Ajouter un nouveau runtime de modèle intégré ultérieurement (un autre modèle ONNX, un moteur en-process différent, GGUF/llama.cpp en-proc, etc.) est un changement localisé : ajouter un `AiProviderKind`, implémenter un adaptateur `IAiProvider`, l'enregistrer, et (optionnellement) câbler l'ensemencement par défaut + une option de dialogue — aucun changement de fonctionnalité, endpoint, ou outil MCP. Le fournisseur ONNX intégré est l'implémentation de référence de ce modèle.

## Capacités

- **Construire cBot** — prompt en texte brut → cBot exécutable via boucle d'auto-réparation **générer → construire → correction IA** (`build-strategy`), à `/ai/build`.
- **Optimisation des paramètres** — boucle fermée : l'IA propose des ensembles de params, chacun persiste + backtesté entre les nœuds (`optimize-run` / `optimize-params`).
- **Agent de portefeuille autonome** — propositions pilotées par mandat avec journal de décision complet (`AgentMandate` → `AgentProposal`).
- **Garde de risque actif** — le service `AiRiskGuard` en arrière-plan évalue les bots en cours d'exécution, peut **auto-arrêter** sur risque critique (opt-in).
- **Gardien d'exposition prop-firm** — limites de drawdown/exposition avec auto-flatten.
- **Alertes du marché** — moteur `AlertRule` avec sentiment IA (grounded recherche web où le fournisseur la supporte).
- **Analyse** — revue de cBot, analyse de backtest, post-mortems, sentiment du marché, design vision de diagramme, curation de place de marché.

## Surfaces

- Endpoints Web sous `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …).
- Outils MCP (`AiTools`) pour clients IA — voir [mcp.md](mcp.md). La sélection du fournisseur est transparente aux clients MCP.
- Groupe de nav **IA** — une **page Blazor par fonctionnalité** : Construire cBot (`/ai/build`), Revue (`/ai/review`), Débat (`/ai/debate`), Sentiment du Marché (`/ai/sentiment`), Vérification d'Exposition (`/ai/exposure`), Résumé de Portefeuille (`/ai/digest`), Conseiller Tune (`/ai/tune`), Optimiser (`/ai/optimize`), plus Agent de Portefeuille, Alertes, Clés MCP. Les pages partagent `AiFeaturePageBase` + `AiOutputPanel` ; chacune affiche `AiFeatureNotice` quand aucun fournisseur n'est configuré.
- **Paramètres → IA** (`/settings/ai`, propriétaire uniquement) — liste des fournisseurs avec une **dialogue Ajouter / éditer fournisseur** (type, URL de base avec conseils par type incl. preset localhost Ollama/LM Studio, modèle, clé optionnelle, basculements de capacité, « définir actif ») et un bouton **Tester la connexion**.

## Configuration

`App:Ai` supporte à la fois la clé unique héritée et l'ensemencement multi-fournisseur :

- Hérité : `ApiKey`, `Model` (défaut `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — toujours honorés comme le fournisseur Anthropic par défaut.
- Multi-fournisseur : `ActiveProvider` (type) et `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — importés dans le magasin au démarrage si aucun identifiant n'existe encore, donc une équipe ops peut livrer un déploiement configuré (incl. LLM local) purement via appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` inchangés. Pour les tests/dev, une clé de config vit dans le [fichier de dev-credentials](../testing/dev-credentials.md) unifié sous `Ai`.

## Fiabilité

Le fournisseur est traité comme peu fiable — rien de ce qu'il fait ne peut faire descendre l'app. Cela tient identiquement pour les endpoints cloud et locaux (un Ollama mort réessaye puis se dégrade exactement comme un Anthropic limité) :

- **Dégradation gracieuse.** Chaque mode d'échec (pas de fournisseur, HTTP 4xx/5xx/429, timeout, corps malformé, contenu vide, capacité non supportée) renvoie un `AiResult.Fail(reason)` typé — le client ne lance jamais dans une page, un outil MCP, ou un service hébergé.
- **Pipeline de résilience.** `AddAiHttpClient` donne au `HttpClient` AI partagé unique une tentative bornée sur les pannes transitoires 5xx / réseau (backoff exponentiel + jitter) plus des timeouts généreux par tentative et totaux (`AiHttp`), réutilisés par chaque adaptateur.

## Test avec le faux LLM local

La couche IA est prouvée end-to-end **sans aucune dépendance externe** par `FakeLocalLlmServer` — un minuscule endpoint **compatible OpenAI** en-process renvoyant une réponse mise en cache déterministe, fil-identique à Ollama/LM Studio/vLLM. Il soutient :

- **Unit** — tests de traduction de requête par adaptateur + analyse de réponse, routage/dégradation de capacité.
- **Integration** — l'adaptateur compatible OpenAI end-to-end, la théorie de résilience paramétrée entre chaque adaptateur, et les **outils MCP IA**.
- **E2E** — le `AiLocalFixture` démarre l'app pointée sur le serveur faux (ou un fournisseur **réel** quand le développeur définit `AI_E2E_BASEURL` (+ optionnel `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) — les identifiants réels gagnent) et pilote chaque fonctionnalité IA par l'UI réelle. Ajouter ou changer n'importe quelle fonctionnalité IA **requiert** un test E2E via ce fixture (voir le mandat de test du repo). Une voie opt-in (`AI_LOCAL_LLM=1`) exécute une vraie complétion via un Testcontainer **Ollama**.
