---
description: "Dimensionnement de position institutionnel pour le détail — ciblage de volatilité et exposition Kelly fractionnaire pour une seule stratégie, plus allocation de parité de risque par volatilité inverse avec matrice de corrélation sur un livre de stratégies."
---

# Dimensionnement de position & Portefeuille

"Quelle devrait être la taille de cette transaction?" est la question qui décide si un avantage se compose ou explose. Les institutions la répondent avec **ciblage de volatilité** et le **critère Kelly**, et elles construisent un livre avec **parité de risque** plutôt que dollars égaux. cMind apporte les deux au détail — mathématiques déterministes sur une série de rendements de stratégie, avec une recommandation en anglais clair.

Ouvrez **cBots → Dimensionnement de position** (`/quant/sizing`).

## Dimensionnement d'une seule stratégie

Étant donné les rendements d'une stratégie (ou courbe d'équité), une volatilité annuelle cible, une fraction Kelly et un plafond de levier, le sizer rapporte :

- **Volatilité annuelle réalisée** — la volatilité propre de la stratégie, annualisée par la règle de racine carrée du temps.
- **Dimensionnement de ciblage de volatilité** — l'exposition qui rend la volatilité réalisée rencontrer votre cible (`target ÷ vol réalisé`), plafonné à votre limite de levier. Les stratégies à vol inférieur obtiennent une taille plus grande.
- **Kelly complet** — la fraction de croissance optimale `f* = μ / σ²` (moyenne sur variance des rendements).
- **Kelly fractionnaire** — `f*` mis à l'échelle par votre fraction Kelly. Demi-Kelly (0.5) est le choix courant sûr ; Kelly complet est fameux comme trop agressif pour des avantages réels et incertains.
- **Exposition recommandée** — la **plus petite** (plus sûre) des sizages volatilité-cible et Kelly-fractionnaire, plafonnée. Une stratégie sans avantage positif (Kelly complet ≤ 0) est dimensionnée à **zéro**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Allocation de portefeuille

Donnez-lui deux stratégies ou plus (séries de rendements alignées) et il construit un livre par **parité de risque par volatilité inverse** — chaque stratégie pondérée par `1 / volatility`, normalisée — afin que le risque, pas les dollars, soit partagé équitablement. Elle retourne également :

- la **matrice de corrélation** sur vos stratégies (repérez celles qui sont secrètement le même pari) ;
- la **volatilité de portefeuille projetée** à ces pondérations, à partir de la covariance d'échantillon ;
- un facteur de **levier** qui met à l'échelle le livre entier vers votre volatilité cible (plafonné).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Pourquoi c'est fiable

Tout cela est du code de domaine pur déterministe (`Core.Portfolio`) sans dépendance d'infrastructure et sans appels externes — testé en unité pour la mise à l'échelle vol-target, la formule Kelly, la propriété égale-risque des poids volatilité-inverse et la matrice de corrélation. Consultatif par défaut : les chiffres sont une recommandation, jamais un ordre automatique.
