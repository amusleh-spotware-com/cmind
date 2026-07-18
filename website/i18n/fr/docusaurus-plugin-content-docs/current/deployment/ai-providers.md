---
description: "Catalogue de configuration pour chaque fournisseur IA supporté par cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini, et tous les points de terminaison compatibles avec OpenAI, y compris les modèles locaux (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) et les clouds compatibles."
---

# Fournisseurs IA — catalogue de configuration

La couche IA de cMind est agnostique vis-à-vis du fournisseur (voir [Fonctionnalités IA](../features/ai.md)). Configurez un fournisseur de deux façons :

1. **Interface utilisateur (propriétaire) :** Paramètres → IA → **Ajouter un fournisseur** → choisir type, URL de base, modèle, clé (facultatif pour local), basculer les capacités, **Définir actif** → **Tester la connexion**.
2. **Config/env (ops) :** alimenter `App:Ai:Providers[]` et `App:Ai:ActiveProvider` — importés dans le magasin au premier démarrage quand aucune information d'identification n'existe. Exemple (env, index de fournisseur `0`) :

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omettre pour les points de terminaison locaux sans clé)
   ```

Exactement un fournisseur est actif à la fois. Les clés sont stockées chiffrées ; un point de terminaison local n'en a pas besoin.

## Sécurité : http vs https

Le texte clair `http://` est accepté **uniquement** pour les hôtes de loopback / privés (intranet) — le cas du LLM local (Ollama, LM Studio, vLLM, une boîte sur site). Tout hôte routable sur l'Internet public **doit** être `https://`, donc une clé API n'est jamais envoyée en clair. Air-gappé/sur site : pointez l'URL de base sur votre point de terminaison interne (loopback ou IP privée) et laissez la clé vide si le runtime n'est pas authentifié.

## IA locale intégrée (ONNX, livré)

cMind propose un **vrai LLM local intégré** (Microsoft.ML.OnnxRuntimeGenAI) qui est **activé par défaut** — pas de clé, pas de service externe. Au premier démarrage, quand aucun fournisseur n'est configuré et `App:Branding:AllowBuiltInAi` est `true`, il est alimenté et activé automatiquement.

