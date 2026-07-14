---
slug: /features
title: Fonctionnalités — la visite complète
description: Tout ce que cMind peut faire — copy trading, IA, build & backtest, gardiens prop-firm, white-label, PWA, MCP, et plus.
sidebar_label: Aperçu
---

# Fonctionnalités — la visite complète 🧭

Bienvenue à la visite grandiose. cMind emballe *beaucoup* dans une app, donc voici la carte. Chaque capacité a son propre doc de plongée profonde — cliquez à travers vers ce qui vous gratte.

## 🔁 Copy trading

Le joyau de la couronne. Reflétez un compte maître sur beaucoup, et gardez-les synchronisés même quand internet se misbehave.

- **[Copy trading](./copy-trading.md)** — le core : mirroring, types d'ordres, SL/TP, slippage, desync/resync.
- **[Transparence d'exécution](./copy-execution-transparency.md)** — voyez exactement ce qui a été copié, quand, et pourquoi.
- **[Frais de performance](./copy-performance-fees.md)** — facturez pour votre signal, style high-water-mark.
- **[Place de marché des fournisseurs](./copy-provider-marketplace.md)** — laissez les traders découvrir et suivre les fournisseurs.
- **[Notifications](./copy-notifications.md)** — soyez averti quand quelque chose vous a besoin.
- **[Recommandeur AI de copie](./ai-copy-recommender.md)** — laissez l'IA suggérer qui copier.
- **[Cycle de vie du token Open API](./token-lifecycle.md)** — comment cMind garde exactement un token valide par cID.

## 📊 Votre base d'accueil

- **[Dashboard](./dashboard.md)** — le centre de commandement en direct, mobile-first : KPIs avec sparklines, un graphique d'activité, un anneau de statut, un flux en direct, et (pour les admins) santé du cluster. Il se rafraîchit lui-même.

## 🧠 Noyau IA

Pas une boîte de chat boulonnée sur le côté — IA qui *fait réellement le travail*.

- **[Assistant IA, agent, garde de risque & alertes](./ai.md)** — génération de stratégie, builds auto-réparateurs, une garde de risque de fond qui peut auto-arrêter les bots, et des alertes intelligentes.

## 🛠️ Build & run

- **[Construire & backtester les cBots](./build-and-backtest.md)** — l'IDE Monaco du navigateur, modèles C#/Python, builds en sandbox, et courbes d'équité en direct.
- **[Serveur MCP](./mcp.md)** — exposez les outils de cMind sur HTTP + SSE afin que les clients IA puissent le piloter.

## 🏢 L'exécuter comme une activité

- **[White-label / branding](./white-label.md)** — remaclez chaque surface via config.
- **[Simulation de défi prop-firm](./prop-firm.md)** — appliquez les règles de perte quotidienne, drawdown, et target avec équité en direct.
- **[Basculements de fonctionnalité](./feature-toggles.md)** — décidez ce que chaque déploiement/client voit.
- **[Compliance / légal](./compliance.md)** — la piste d'audit et la surface légale.

## 📱 L'expérience

- **[App installable (PWA)](./pwa.md)** — mobile-first, coquille hors ligne, ajouter-à-l'écran-d'accueil.
- **[Système de design UI & mobile-first](../ui-guidelines.md)** — les jetons de design et les règles derrière le look.

## ⚙️ Sous le capot

Les bits opérationnels qui gardent tout cela en marche :

- **[Flotte de nœuds & découverte](../operations/node-discovery.md)** — comment les nœuds s'enregistrent automatiquement et guérissent.
- **[Scaling horizontal](../deployment/scaling.md)** — ajoutez des répliques, aucun coordinateur externe nécessaire.
- **[Logging & audit](../operations/logging.md)** — logs structurés + OpenTelemetry.
- **[Déploiement](../deployment/local.md)** — faites-le fonctionner n'importe où.

:::note[Garder les docs honnêtes]
Chaque doc de fonctionnalité est gardée en lockstep avec le code — changez le comportement, mettez à jour la doc, même commit. Si vous repérez jamais la dérive, c'est un bug : s'il vous plaît [ouvrez une issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) ou envoyez un PR. 🙏
:::
