---
description: "Backtest Integrity Lab — statistiques de surapprentissage de qualité institutionnelle déterministes (Sharpe probabiliste et dégonflé, t-stat) qui transforment un backtest brut en verdict Robuste / Fragile / Surappris, corrigé selon le nombre de configurations essayées."
---

# Laboratoire d'intégrité du Backtest

Les plateformes de vente au détail vous montrent le Sharpe ou le profit net d'un backtest et s'arrêtent là. Les institutions ne font jamais confiance à un backtest brut — elles demandent si le résultat survit à la **correction du biais de sélection et du nombre de configurations essayées**. Le Laboratoire d'intégrité du Backtest apporte cette vérification à cMind. C'est des **mathématiques déterministes** (pas d'IA, pas d'appels externes), donc le verdict est reproductible et chaque nombre est explicable.

Ouvrez-le à **cBots → Intégrité** (`/quant/integrity`).

## Ce qu'il calcule

Étant donné une série de rendements (ou une courbe d'équité/solde) et le nombre d'ensembles de paramètres que vous avez essayés pour l'obtenir, l'analyseur rapporte :

- **Ratio de Sharpe** — par période et annualisé (racine carrée du temps).
- **Probabilistic Sharpe Ratio (PSR)** — la confiance que le *vrai* Sharpe dépasse l'indice de référence, en tenant compte de la longueur de l'historique, de l'asymétrie et de l'aplatissement (Bailey & López de Prado, 2012). Un historique court ou à queue grasse le réduit.
- **Deflated Sharpe Ratio (DSR)** — PSR mesuré par rapport à un **indice de référence dégonflé** : le Sharpe que vous auriez esperé du *meilleur des N essais aléatoires* sous l'hypothèse nulle (le False Strategy Theorem). Plus vous essayez de configurations, plus la barre est élevée — c'est ce qui détecte le surapprentissage.
- **t-statistic** du rendement moyen. Selon Harvey, Liu & Zhu, un avantage réel devrait dépasser **t ≥ 3,0**, pas le classique 2,0.
- **Asymétrie / aplatissement** des rendements, qui alimentent les corrections PSR/DSR.

## Le verdict

| Verdict | Signification | Règle |
|---|---|---|
| **Robuste** | L'avantage survit aux essais que vous avez effectués. | DSR ≥ 95% **et** PSR ≥ 95% **et** \|t\| ≥ 3,0 |
| **Fragile** | Statistiquement vivant mais pas de manière convaincante — ne montez pas en taille basé sur ceci seul. | entre les deux |
| **Surappris** | Probablement un artefact du biais de sélection, pas un véritable avantage. | DSR < 90% |

Chaque résultat s'accompagne d'une justification en langage clair afin que le « pourquoi » ne soit jamais caché.

## Probabilité de surapprentissage du Backtest (entre les essais)

Entrer un *nombre* d'essais est bien ; entrer la **série réelle hors-échantillon de chaque configuration que vous avez essayée** est mieux. Collez-les dans la **grille d'essais** optionnelle (une série par ligne) et cMind exécute une **validation croisée combinatoirement symétrique** (Bailey, Borwein, López de Prado & Zhu, 2015) : il divise les observations en groupes, et pour chaque façon de choisir la moitié comme échantillon d'apprentissage, il choisit la meilleure configuration en échantillon et vérifie si ce gagnant atterrit dans la moitié inférieure **hors-échantillon**. La **Probabilité de surapprentissage du Backtest (PBO)** est la fraction de divisions où le gagnant n'a pas généralisé. Un PBO proche de 0 signifie que la meilleure configuration est vraiment la meilleure ; un PBO de 0,5 ou plus signifie que votre processus de sélection choisit du bruit — le verdict devient **Surappris** indépendamment de la qualité du gagnant.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Lorsque l'optimiseur native cTrader Console sera lancé, cMind alimentera sa surface complète d'essais ici automatiquement.

## Essais — le nombre qui compte

`Trials` est **le nombre d'ensembles de paramètres que vous avez testés** avant de choisir celui-ci. Tester une stratégie et en tester dix mille et garder la meilleure sont des choses très différentes : la seconde fabrique un Sharpe en-échantillon élevé par hasard. Entrer le nombre d'essais honnête est le but entier — cela augmente la dégonflation et peut déplacer un backtest « excellent » vers **Surappris**. Lorsque l'optimiseur native cTrader Console sera lancé, cMind alimente la taille réelle de la grille du balayage automatiquement.

## Entrées

- **Rendements périodiques** — un nombre par période (p. ex. `0,01` = +1%). Au moins deux. Le champ se valide au fur et à mesure que vous tapez : il compte les nombres valides, signale tout jeton qui n'est pas un nombre, et active **Analyser** uniquement une fois qu'au moins deux valeurs propres sont présentes (la grille d'essais active **Évaluer le surapprentissage** une fois deux séries de quatre nombres ou plus chacune prêtes).
- **Courbe d'équité / solde** — cMind dérive les rendements simples consécutifs pour vous.
- **Directement depuis une exécution de backtest — pas de copie-collage.** Chaque backtest complété expose une icône de bouclier **Vérifier l'intégrité du backtest** sur la ligne de liste **Backtest** et sur sa vue détail d'instance ; un clic exécute le Lab sur la courbe d'équité stockée de cette exécution et affiche le verdict dans une boîte de dialogue. L'icône est désactivée jusqu'à ce que le backtest soit complété et ait produit un rapport, donc c'est jamais un contrôle mort. Sous le capot, ceci est `POST /api/quant/integrity/backtest/{instanceId}`, qui lit la courbe d'équité du rapport stocké.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Retourne le verdict, toutes les métriques et la justification. `POST /api/quant/integrity/backtest/{id}` exécute la même analyse sur un backtest complété dont vous êtes propriétaire.

## Pourquoi c'est fiable

Les statistiques sont des fonctions pures dans le cœur du domaine (`Core.Quant`) sans aucune dépendance d'infrastructure — elles ne peuvent pas être mises hors ligne par un hoquet réseau, et elles sont épinglées par des tests unitaires à vecteur doré par rapport aux formules publiées. Les CDF normales/inverses sont des approximations de forme fermée (Abramowitz-Stegun / Acklam), donc les mêmes entrées produisent toujours le même verdict.
