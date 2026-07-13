---
description: "Backtest Integrity Lab — statistiques déterministes de qualité institutionnelle (Sharpe probabiliste & déflatée, t-stat) qui transforment un backtest brut en verdict Robust / Fragile / Overfit, correction pour le nombre de configurations essayées."
---

# Laboratoire d'intégrité du Backtest

Les plates-formes de détail vous montrent le Sharpe ou le bénéfice net d'un backtest et s'arrêtent là. Les institutions ne font jamais confiance à un backtest brut — elles demandent si le résultat survive **correction pour le biais de sélection et le nombre de configurations essayées**. Le Backtest Integrity Lab apporte cette vérification à cMind. C'est des **mathématiques déterministes** (pas d'IA, pas d'appels externes), donc le verdict est reproductible et chaque nombre est expliquable.

Ouvrez-le à **cBots → Intégrité** (`/quant/integrity`).

## Ce qu'il calcule

Étant donné une série de rendements (ou une courbe d'équité/solde) et le nombre d'ensembles de paramètres que vous avez essayés pour y arriver, l'analyseur rapporte :

- **Ratio de Sharpe** — par période et annualisé (racine carrée du temps).
- **Ratio de Sharpe Probabiliste (PSR)** — la confiance que le *vrai* Sharpe dépasse le benchmark, en tenant compte de la longueur de la piste, de l'asymétrie et de l'aplatissement (Bailey & López de Prado, 2012). Un enregistrement court ou à queue grasse le baisse.
- **Ratio de Sharpe Déflatée (DSR)** — PSR mesuré par rapport à un **benchmark déflaté** : le Sharpe que vous attendriez du *meilleur des N essais aléatoires* sous la null (le Théorème de Fausse Stratégie). Plus de configurations vous essayez, plus haut la barre — c'est ce qui attrape la suroptimisation.
- **Statistique t** du rendement moyen. Suivant Harvey, Liu & Zhu, un vrai avantage devrait franchir **t ≥ 3.0**, pas le 2.0 du manuel scolaire.
- **Asymétrie / Aplatissement** des rendements, qui alimentent les corrections PSR/DSR.

## Le verdict

| Verdict | Signification | Règle |
|---|---|---|
| **Robuste** | L'avantage survit aux essais que vous avez lancés. | DSR ≥ 95% **et** PSR ≥ 95% **et** \|t\| ≥ 3.0 |
| **Fragile** | Statistiquement vivant mais pas de manière convaincante — ne dimensionnez pas uniquement sur la base de cela. | entre les deux |
| **Suroptimisé** | Très probablement un artefact du biais de sélection, pas un vrai avantage. | DSR < 90% |

Chaque résultat porte une rationale en anglais clair afin que le "pourquoi" ne soit jamais caché.

## Probabilité de suroptimisation du backtest (sur les essais)

Alimenter un *nombre* d'essai est bon ; alimenter la **série réelle out-of-sample de chaque configuration que vous avez essayée** est mieux. Collez-les dans la **grille d'essai** optionnelle (une série par ligne) et cMind exécute **Validation Croisée Combinatoriquement-Symétrique** (Bailey, Borwein, López de Prado & Zhu, 2015) : elle divise les observations en groupes, et pour chaque façon de choisir la moitié comme in-sample elle choisit la meilleure configuration in-sample et vérifie si ce gagnant se retrouve dans la moitié inférieure **out-of-sample**. La **Probabilité de suroptimisation du backtest (PBO)** est la fraction de divisions où le gagnant a échoué à généraliser. Une PBO proche de 0 signifie que la meilleure configuration est véritablement meilleure ; une PBO de 0,5 ou plus signifie que votre processus de sélection choisit le bruit — le verdict devient **Suroptimisé** peu importe la beauté du gagnant.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Quand l'optimiseur cTrader Console natif arrivera, cMind alimentera sa surface d'essai complète ici automatiquement.

## Essais — le nombre qui compte

`Trials` est **combien d'ensembles de paramètres vous avez testés** avant de choisir celui-ci. Tester une stratégie et tester dix mille et garder la meilleure sont des choses sauvagement différentes : la seconde fabrique un Sharpe in-sample élevé par chance. Alimenter le nombre d'essai honnête est tout le point — cela augmente la déflation et peut déplacer un backtest "excellent" à **Suroptimisé**. Quand l'optimiseur cTrader Console natif arrivera, cMind alimente sa taille de grille de balayage réelle automatiquement.

## Entrées

- **Rendements périodiques** — un nombre par période (par exemple `0.01` = +1%). Au moins deux.
- **Courbe d'équité / solde** — cMind dérive pour vous les rendements simples consécutifs.
- Ou exécutez-le directement sur un backtest complété : `POST /api/quant/integrity/backtest/{instanceId}` lit la courbe d'équité du rapport stocké.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Retourne le verdict, toutes les métriques et la rationale. `POST /api/quant/integrity/backtest/{id}` exécute la même analyse sur un backtest complété que vous possédez.

## Pourquoi c'est fiable

Les statistiques sont des fonctions pures dans le cœur du domaine (`Core.Quant`) sans dépendances d'infrastructure — elles ne peuvent pas être mises à terre par une pépite de réseau, et elles sont épinglées par des tests unitaires de vecteur doré par rapport aux formules publiées. Le CDF/inverse normal sont des approximations de forme fermée (Abramowitz-Stegun / Acklam), donc les mêmes entrées donnent toujours le même verdict.
