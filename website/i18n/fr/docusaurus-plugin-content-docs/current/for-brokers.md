---
slug: /for-brokers
title: cMind pour les courtiers cTrader
description: Pourquoi un courtier cTrader devrait exécuter un cMind white-label pour ses propres clients — donnez aux traders l'IA, le copy trading et les défis prop-firm sous votre marque, limitez les comptes à votre courtage, et gagnez un avantage sur les concurrents.
keywords:
  - Courtier cTrader
  - Plateforme de trading white-label
  - Technologie de courtier
  - Copy trading pour les courtiers
  - Outils de trading IA
  - Logiciel de prop firm
sidebar_position: 6
---

# cMind pour les courtiers cTrader 🏦

Vous gérez un courtage cTrader. Vos clients peuvent déjà trader — mais tout aussi les clients des autres courtiers. **cMind vous permet de remettre à vos traders une plateforme complète d'opérations de trading alimentée par l'IA, marquée comme la vôtre**, afin qu'ils construisent, backtestent, exécutent, copient, et surveillent les stratégies dans *votre* écosystème au lieu de dériver vers un outil tiers. C'est des clients plus collants, plus de volume, et un vrai avantage sur les courtiers n'offrant rien qu'un terminal.

:::tip[TL;DR]
Exécutez un cMind white-label pour vos clients. Limitez les comptes à **votre** courtage, activez l'IA et le copy trading, et livrez-le sous votre marque. → [White-label pour le business](./white-label-for-business.md)
:::

## L'avantage que vous obtenez sur les autres courtiers

- **Différenciez-vous sur les outils, pas seulement sur les spreads.** Donnez aux clients la génération de cBot IA, le backtesting sur un cluster géré, le copy trading, et les défis prop-firm — des capacités que la plupart des courtiers ne proposent tout simplement pas.
- **Gardez les clients dans votre écosystème.** Quand les traders construisent et exécutent leurs stratégies dans votre plateforme marquée, ils restent. La rétention est le tout du jeu.
- **Sous votre marque, sur votre domaine.** Nom, logo, couleurs, favicon, même l'app téléphone installable — tout est vôtre. Personne ne voit « cMind ». → [Fonctionnalité White-label](./features/white-label.md)

## Servez uniquement vos comptes (liste blanche des courtiers)

Exécutez un white-label pour *vos* clients ? Limitez les courtiers dont les comptes de trading les utilisateurs peuvent ajouter afin que votre déploiement n'ait jamais servi que votre portefeuille :

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Votre Nom de Courtage"]
    }
  }
}
```

Quand la liste blanche est définie, cMind vérifie tous les comptes qu'un utilisateur essaie d'ajouter — via l'API cTrader Open et via la connexion manuelle cID (vérifiée en lisant le nom du courtier réel du compte) — et rejette tous les comptes qui ne sont pas sur votre liste. Laissez-la vide et chaque courtier est autorisé (la par défaut). Voir la [doc de fonctionnalité White-label](./features/white-label.md#broker-allowlist) pour la mécanique complète.

## Livrez une seule app Open API pour tous vos utilisateurs

Ignorez les tracas par utilisateur : fournissez **une seule application cTrader Open API** et chaque client autorise ses comptes via celle-ci — aucun client n'enregistre jamais le sien. Enregistrez une seule URL de redirection, déposez les identifiants dans la config ou les paramètres du propriétaire, et le mode partagé s'active pour tous. Vous avez négocié une limite de messages cTrader supérieure ? Accordez les **limites de taux de client par type de message** (ou désactivez le pacing). → [App Open API partagée & limites de taux](./features/open-api-shared-app.md)

## Nouvelles façons de monétiser

- **IA, sans aucune friction pour les clients.** Fournissez une clé de fournisseur IA par défaut au niveau du déploiement et chaque client obtient les fonctionnalités IA instantanément — pas d'inscription ailleurs. Majorez-la, ou regroupez-la dans les niveaux premium. Les clients peuvent toujours apporter leur propre clé. → [Fonctionnalité IA](./features/ai.md)
- **Défis prop-firm.** Exécutez des défis de trader financés avec suivi d'équité en direct et règles appliquées, et facturez les entrées. → [Règles de prop-firm](./features/prop-firm.md)
- **Activité de copy-trading.** Les frais de performance et une place de marché des fournisseurs transforment le copy trading en revenu. → [Frais de performance](./features/copy-performance-fees.md) · [Place de marché des fournisseurs](./features/copy-provider-marketplace.md)
- **Niveaux de fonctionnalité.** Décidez quelles capacités chaque segment client voit avec [basculements de fonctionnalité](./features/feature-toggles.md).

## Réglementé, auditable, multi-client

- **Les logs de [Compliance](./features/compliance.md)** vous donnent la piste d'audit que votre régulateur demandera.
- **[L'authentification à deux facteurs](./features/two-factor-auth.md)** peut être rendue obligatoire par déploiement.
- **Branding par client** — exécutez une instance marquée séparée par segment, pilotée depuis votre propre plan de contrôle. → [Branding multi-client](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Comment commencer

1. Lisez [White-label pour le business](./white-label-for-business.md) pour la remaque en 60 secondes.
2. Définissez `App:Accounts:AllowedBrokers` à votre courtage et choisissez votre [ensemble de fonctionnalités](./features/feature-toggles.md).
3. [Déployez-le](./deployment/cloud.md) — Docker, Kubernetes, Azure, ou AWS.

Vous ne voulez pas gérer l'infrastructure vous-même ? Un fournisseur d'hébergement peut opérer un cMind géré pour vous — pointez-les vers [Pour les fournisseurs cloud & VPS](./for-cloud-providers.md).

## Façonnez la roadmap

cMind est open source. Les courtiers qui le construisent obtiennent une parole outsized sur la direction — demandez les intégrations et les contrôles dont vous avez besoin, et contribuez-les via le [guide de contribution](./contributing.md).
