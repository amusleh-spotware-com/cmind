---
description: "Santé de la stratégie & Déclin alpha — détection de déclin déterministe qui compare le Sharpe récent d'une stratégie à son enregistrement antérieur et localise le plus grand changement de moyenne (CUSUM change-point), retournant un verdict Saine / Dégradation / Décadence."
---

# Santé de la stratégie & Déclin alpha

Chaque avantage décline — la recherche est brutale que la demi-vie d'une stratégie quant s'est effondrée des années aux mois, donc *l'adaptation bat la découverte*. Le moniteur Strategy Health vous dit, à partir de l'historique de rendements propres de la stratégie, si l'avantage est toujours là.

Ouvrez **cBots → Santé de la stratégie** (`/quant/health`).

## Que fait-il

Étant donné une série de rendements (ou courbe d'équité, plus ancien d'abord), il :

- divise l'historique en une moitié **antérieure** et **récente** et compare leurs ratios Sharpe ;
- exécute une analyse **CUSUM change-point** pour localiser l'observation où la moyenne s'est décalée le plus clairement (une rupture de régime), rapportée uniquement quand la déviation est statistiquement notable ;
- retourne un verdict :

| Verdict | Signification |
|---|---|
| **Saine** | Les performances récentes sont en ligne avec (ou mieux que) l'enregistrement antérieur. |
| **Dégradation** | Le Sharpe récent est matériellement plus faible que l'enregistrement antérieur — observez de près. |
| **Décadence** | L'avantage a effectivement disparu dans la fenêtre récente — considérez une pause. |
| **Inconnue** | Pas assez d'historique à juger. |

```http
POST /api/quant/health
{ "returns": [...] }   // ou { "equity": [...] }
```

## Pourquoi c'est fiable

C'est du code de domaine pur déterministe (`Core.Health`) sans dépendance d'infrastructure et sans appels externes — testé en unité pour les cas décadents, dégradants, sains et trop courts et pour la localisation de change-point. C'est le compagnon manuel des contrôles de santé toujours activés qui soutiennent les agents autonomes : les mêmes statistiques entraînent le disjoncteur qui dé-risque une stratégie en direct dont l'avantage s'estompe.
