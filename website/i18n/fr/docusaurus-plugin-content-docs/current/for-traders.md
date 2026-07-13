---
slug: /for-traders
title: cMind pour les traders cTrader
description: Pourquoi un trader cTrader devrait auto-héberger cMind — possédez votre stack et vos données, authorisez, backtestez, exécutez et surveillez les cBots dans une console alimentée par l'IA, sur votre laptop, VPS ou téléphone.
keywords:
  - cTrader
  - Trading algorithmique
  - Plateforme de trading auto-hébergée
  - Backtesting de cBot
  - Bots de trading IA
  - Logiciel de trading open source
sidebar_position: 5
---

# cMind pour les traders cTrader 📈

Vous tradez déjà sur cTrader. Vous jonchez déjà avec un éditeur de code, un backtester, un VPS, et trois onglets de navigateur. **cMind effondre tout cela en une console sombre et conviviale au clavier que vous exécutez vous-même** — et c'est open source, donc rien à propos de votre avantage, vos stratégies, ou vos identifiants ne quitte jamais votre machine.

:::tip TL;DR
Auto-hébergez cMind sur un laptop, un VPS bon marché, ou un serveur domestique. Authorisez, backtestez, exécutez, et surveillez les cBots en un seul endroit, avec un noyau IA qui fait les corvées. → [Exécutez-le en 5 minutes](./deployment/local.md)
:::

## Pourquoi auto-héberger au lieu d'un service hébergé ?

- **Possédez votre stack et vos données.** Vos cBots, identifiants, tokens, et historique d'équité vivent sur **votre** infrastructure — pas de tiers, pas de verrouillage, pas d'email « nous supprimons ce produit ».
- **C'est véritablement vôtre à changer.** C# 14 / .NET 10, DDD strict, EF Core + PostgreSQL, un serveur MCP — tout open source et hackable. Forkez-le, étendez-le, envoyez un PR.
- **Pas de paywall par fonctionnalité.** Apportez votre propre clé IA pour n'importe quel fournisseur ; chaque fonctionnalité IA est activée.

Préférez-vous ne pas exécuter de serveurs vous-même ? Une entreprise d'hébergement peut exécuter un cMind géré pour vous — voir [Pour les fournisseurs cloud & VPS](./for-cloud-providers.md).

## Une console, pas de jonglage d'onglets

- **Authorisez** dans un vrai IDE Monaco (l'éditeur VS Code), avec des modèles C# **et** Python et `dotnet build` en sandbox dans des conteneurs jetables. → [Build & backtest](./features/build-and-backtest.md)
- **Backtestez** à travers une flotte de nœuds et regardez les courbes d'équité revenir en direct.
- **Exécutez** les stratégies en direct et **surveillez**-les depuis un tableau de bord. → [Dashboard](./features/dashboard.md)
- **Copiez** un compte maître sur de nombreux comptes à travers les courtiers et les ID cTrader, avec une réconciliation qui survit aux connexions perdues et aux tokens rotatifs. → [Copy trading](./features/copy-trading.md)

## L'IA qui fait les corvées, pas du bavardage

Apportez votre propre clé API (n'importe quel fournisseur supporté — cloud ou un modèle local) et obtenez du texte brut en anglais → un vrai cBot compilant avec une boucle d'auto-réparation, un ajustement des paramètres, des post-mortems de backtest, et une garde des risques qui peut auto-arrêter un bot qui se comporte mal. → [Rencontrez le noyau IA](./features/ai.md)

## Outils de qualité institutionnelle, pour un

La même rigueur qu'un bureau paie, sur votre propre machine :

- [Intégrité du backtest](./features/backtest-integrity.md) · [Dimensionnement des positions](./features/position-sizing.md)
- [Santé de la stratégie](./features/strategy-health.md) · [Labo de régime](./features/regime-lab.md)
- [TCA d'exécution](./features/execution-tca.md) · [Journal de trading](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Positionnement contrarian](./features/contrarian-positioning.md)

## Exécute où vous le faites

Commencez sur votre laptop avec `docker compose up`, passez à un VPS bon marché ou un serveur domestique quand vous êtes prêt, et vérifiez vos bots depuis votre téléphone — cMind est une [PWA](./features/pwa.md) installable et mobile-first. → [Exécutez-le localement](./deployment/local.md)

Voulez que votre client IA le pilote ? Il y a un [serveur MCP](./features/mcp.md) intégré.

## Aidez à l'améliorer

cMind est open source et sous licence MIT — la roadmap est façonnée par la communauté :

- Fichier des problèmes et des demandes de fonctionnalités, et votez sur ce qui compte.
- Ajouter des modèles de cBot, des adaptateurs de fournisseur IA, ou des traductions UI.
- Envoyez des PRs — trois niveaux de tests (unit + integration + E2E) et le DDD strict maintiennent la barre haute, et le [guide de contribution](./contributing.md) vous guide.

Prêt ? → [Lisez l'intro](./intro.md) puis [exécutez-le localement](./deployment/local.md).
