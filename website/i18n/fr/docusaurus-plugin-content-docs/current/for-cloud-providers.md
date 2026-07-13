---
slug: /for-cloud-providers
title: cMind pour les fournisseurs cloud & VPS
description: Pourquoi un fournisseur cloud ou VPS devrait proposer un hébergement cMind géré — un produit clé en main et différencié pour les traders algo, les courtiers et les prop firms, avec des moyens clairs de monétiser le compute, la revente white-label et l'IA gérée.
keywords:
  - Hébergement géré
  - Fournisseur VPS
  - Fournisseur cloud
  - Hébergement de plateforme de trading
  - Revendeur white-label
  - Hébergement IA géré
sidebar_position: 7
---

# cMind pour les fournisseurs cloud & VPS 🖥️

Vous louez déjà du compute. cMind est un produit open source clé en main que vous pouvez envelopper autour de ce compute : **proposez un hébergement cMind géré** et remportez un workload de valeur élevée, collant et gourmand en compute — les traders algorithmiques, les courtiers, les prop firms, et les communautés de trading qui veulent la plateforme exécutée sans devenir l'équipe ops eux-mêmes.

:::tip TL;DR
Exécutez le tier sans état + Postgres + une flotte de nœuds ; donnez aux clients une URL marquée. Monétisez l'abonnement, le compute, le white-label, et l'IA. → [Déployez sur le cloud](./deployment/cloud.md)
:::

## Pourquoi proposer un cMind géré

- **Pas de coût de construction.** C'est open source, sous licence MIT, et déjà documenté, testé, et conteneurisé. Vous l'emballez et l'exploitez — vous ne le construisez pas.
- **Un produit différencié pour une niche lucrative.** Le trading algo consomme du compute : les backtests et les nœuds en direct consomment du CPU, ce qui est une *utilisation facturable* que vous vendez déjà.
- **Clients collants.** Les traders qui construisent et exécutent des stratégies dans la plateforme ne quittent pas facilement.
- **Transforme une caveat en upsell.** cMind est auto-hébergé par conception — pour les clients qui « ne veulent pas être l'équipe ops », *vous* êtes la réponse.

## Qui achète cMind géré de vous

- **Les quants individuels & traders** qui veulent qu'il soit hébergé. → [Pour les traders](./for-traders.md)
- **Les courtiers cTrader** exécutant un white-label pour leurs clients. → [Pour les courtiers](./for-brokers.md)
- **Les prop firms & entreprises de copy-trading** qui ont besoin d'infrastructure marquée et auditable.

## Que « cMind géré » signifie d'exécuter

Vous exploitez trois tiers ; le client obtient une URL web marquée :

| Tier | Ce que c'est | Où cela s'exécute |
|---|---|---|
| Sans état (Web + MCP) | L'app + API + serveur MCP | N'importe quelle plateforme de conteneur, auto-escaladée |
| Base de données | PostgreSQL | Postgres géré (RDS / Flexible Server / votre propre) |
| Flotte de nœuds | Construit & exécute les conteneurs cTrader | **VMs ou Kubernetes — a besoin de Docker privilégié** |

:::warning Une chose à planifier à l'avance
Les agents de nœud construisent et exécutent les conteneurs cTrader, donc ils ont besoin de **Docker privilégié**. Cela exclut les runtimes de conteneurs sans serveur (Azure Container Apps, AWS Fargate) *pour les agents* — exécutez-les sur [Kubernetes](./deployment/kubernetes.md), une VM, ou EC2. Le tier sans état s'exécute partout.
:::

Les vrais guides de déploiement copy-paste rendent cela concret : [aperçu du cloud](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## Comment vous le monétisez

- **Abonnement à l'hébergement géré.** Plans Starter / Team / Business mensuels dimensionnés par flotte de nœuds et concurrence de backtest.
- **Metering d'utilisation & compute.** Facturez les heures de backtest, les heures de nœud en direct, et le stockage — naturellement mesuré par la flotte de conteneurs que vous exploitez déjà.
- **Niveaux de revendeur white-label.** Facturez plus pour une remaque complète (logo, couleurs, PWA, `ShowSiteLink=false`) et pour activer les capacités premium via [basculements de fonctionnalité](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **IA gérée.** Regroupez une clé de fournisseur IA par défaut afin que chaque utilisateur client obtienne l'IA sans configuration, et majorez l'utilisation — ou proposez bring-your-own-key. → [Fonctionnalité IA](./features/ai.md)
- **Partage de revenu prop-firm & copy-trading.** Accueillez les firms exécutant des défis et des frais de performance et prenez une réduction de plateforme. → [Prop-firm](./features/prop-firm.md) · [Frais de performance](./features/copy-performance-fees.md) · [Place de marché des fournisseurs](./features/copy-provider-marketplace.md)
- **Setup, onboarding & SLA.** Attachez les services professionnels et le support premium.

## Modèles multi-client

- **Déploiement par client (recommandé).** Une instance marquée par client — isolement fort, branding et base de données par client, un jeton de jointure de nœud distinct par client. Le branding est lu depuis `IOptionsMonitor`, donc chaque instance porte sa propre identité. → [Branding multi-client](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Découverte de nœud](./operations/node-discovery.md)
- **Plan de contrôle partagé (avancé).** Pilotez beaucoup d'instances depuis votre propre couche de provisioning, semant le branding et les fonctionnalités par client par programmation.

## Metering d'utilisation pour la facturation

Un endpoint **`GET /api/usage`** en lecture seule pour propriétaire/admin renvoie un résumé qu'un fournisseur peut sonder et facturer — sans aucun nouveau domaine ou persistance, il projette l'état existant :

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Sondez-le par déploiement client pour piloter les tarifs basés sur les sièges, la flotte ou la charge de travail. Associez avec [logging & observabilité](./operations/logging.md) pour un metering plus fin du compute.

## Maintenir les marges prévisibles

Escaladez les nœuds à la demande, partagez les tiers Postgres, et auto-escaladez le tier sans état. Les surfaces opérationnelles dont vous avez besoin sont déjà là :

- [Scaling & auto-guérison](./deployment/scaling.md)
- [Logging & observabilité](./operations/logging.md)
- [Backup & recovery](./operations/backup-recovery.md)

## Commencez

1. Montez un déploiement de référence depuis les [guides cloud](./deployment/cloud.md).
2. Modélisez-le par client (branding + jeton de jointure + DB) et câblez votre facturation à l'utilisation du compute.
3. Listez-le — vous avez maintenant une plateforme de trading algo gérée à vendre.

## Contribuez

Les fournisseurs exécutant cMind à l'échelle frappent d'abord les bords aigus. Remonter vos correctifs opérationnels et améliorations IaC garde votre flotte bon marché à maintenir — commencez par le [guide de contribution](./contributing.md).