- **Config :** `App:Ai:BuiltIn:Enabled` (défaut `true`), `App:Ai:BuiltIn:ModelPath` (défaut `models/onnx`, relatif au répertoire de base de l'application), `App:Ai:BuiltIn:MaxTokens` (défaut `1024`).
- **Fichiers modèles :** pointez `ModelPath` sur un répertoire contenant un modèle ONNX GenAI — `genai_config.json`, le tokeniseur et les poids `.onnx`. Un build CPU **Phi-3-mini** fonctionne bien, par exemple :

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-128k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # puis définir App:Ai:BuiltIn:ModelPath sur ce dossier (contient genai_config.json)
  ```

  Regroupez le dossier avec votre image de déploiement / volume Helm, ou montez-le à l'exécution. Quand les fichiers sont absents, le built-in se dégrade en un message clair "modèle non installé" — l'application continue à fonctionner ; configurez un autre fournisseur ou installez le modèle.
- **GPU :** permutez le package/modèle CPU pour un build ONNX GenAI CUDA/DirectML ; le chemin du code reste inchangé.

## White-label : limiter l'IA

Défini sous `App:Branding` (appliqué côté serveur — une insertion refusée retourne `400`) :

- `AllowBuiltInAi: false` — supprimez complètement le modèle built-in livré.
- `AllowLocalProviders: false` — interdire les points de terminaison locaux/auto-hébergés (Ollama/LM Studio/vLLM et toute URL loopback/privée compatible OpenAI).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — autoriser uniquement ces types (vide = tous).

## Extension avec des modèles built-in futurs

La couche de fournisseur est basée sur des adaptateurs (`IAiProvider` classé par `AiProviderKind`), donc un futur runtime de modèle built-in est ajouté sans toucher à aucune fonctionnalité IA : ajouter un type, implémenter un adaptateur, l'enregistrer. Le built-in ONNX est l'implémentation de référence. Voir [Fonctionnalités IA → Extension](../features/ai.md#extending-future-built-in-models).

## Fournisseurs cloud

### Anthropic (Claude)

- Clé : <https://console.anthropic.com/> → Clés API.
- URL de base : `https://api.anthropic.com/` · Modèle : p. ex. `claude-opus-4-8`.
- Capacités : recherche Web + vision activées par défaut.

### OpenAI

- Clé : <https://platform.openai.com/api-keys>.
- URL de base : `https://api.openai.com/v1/` · Modèle : p. ex. `gpt-4o`.
- Type : **OpenAiCompatible**. Activez la vision dans la boîte de dialogue si vous utilisez un modèle de vision.

### Azure OpenAI

- Clé + point de terminaison : Portail Azure → votre ressource Azure OpenAI.
- URL de base : `https://<resource>.openai.azure.com/` · Modèle : votre **nom de déploiement**.
- Type : **AzureOpenAi** (utilise l'en-tête `api-key` + la requête `api-version` et le chemin de déploiement).

### Google Gemini

- Clé : <https://aistudio.google.com/app/apikey>.
- URL de base : `https://generativelanguage.googleapis.com/` · Modèle : p. ex. `gemini-2.0-flash`.
- Type : **Gemini**. Ancrage de recherche Web + vision activée par défaut.

### Autres clouds compatibles avec OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Type : **OpenAiCompatible**. URL de base = point de terminaison compatible OpenAI du fournisseur, Modèle = son identifiant de modèle, CléApi = clé du fournisseur. Aucun changement cMind nécessaire — un adaptateur les dessert tous.

## Modèles locaux (pas de clé)

Tous les runtimes locaux exposent le fil OpenAI Chat Completions, donc utilisez **Type : OpenAiCompatible** avec l'URL de base du runtime et le nom du modèle servi ; laissez la clé vide.

### Ollama

```
# installer depuis https://ollama.com, puis :
ollama pull llama3.1:8b
```

- URL de base : `http://localhost:11434/v1/` · Modèle : le nom tiré (p. ex. `llama3.1:8b`, `qwen2.5-coder`).
- Pas de clé API. Les capacités par défaut sont text-only ; n'activez la vision que pour un modèle de vision.

### LM Studio

- Démarrez le serveur local (Développeur → Démarrer le serveur).
- URL de base : `http://localhost:1234/v1/` · Modèle : l'identifiant du modèle chargé. Pas de clé API.

### vLLM / llama.cpp `server` / LocalAI

- Servir un point de terminaison compatible OpenAI (chacun en expédie un).
- URL de base : votre URL servie (p. ex. `http://localhost:8000/v1/`) · Modèle : nom du modèle servi. Pas de clé sauf si vous mettiez une auth devant.

## Vérification

- **Tester la connexion** dans la boîte de dialogue exécute une petite complétion de ping et signale le succès + la latence — idéal pour confirmer un point de terminaison local.
- Automatisé : la suite E2E de l'application pilote chaque fonctionnalité IA par défaut contre un serveur OpenAI-compatible faux intégré, ou votre vrai fournisseur quand `AI_E2E_BASEURL` (+ optionnel `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) est défini. Voir [Fonctionnalités IA → Test](../features/ai.md#testing-with-the-fake-local-llm).

## Basculement / Rotation

- **Basculer le fournisseur actif :** Paramètres → IA → **Définir actif** sur une autre carte (l'activation d'une en désactive les autres).
- **Faire pivoter une clé :** modifier le fournisseur et fournir une nouvelle clé (laisser vide pour garder celle stockée).
- **Supprimer :** supprimer la carte. Sans fournisseur actif, les fonctionnalités IA se désactiven et le reste de l'application fonctionne sans changement.
