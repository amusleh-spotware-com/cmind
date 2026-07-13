---
description: "Regime Lab — étiquette une série de rendements dans les régimes de volatilité Calme / Normal / Turbulent et rapporte les performances par régime, plus l'exposant Hurst (persistance de tendance vs réversion à la moyenne). Déterministe."
---

# Regime Lab

Un seul ratio de Sharpe cache la vérité que la plupart des avantages sont conditionnels : excellents en marchés calmes et tendance, morts en turbulence (ou l'inverse). Le Regime Lab divise l'historique d'une stratégie en régimes de volatilité et affiche les performances dans chacun — afin que vous sachiez *quand* votre avantage fonctionne réellement.

Ouvrez **cBots → Regime Lab** (`/quant/regimes`).

## Que fait-il

Étant donné une série de rendements (ou courbe d'équité, plus ancien d'abord), il :

- calcule une **volatilité réalisée traînante** à chaque point et divise l'historique en régimes **Calme**, **Normal** et **Turbulent** par les terciles de cette volatilité ;
- rapporte les **performances par régime** — observations, rendement moyen, volatilité et Sharpe — afin que vous puissiez voir où l'avantage vit ;
- estime l'**exposant Hurst** via analyse rescaled-range (R/S) : au-dessus de ~0,55 la série est **tendance / persistante**, au-dessous de ~0,45 elle est **mean-reverting**, et autour de 0,5 elle est proche d'une marche aléatoire.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // ou { "equity": [...] }
```

## Pourquoi c'est fiable

Code de domaine pur déterministe (`Core.Regimes`) sans dépendance d'infrastructure et sans appels externes — testé en unité pour la séparation des régimes (volatilité calme vs turbulent) et pour la direction Hurst (les séries anti-persistantes scoren en dessous de 0.5, une tendance persistante score au-dessus). Le même signal de régime alimente la boucle de réflexion des agents autonomes, afin qu'un agent puisse s'appuyer sur les régimes où son avantage est réel.
