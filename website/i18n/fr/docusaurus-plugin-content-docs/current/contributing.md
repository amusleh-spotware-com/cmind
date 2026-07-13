---
slug: /contributing
title: Contribuer
description: Comment contribuer à cMind — les PRs assistées par l'humain ou l'IA sont bienvenues. Première contribution en 10 minutes.
sidebar_position: 5
---

# Contribuer à cMind 🛠️

Merci d'être là. cMind s'améliore chaque fois que quelqu'un ouvre un issue, rapporte un comportement cTrader précis,
corrige une typo dans ces très docs, ou fournit une PR. **Vous n'avez pas besoin d'être un gourou .NET** — les testeurs, traders, et fixeurs de doc sont
aussi appréciés que ceux qui écrivent des agrégats.

:::tip Le guide canonique vit dans le repo
Cette page est l'on-ramp convivial. Le processus complet, toujours actuel — règles fondamentales, conventions de codage,
flux de révision — est dans **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Votre première contribution en ~10 minutes

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 avertissements, ou CI vous refusera poliment
dotnet test           # unit + integration + E2E
```

Trouvé quelque chose à corriger ? Branchez-le, changez-le, ajoutez un test, et ouvrez une PR. C'est la boucle entière.

## Façons d'aider (pas toutes sont du code)

| Contribution | Effort | Où |
|---|---|---|
| 🐛 Signalez un bug reproductible | 10 min | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Suggérez une feature | 10 min | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Améliorez ces docs | 15 min | Éditer sous `website/docs/` et PR |
| 🧪 Ajoutez un test manquant | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Signalez le comportement exact cTrader | 10 min | [Ouvrir une Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Les règles de la maison (version courte)

cMind déplace **de l'argent réel**, donc quelques choses sont non-négociables — et honnêtement, elles rendent la codebase
une joie de travailler dedans :

- **Strict Domain-Driven Design.** La logique métier vit sur les agrégats et les objets de valeur, jamais dans
  les endpoints ou l'UI. (Il y a un playbook convivial pour cela dans le repo.)
- **Trois tiers de test, chaque changement.** Unit + integration + E2E, *y compris* les chemins d'échec (connexions
  larguées, ordres rejetés, nœuds morts). Les tests verts sont le prix d'admission.
- **Zéro avertissements.** `TreatWarningsAsErrors=true`. Idiomes C# 14 modernes.
- **Pas de secrets, pas de chaînes magiques, jamais `DateTime.UtcNow`** (injectez `TimeProvider` à la place).
- **Docs dans le même commit.** Changez le comportement → mettez à jour son doc. Oui, cela inclut ce site.

Détail complet, avec le *pourquoi* derrière chaque règle, dans
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) et
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Contribuer avec l'IA 🤖

Nous accueillons vraiment les **PRs assistées par l'IA** — ce projet est construit pour être travaillé par les agents aussi bien que
les humains. Si vous conduisez Claude, Copilot, ou similaire : pointez-le vers
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), laissez-le lire les fichiers `CLAUDE.md` imbriqués, et maintenez-le au même niveau (tests, zéro avertissements, DDD). Une bonne PR d'IA est
indistinguible d'une bonne PR humaine — même révision, même bienvenue.

## Soyez excellent les uns envers les autres

Nous avons un [Code de Conduite](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
L'essence : soyez gentil, assumez la bonne foi, et rappelez-vous qu'il y a une personne (ou un agent d'une personne) à l'autre bout. Posez des questions tôt — c'est une force, pas une gêne.

Bienvenue à bord. Nous sommes impatients de voir ce que vous construirez. 🎉
