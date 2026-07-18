---
description: "L'IA de cMind est agnostique au fournisseur — Anthropic, OpenAI, Azure OpenAI, Google Gemini et tout point de terminaison compatible OpenAI, y compris les modèles locaux (Ollama, LM Studio, vLLM). Choisissez un fournisseur, un modèle et un point de terminaison ; chaque fonctionnalité IA fonctionne sans modification."
---

# Fonctionnalités IA

La couche IA de cMind est **agnostique au fournisseur**. Chaque fonctionnalité communique avec une seule interface
neutre (`IAiClient.CompleteAsync`) ; un **client de routage** résout le credential du fournisseur actif et dispatch
vers l'adaptateur fil correspondant. Vous choisissez un fournisseur + modèle + point de terminaison (et, si le fournisseur
le nécessite, une clé) ; chaque fonctionnalité existante fonctionne sans modification avec les mêmes contrôles, chiffrement,
résilience et dégradation.

**Batteries incluses :** un **LLM local intégré ships with the app et est activé par défaut**
(Microsoft.ML.OnnxRuntimeGenAI, p. ex. Phi-3-mini) — ainsi chaque déploiement dispose d'une IA fonctionnelle **sans
clé API ni service externe**. Un déploiement white-label peut le supprimer et restreindre les fournisseurs que les utilisateurs
peuvent ajouter. Au-delà de l'intégré, connectez n'importe quel fournisseur externe.

Fournisseurs supportés :

