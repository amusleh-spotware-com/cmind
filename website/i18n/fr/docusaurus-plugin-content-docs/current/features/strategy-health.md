---
description: "Strategy Health & Alpha Decay — détection déterministe de la décroissance qui compare la Sharpe récente d'une stratégie à son historique antérieur et localise le plus grand changement de moyenne (point de rupture CUSUM), retournant un verdict Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Tout avantage se désintègre — la recherche est claire : la demi-vie d'une stratégie quant s'est effondrée d'années à mois, donc *l'adaptation surpasse la découverte*. Le moniteur Strategy Health vous indique, à partir de l'historique des rendements d'une stratégie, si l'avantage est toujours présent.

Ouvrez **cBots → Strategy Health** (`/quant/health`).

## What it does

Étant donné une série de rendements (ou courbe d'équité, ancien en premier), elle :

- divise l'historique en une moitié **antérieure** et une moitié **récente** et compare leurs ratios Sharpe ;
- exécute une analyse **de point de rupture CUSUM** pour localiser l'observation où la moyenne s'est le plus clairement décalée (une rupture de régime), signalée uniquement lorsque la déviation est statistiquement notable ;
- retourne un verdict :

| Verdict | Signification |
|---|---|
| **Healthy** | La performance récente est conforme à (ou meilleure que) l'historique antérieur. |
| **Degrading** | La Sharpe récente est sensiblement plus faible que l'historique antérieur — surveillez attentivement. |
| **Decayed** | L'avantage a effectivement disparu dans la fenêtre récente — envisagez de mettre en pause. |
| **Unknown** | Pas assez d'historique pour juger. |

- **Directement depuis une exécution de backtest — pas de copier-coller.** Chaque backtest terminé expose une icône de cœur **Vérifier la santé de la stratégie** sur la ligne de la liste **Backtest** et sur sa vue de détail d'instance ; un clic exécute le moniteur sur la courbe d'équité stockée de cette exécution et affiche le verdict dans une boîte de dialogue. L'icône est désactivée jusqu'à ce que le backtest soit terminé et ait produit un rapport, elle n'est donc jamais un contrôle mort. Sous le capot, c'est `POST /api/quant/health/backtest/{instanceId}`, qui lit la courbe d'équité du rapport stocké.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Why it is reliable

C'est du code domaine pur et déterministe (`Core.Health`) sans dépendance infrastructure et sans appels externes — testé unitairement pour les cas décroissants, dégradants, sains et trop courts, et pour la localisation du point de rupture. C'est le compagnon manuel aux vérifications de santé toujours actives qui soutiennent les agents autonomes : les mêmes statistiques pilotent le disjoncteur qui réduit les risques d'une stratégie en direct dont l'avantage s'estompe.
