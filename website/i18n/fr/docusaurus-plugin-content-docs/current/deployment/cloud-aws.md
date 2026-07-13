---
description: "deploy/aws = Module Terraform : ECS Fargate (Web + MCP) derrière ALB, RDS Postgres, logs CloudWatch."
---

# Déploiement AWS — étape par étape

`deploy/aws` = Module Terraform : **ECS Fargate** (Web + MCP) derrière **ALB**, **RDS Postgres**, logs CloudWatch.

## 1. Prérequis

- Terraform ≥ 1.5 + identifiants AWS (`aws configure` / variables env) avec droits pour créer des
  ressources VPC, ECS, RDS, ALB, IAM.
- Trois images dans le registre qu'ECS peut récupérer (ECR, ou GHCR public).

## 2. Initialiser

```bash
cd deploy/aws
terraform init
```

## 3. Appliquer

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Crée : RDS Postgres (`appdb`), cluster ECS, services Fargate pour Web + MCP, ALB (Web à `/`,
MCP à `/mcp`), groupes de sécurité, groupe de logs CloudWatch, **collecteur ADOT (AWS Distro for
OpenTelemetry) sidecar** dans chaque tâche. L'app exporte OTLP vers le sidecar, qui envoie
les traces à **X-Ray**, les métriques à **CloudWatch** (EMF, espace de noms `cmind`) ; les logs restent sur
le driver `awslogs` au format JSON compact. Discovery activé pour Web. Le rôle de tâche accorde au sidecar
l'accès en écriture à X-Ray + CloudWatch — pas de collecteur à exécuter soi-même.

> Utilise le **VPC/subnets par défaut** du compte pour la brièveté. En production, configurez
> votre propre VPC, subnets privés, écouteur HTTPS (certificat ACM).

## 4. Obtenir les URLs

```bash
terraform output web_url   # racine ALB
terraform output mcp_url   # ALB /mcp
```

Ouvrez `web_url`, connectez-vous avec le propriétaire (changement de mot de passe forcé à la première connexion).

## 5. Ajouter des agents nœud (séparé)

Fargate n'autorise pas privilégié/DinD, donc exécutez les agents ailleurs pointant vers `web_url` :

- **ECS sur EC2** — fournisseur de capacité avec les définitions de tâche `privileged = true` exécutant
  `cmind-node-agent`.
- **EKS** — graphique Helm ([kubernetes.md](kubernetes.md)) avec `nodeAgent.privileged=true`.

Définissez `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Les agents s'auto-enregistrent — voir
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Vérifier

```bash
aws logs tail /ecs/cmind --since 5m         # logs JSON compact
curl -s "$(terraform output -raw web_url)/version"
```

## Notes de production

- Ajoutez un écouteur HTTPS + certificat ACM ; limitez le groupe de sécurité ALB.
- Stockez les secrets dans AWS Secrets Manager / SSM, injectez-les via les `secrets` de la
  définition de tâche au lieu de `environment` en texte brut.
- Activez RDS Multi-AZ + sauvegardes.
- Traces (X-Ray), métriques (CloudWatch EMF), logs (CloudWatch Logs) câblés automatiquement via
  le sidecar ADOT ; corrélez sur `trace_id`. Voir
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- L'app pointe déjà `OTEL_EXPORTER_OTLP_ENDPOINT` vers le sidecar en tâche ; reconfigurez vers
  le collecteur externe si vous préférez centraliser.

## Agent copy-trading + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` ajoute un service ECS Fargate **copy-agent** hébergeant `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) sans **ALB** — worker tenant des sockets
cTrader longue durée. La chaîne de connexion à la base de données est stockée dans **AWS Secrets Manager**,
injectée via le bloc `secrets` de la tâche (rôle d'exécution accordé `secretsmanager:GetSecretValue` sur
ce seul secret), pas d'env en texte brut. Le `NodeName` de chaque tâche est par défaut le nom d'hôte de son
conteneur (unique par tâche Fargate), donc les attributs de bail DB exécutent les profils par tâche —
deux tâches n'hébergent jamais deux fois le même. Augmentez `copy_agent_count` pour ajouter de la
capacité de copie ; l'anneau de clés DataProtection est partagé via Postgres, donc n'importe quelle tâche
peut décrypter les tokens Open API stockés.