- **IA locale intégrée** (`BuiltInOnnx`) — modèle GenAI ONNX in-process, pas de clé, livré + activé par défaut.
- **Anthropic** (Claude — Messages API)
- **OpenAI** et **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Tout point de terminaison compatible OpenAI**, y compris **les modèles locaux** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) et les clouds compatibles OpenAI (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — tous via l'adaptateur compatible OpenAI, différant uniquement par URL de base + modèle + clé.

Exactement **un** fournisseur est actif à un instant donné. Les credentials sont stockés **chiffrés**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`) ;
un point de terminaison local n'a **pas besoin de clé**. Avec **aucun** fournisseur actif, chaque fonctionnalité
retourne le résultat désactivé et le reste de l'application fonctionne sans modification (pas de clé nécessaire pour
compiler, tester ou exécuter la plateforme).

**Rétro-compat :** le `App:Ai:ApiKey` existant d'un déploiement (ou l'ancien paramètre chiffré `ai.api_key`)
est honoré automatiquement comme fournisseur **Anthropic** actif par défaut — aucune action requise.

IA non configurée → les pages IA atténuent les actions et affichent une bannière plus une invite unique pour ajouter
un fournisseur dans **Settings → AI** (`AiFeatureNotice`). État sur `GET /api/ai/status` (`{ enabled, kind, model }`) ;
fournisseurs gérés (propriétaire uniquement) via `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, et un `POST /api/ai/providers/test` ping de connectivité.

## Déploiement par défaut vs propre fournisseur d'un utilisateur

Les credentials IA ont deux scopes :

- **Valeur par défaut du déploiement (gérée par le propriétaire).** Le propriétaire configure un fournisseur
  (ou en livre un via `App:Ai:Providers[]` / le legacy `App:Ai:ApiKey`). Il devient **la valeur par défaut
  partagée pour chaque utilisateur** — ainsi un broker ou hébergeur peut financer l'IA pour tous ses utilisateurs
  **sans configuration par utilisateur et sans limite par utilisateur**. Géré via les routes `/api/ai/providers` susmentionnées,
  propriétaires uniquement.
- **Propre fournisseur d'un utilisateur (en libre-service).** Tout utilisateur connecté peut ajouter son propre
  fournisseur sous `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Quand présent, **son fournisseur actif propre écrase la valeur par défaut du déploiement
  pour ses propres fonctionnalités IA** ; le supprimer revient à la valeur par défaut.

**Ordre de résolution** (dans `AiProviderStore`, par requête utilisateur) : le credential actif propre de l'utilisateur →
la valeur par défaut du déploiement → la clé de config legacy → aucun (IA désactivée). Un credential est actif
**par scope** (index unique partiel par `OwnerUserId`), et chaque scope est résolu indépendamment, donc un utilisateur
activant sa propre clé ne perturbe jamais la valeur par défaut partagée. Les contextes background/non-Web (pas d'utilisateur
dans la requête) résolvent toujours la valeur par défaut du déploiement.

## Matrice de capacités des fournisseurs

Les capacités sont par défaut par fournisseur et sont écrasables par le propriétaire. Quand une capacité est désactivée,
la fonctionnalité **se dégrade, ne lève jamais d'exception** : la recherche web est silencieusement supprimée ;
la vision retourne un échec typé de capacité non supportée.

| Fournisseur | Kind | URL de base par défaut | Clé requise | Recherche web | Vision | Notes |
|---|---|---|---|---|---|---|
| IA locale intégrée | `BuiltInOnnx` | n/a (in-process) | non | ✖ | ✖ | modèle GenAI ONNX livré, activé par défaut |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | oui | ✅ | ✅ | Messages API, outil `web_search` |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | oui | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | oui | ✅ | ✅ | chemin du déploiement + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | oui | ✅ | ✅ | `generateContent`, grounding `google_search` |
| Ollama (local) | `OpenAiCompatible` | `http://localhost:11434/v1/` | non | ✖ | dépend du modèle | via adaptateur compatible OpenAI |
| LM Studio (local) | `OpenAiCompatible` | `http://localhost:1234/v1/` | non | ✖ | dépend du modèle | via adaptateur compatible OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | votre URL servie | non | ✖ | dépend du modèle | via adaptateur compatible OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL du fournisseur | oui | ✖ | dépend du modèle | via adaptateur compatible OpenAI |

Guides de configuration par fournisseur (clés, URLs, IDs de modèle, étapes UI) : voir
[AI providers — catalogue de configuration](../deployment/ai-providers.md).

## IA locale intégrée (livrée, activée par défaut)

cMind livre un **vrai LLM local qui s'exécute in-process** via
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (un modèle instruct compact tel que
Phi-3.5-mini). Il n'a **pas besoin de clé API ni de service externe**, et au premier démarrage — quand aucun fournisseur
n'est configuré et que le gate white-label le permet — il est **ensemencé et activé automatiquement**, ainsi chaque
déploiement dispose d'une IA fonctionnelle dès le départ.

- Le répertoire du modèle (`genai_config.json` + tokenizer + poids) est configuré par
  `App:Ai:BuiltIn:ModelPath` (par défaut `models/onnx`, relatif au répertoire de base de l'application). Quand les
  fichiers du modèle sont absents, le fournisseur **se dégrade vers un échec typé avec une indication d'installation** —
  il ne lève jamais d'exception, et le reste de l'application n'est pas affecté.
- Il alimente chaque fonctionnalité IA textuelle. Étant un modèle compact, il est text-only (pas de recherche web
  ni de vision côté serveur) et la génération est sérialisée (une instance du modèle, réutilisée après un lazy load).
- **Plusieurs modèles intégrés peuvent coexister.** Chaque modèle téléchargé vit sous `ModelPath/<key>` ; un catalogue curé (Phi-3.5-mini par défaut, plus Phi-3-mini-128k) peut être téléchargé et basculé depuis **Settings → AI**. Sélectionner un sous-modèle intégré le charge in-process. Acquérir/regrouper un modèle : voir [AI providers → intégré](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Contrôles white-label

Un déploiement white-label restreint l'IA via `App:Branding` (appliqué côté serveur à chaque upsert de fournisseur) :

- `AllowBuiltInAi` (par défaut `true`) — mettre `false` pour **supprimer entièrement le modèle intégré**.
- `AllowLocalProviders` (par défaut `true`) — mettre `false` pour interdire les points de terminaison locaux/self-hosted
  (loopback / compatible OpenAI privé, p. ex. Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (par défaut vide = tous) — lister uniquement les kinds que le déploiement autorise (p. ex.
  `["Anthropic","OpenAiCompatible"]`) pour verrouiller les fournisseurs que les utilisateurs peuvent ajouter.
- `AllowAiTasks` (par défaut `true`) — mettre `false` pour supprimer la fonctionnalité de **tâche IA en arrière-plan** (la
  page `/ai/tasks` et l'API des tâches retournent 404 ; le worker cesse de réclamer) ; les fonctionnalités IA synchrone fonctionnent toujours.
- `AllowAiModelManagement` (par défaut `true`) — mettre `false` pour masquer **l'exploration de modèles** et la **liaison par fonctionnalité
  de modèle**. Les deux sont réglables par le propriétaire à l'exécution depuis **Settings → Deployment** (superposés en direct sur
  `IOptionsMonitor`) et catalogués dans `WhiteLabelCatalog`.

## Extension : futurs modèles intégrés

La couche IA est **adaptateur-based et conçue pour grandir**. Chaque fournisseur est un `IAiProvider` sélectionné par
`AiProviderKind` ; l'interface côté fonctionnalité (`IAiClient`/`AiFeatureService`) ne change jamais. Ajouter un nouveau
runtime de modèle intégré plus tard (un autre modèle ONNX, un autre moteur in-process, GGUF/llama.cpp
in-proc, etc.) est un changement localisé : ajouter un `AiProviderKind`, implémenter un adaptateur `IAiProvider`,
l'enregistrer, et (optionnellement) câbler l'ensemencement par défaut + une option de dialogue — aucun changement de
fonctionnalité, endpoint ou outil MCP. Le fournisseur ONNX intégré est l'implémentation de référence de ce pattern.

## Capacités

- **Build cBot** — prompt en anglais plain → cBot exécutable via **génération → build → boucle d'auto-réparation AI-fix** (`build-strategy`), sur `/ai/build`. Le **code source généré est affiché** quand le build se termine (avec un bouton copier), à côté du journal de build — en succès *et* en échec — vous voyez toujours ce que l'IA a écrit, pas seulement les erreurs.
- **Tâches IA en arrière-plan** — démarrez un travail IA de longue durée (p. ex. construire un cBot) avec les modèles de votre choix, puis quittez la page et revenez au résultat. Choisissez plusieurs modèles pour comparer — chacun s'exécute en tant que sa propre tâche (`/ai/tasks`). Un worker de l'hôte web réclame les tâches sur un bail auto-cicatrisant (réclamé si un nœud meurt) et diffuse la progression dans un journal d'activité par tâche.
- **Parcourir et sélectionner les modèles, par fonctionnalité** — parcourez les modèles qu'un point de terminaison de fournisseur annonce (`GET /v1/models` sur LM Studio / Ollama / vLLM / llama.cpp, ou le catalogue intégré) au lieu de taper à la main une id, et **liez chaque fonctionnalité IA à un modèle différent** afin que plusieurs modèles servent différentes fonctionnalités à la fois (une fonctionnalité non liée revient au fournisseur actif du scope).
- **Optimisation des paramètres** — boucle fermée : l'IA propose des ensembles de paramètres, chacun persisté + backtesté
  sur les nœuds (`optimize-run` / `optimize-params`).
- **Agent de portfolio autonome** — propositions pilotées par un mandat avec journal de décision complet
  (`AgentMandate` → `AgentProposal`).
- **Garde-barre risque actif** — service background `AiRiskGuard` évalue les bots en cours, peut **arrêter automatiquement**
  en cas de risque critique (opt-in).
- **Garde-barre exposition prop-firm** — limites de drawdown/exposition avec flatten automatique.
- **Alertes de marché** — moteur `AlertRule` avec sentiment IA (recherche web grounding si le fournisseur le supporte).
- **Analyse** — revue de cBot, analyse de backtest, post-mortems, sentiment de marché, conception de vision de graphique,
  curation du marketplace.

## Surfaces

- Endpoints web sous `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …), plus **background tasks** (`/api/ai/tasks` create/list/detail/cancel/delete), **model discovery** (`/api/ai/models/probe`, `/api/ai/usable-models`) and **per-feature bindings** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- Outils MCP (`AiTools`) pour les clients IA — voir [mcp.md](mcp.md). La sélection du fournisseur est
  transparente pour les clients MCP.
- **Navigation IA** — une Blazor **page par fonctionnalité** : Build cBot (`/ai/build`), Review (`/ai/review`),
  Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest
  (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), **AI Tasks** (`/ai/tasks`), plus Portfolio Agent, Alerts, MCP Keys.
  Les pages partagent `AiFeaturePageBase` + `AiOutputPanel` ; chacune affiche `AiFeatureNotice` quand aucun
  fournisseur n'est configuré.
- **Settings → AI** (`/settings/ai`, propriétaire uniquement) — liste des fournisseurs avec un **dialogue Ajouter/éditer
  fournisseur** (kind, URL de base avec indications par kind incluant un preset Ollama/LM Studio localhost,
  modèle, clé optionnelle, bascules de capacité, « définir comme actif ») et un bouton **Tester la connexion**.

## Configuration

`App:Ai` supporte à la fois la clé unique legacy et l'ensemencement multi-fournisseur :

- Legacy : `ApiKey`, `Model` (par défaut `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — toujours honorés comme
  fournisseur Anthropic par défaut.
- Multi-fournisseur : `ActiveProvider` (kind) et `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — importés dans le store au démarrage si aucun credential n'existe encore, ainsi
  une équipe ops peut livrer un déploiement configuré (incl. LLM-local) purement via appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` inchangés. Pour les tests/dev, une clé de config
se trouve dans le [fichier de credentials dev unifié](../testing/dev-credentials.md) sous `Ai`.

## Fiabilité

Le fournisseur est traité comme non fiable — rien de ce qu'il fait ne peut faire tomber l'application. Cela
s'applique identiquement aux endpoints cloud et locaux (un Ollama mort relance puis se dégrade exactement comme un
Anthropic limité) :

- **Dégradation gracieuse.** Chaque mode d'échec (pas de fournisseur, HTTP 4xx/5xx/429, timeout, corps malformé,
  contenu vide, capacité non supportée) retourne un `AiResult.Fail(reason)` typé — le client ne lève jamais
  d'exception dans une page, un outil MCP ou un service hébergé.
- **Pipeline de résilience.** `AddAiHttpClient` donne au `HttpClient` IA partagé un retry borné sur les échecs
  transitoires 5xx / réseau (backoff exponentiel + jitter) plus des timeouts généreux par tentative et total
  (`AiHttp`), réutilisé par chaque adaptateur.

## Test avec le faux LLM local

La couche IA est prouvée bout-en-bout **sans aucune dépendance externe** par `FakeLocalLlmServer` — un tiny
endpoint **compatible OpenAI** in-process retournant une réponse canned déterministe, fil-identique à
Ollama/LM Studio/vLLM. Il supporte :

- **Unit** — tests de traduction de requête + parse de réponse par adaptateur, routage/dégradation de capacité.
- **Integration** — l'adaptateur compatible OpenAI bout-en-bout, la théorie de résilience paramétrée sur chaque
  adaptateur, et les **outils IA MCP**.
- **E2E** — `AiLocalFixture` boot l'application pointée sur le faux serveur (ou un **vrai** fournisseur quand
  le développeur définit `AI_E2E_BASEURL` (+ optionnel `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  les vrais credentials l'emportent) et pilote chaque fonctionnalité IA à travers la vraie UI. Ajouter ou changer
  une fonctionnalité IA **requiert** un test E2E à travers cette fixture (voir le mandat de test du repo). Une voie
  opt-in (`AI_LOCAL_LLM=1`) exécute une vraie complétion à travers un **Ollama** Testcontainer.

## IA locale intégrée — zéro configuration par défaut

Le LLM local ONNX intégré fonctionne out-of-the-box : quand son répertoire de modèle est absent et
`App:Ai:BuiltIn:AutoDownload` est `true` (par défaut), l'application télécharge le modèle une fois en
arrière-plan depuis `App:Ai:BuiltIn:DownloadBaseUrl`. Pendant le téléchargement, les appels IA (et **Tester
la connexion** dans Settings → AI) retournent un message clair « le modèle est en téléchargement (configuration
premier usage) » plutôt qu'un échec dur. Les déploiements air-gapped/métrés définissent `AutoDownload=false` et
pré-approvisionnent le répertoire du modèle (`App:Ai:BuiltIn:ModelPath`). Le gate white-label
`App:Branding:AllowBuiltInAi` s'applique toujours.

Le téléchargement est aussi **pré-réchauffé au démarrage** quand le modèle intégré est le fournisseur actif, donc il est prêt avant le premier clic IA plutôt que d'échouer ce clic avec « téléchargement en cours... ». **Settings → AI** affiche l'état d'installation en direct sur la carte du fournisseur intégré — *Modèle prêt* / *Téléchargement du modèle...* / *Modèle non installé* / *Téléchargement échoué* — avec un bouton **Télécharger le modèle** (ou **Réessayer le téléchargement**) qui lance la récupération en arrière-plan à la demande (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Activer le fournisseur intégré à partir des Settings réutilise la ligne déjà ensemencée au lieu d'ajouter une copie, donc ne rentre jamais en conflit sur la contrainte de fournisseur actif unique.
