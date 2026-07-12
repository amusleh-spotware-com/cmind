---
slug: /intro
title: Bienvenue sur cMind
description: Une introduction accessible à cMind — la plateforme d'opérations de trading pour cTrader, open source et auto-hébergeable.
sidebar_position: 1
---

# Bienvenue sur cMind 👋

Vous voulez donc créer des bots de trading, les backtester sans faire fondre votre ordinateur portable,
les exécuter sur plusieurs machines, répliquer des transactions sur une douzaine de comptes et laisser
une IA surveiller le risque pendant que vous dormez. **Vous êtes exactement au bon endroit.**

cMind est une **plateforme d'opérations de trading pour cTrader, open source et auto-hébergeable**.
Voyez-la comme l'ensemble de votre desk de trading — création, exécution, une flotte de calcul, le copy
trading et un cœur d'IA — réuni dans une application calme, sombre et adaptée au mobile, qui vous
appartient de bout en bout.

:::tip En une phrase
Créez → backtestez → exécutez → copiez vos stratégies cTrader à grande échelle, avec l'IA intégrée, sur
vos propres serveurs et sous votre propre marque.
:::

## Que peut-il vraiment faire ?

| Vous voulez… | cMind le fait | En savoir plus |
|---|---|---|
| Écrire un cBot dans le navigateur | IDE Monaco + modèles C#/Python, compilations en bac à sable | [Créer & backtester](./features/build-and-backtest.md) |
| Backtester sur plusieurs machines | Une flotte de nœuds auto-réparatrice choisit la machine la moins chargée | [Mise à l'échelle](./deployment/scaling.md) |
| Copier un compte vers plusieurs | Réplication robuste avec resynchronisation, sans transactions en double | [Copy trading](./features/copy-trading.md) |
| Laisser l'IA faire le gros du travail | Génération de stratégies, auto-réparation, garde-fou du risque, post-mortems | [Cœur d'IA](./features/ai.md) |
| Respecter les règles de la prop firm | Suivi de l'équité en direct + simulation des règles de challenge | [Prop-firm](./features/prop-firm.md) |
| Le livrer comme *votre* produit | Marque blanche complète : nom, couleurs, logo, favicon | [Marque blanche](./features/white-label.md) |
| L'exécuter sur votre téléphone | PWA installable et pensée pour le mobile | [PWA](./features/pwa.md) |
| Le piloter depuis un client IA | Serveur MCP intégré (HTTP + SSE) | [MCP](./features/mcp.md) |

## Le parcours en 5 minutes ⏱️

Si vous avez Docker et cinq minutes, vous pouvez dès maintenant manipuler une vraie instance cMind :

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Ouvrez ensuite **<http://localhost:8080>**, connectez-vous, et c'est parti. Le guide complet (avec le
dépannage pour quand Docker aura inévitablement son mot à dire) se trouve dans
**[Exécuter en local](./deployment/local.md)**.

## Nouveau ici ? Suivez la route de briques jaunes 🟡

1. **[Pour qui est-ce ?](./audience.md)** — assurez-vous d'être notre genre de casse-tête.
2. **[Exécuter en local](./deployment/local.md)** — mettez en route une vraie instance.
3. **[Fonctionnalités](./features/README.md)** — la visite complète de ce qu'il contient.
4. **[Déployer pour de vrai](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Faites-le vôtre](./white-label-for-business.md)** — appliquez votre marque blanche pour votre entreprise.
6. **[Contribuer](./contributing.md)** — les PR (humaines *et* assistées par IA) sont les bienvenues.

## Un mot rapide sur l'argent 💸

cMind déplace **du capital réel**. Nous le prenons au sérieux — chaque changement est livré avec des
tests unitaires, d'intégration et de bout en bout, chemins d'échec compris (connexions coupées, ordres
rejetés, nœuds morts). Vous devriez le prendre au sérieux aussi : **testez d'abord sur un compte démo**,
et lisez les [notes de conformité](./features/compliance.md) avant de le pointer vers quoi que ce soit
de réel. Le trading est risqué ; ce logiciel est un outil, pas un conseil financier.

Bon — assez de préambule. Allons construire quelque chose. →
