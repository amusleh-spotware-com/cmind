---
description: "deploy/azure/main.bicep provisionne une couche apatride sur Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# Déploiement Azure — étape par étape

`deploy/azure/main.bicep` provisionne une couche apatride sur **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Prérequis

- Azure CLI (`az login` effectué), abonnement, permission pour créer des groupes de ressources.
- Trois images poussées vers le registre qu'Azure peut récupérer (par ex. GHCR public, ou ACR).

## 2. Créer un groupe de ressources

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Déployer le Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Crée : environnement Container Apps, Web (ingress externe), MCP (ingress externe), Postgres Flexible Server + `appdb`, Log Analytics, composant **Application Insights basé sur l'espace de travail**. Discovery activé pour Web. Sa chaîne de connexion est injectée dans Web + MCP en tant que `APPLICATIONINSIGHTS_CONNECTION_STRING`, donc les traces + métriques s'exportent nativement vers App Insights tandis que les logs arrivent dans le même espace de travail Log Analytics — pas de collecteur nécessaire. Passez `-p otlpEndpoint=...` pour *aussi* transférer vers le collecteur OTLP.

## 4. Obtenir les URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Ouvrez `webUrl`, connectez-vous avec le propriétaire (changement de mot de passe forcé à la première connexion).

## 5. Ajouter des agents nœud (séparé)

Container Apps ne peut pas exécuter privilégié/DinD, donc exécutez les agents ailleurs, pointant vers `webUrl` :

- **AKS** — déployez le graphique Helm ([kubernetes.md](kubernetes.md)) avec `nodeAgent.privileged=true`, augmentez Web/MCP à 0 si vous voulez seulement la couche agent là-bas.
- **VM / VMSS** — exécutez l'image `cmind-node-agent` `--privileged` avec `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Les agents s'auto-enregistrent dans un intervalle de pulsation — voir [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Vérifier

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # logs JSON compact
curl -s <webUrl>/version
```

## Notes de production

- Mettez en façade Web avec Azure Front Door / App Gateway pour TLS + WAF.
- Stockez les secrets dans Key Vault ; passez un certificat Data Protection stable (`App__DataProtectionCertBase64` / `...Password`) pour que l'anneau de clés survive aux redémarrages de réplicas.
- App Insights (traces+métriques) + Log Analytics (logs) câblés automatiquement ; corrélez sur `trace_id`. Voir [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Définissez le paramètre `otlpEndpoint` (ou `OTEL_EXPORTER_OTLP_ENDPOINT` sur les apps) pour *aussi* transférer vers le collecteur.
- Les règles d'échelle Container Apps (min/max) sont câblées dans Bicep.

## Agent copy-trading + Key Vault (S5)

`deploy/azure/main.bicep` provisionne aussi un **copy-agent** Container App hébergeant `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) sans **ingress** — worker tenant des sockets cTrader longue durée. Lit la chaîne de connexion à la base de données depuis un secret **Azure Key Vault** via une **identité gérée assignée par l'utilisateur** (rôle Secrets User de Key Vault) plutôt qu'un secret en texte brut en ligne. Le `NodeName` de chaque réplica est par défaut son nom d'hôte de conteneur (unique), donc les attributs de bail DB exécutent les profils par réplica et deux réplicas n'hébergent jamais deux fois le même. Augmentez `minReplicas`/`maxReplicas` pour ajouter de la capacité de copie ; l'anneau de clés DataProtection est partagé via Postgres, donc n'importe quel réplica peut décrypter les tokens Open API stockés. Sorties : `copyAgentName`, `keyVaultName`.
