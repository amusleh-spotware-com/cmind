---
description: "Analyse du coût des transactions — mesure la qualité d'exécution (glissement en points de base et insuffisance d'implémentation) d'une commande par rapport à son prix d'arrivée, l'avantage d'exécution composé que les banques vivent. Déterministe."
---

# Analyse du coût des transactions (TCA)

L'alpha d'exécution est minuscule par transaction et énorme sur des milliers d'entre elles — c'est une grande partie de la façon dont les banques et les bureaux de prop conservent leur avantage. TCA mesure à quel point le prix que vous avez réellement atteint a dérivé par rapport au prix quand vous avez *décidé* de commercer.

Ouvrez **cBots → Coût d'exécution** (`/quant/tca`).

## Qu'il mesure

Étant donné le **prix d'arrivée (décision)**, le **côté**, et vos **remplissages** (prix × quantité), il rapporte :

- **Prix de remplissage moyen (VWAP)** — le prix pondéré par le volume que vous avez réellement obtenu.
- **Glissement (bps)** — la dérive de l'arrivée au VWAP en points de base, **signé afin qu'un nombre positif soit un coût** (acheter au-dessus de l'arrivée ou vendre au-dessous) et un nombre négatif soit une amélioration de prix.
- **Insuffisance d'implémentation** — ce coût exprimé en termes prix × quantité : l'argent que la dérive vous a coûté sur cette commande.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Tranchage intelligent (Almgren-Chriss)

Au-delà de mesurer le coût, cMind peut planifier une grande commande pour *minimiser* le coût. **cBots → Calendrier d'exécution** (`/quant/execution`) construit un **calendrier d'exécution optimal Almgren-Chriss** : étant donné la quantité totale, un nombre de tranches, votre aversion au risque, la volatilité et l'impact de marché temporaire, il retourne la taille à négocier dans chaque tranche. L'aversion au risque plus élevée **charge en avant** le calendrier (risque de synchronisation réduite) ; l'aversion au risque nul s'aplatit sur un **TWAP** pair. Les tranches s'additionnent toujours au total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Pourquoi c'est fiable

Code de domaine pur et déterministe (`Core.Execution`) sans dépendance d'infrastructure et pas d'appels externes — testé en unité pour le signe de coût achat/vente, l'amélioration de prix, le glissement nul, l'agrégation VWAP et les garde-fous d'entrée. C'est la moitié de mesure de la qualité d'exécution ; c'est la même métrique de shortfall que le moteur de copie utilise pour juger (et, avec tranchage intelligent, réduire) le coût des ordres miroir.
