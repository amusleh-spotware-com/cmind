---
description: "Journal de trading & Coach — analyse vos propres lancements et backtests pour les fuites comportementales (surconcentration, défaillances répétées, biais perdant) et vous coache sur la stratégie que vous avez déjà. Déterministe, avec récit IA optionnel."
---

# Journal de trading & Coach

La plus nouvelle catégorie vraiment utile de l'IA pour le commerce ne prédit pas le marché — elle analyse *votre propre* comportement. Le Trading Journal transforme votre historique de lancements et backtests en retours d'informations honnêtes afin que vous puissiez améliorer la stratégie que vous avez déjà.

Ouvrez **IA → Journal de trading** (`/journal`).

## Ce qu'il surface

À partir de vos instances (lancements et backtests), il calcule, déterministiquement :

- **Décomptes de victoire / défaite / défaillance et taux de victoire** sur vos backtests ;
- **Aperçus comportementaux** — les fuites qui coûtent silencieusement les traders de détail :
  - **Surconcentration** — la plupart de votre activité se concentre sur un symbole ;
  - **Défaillances répétées** — une part élevée de lancements n'a pas pu être construite ou configurée ;
  - **Biais perdant** — plus de backtests perdants que gagnants (avec un coup de coude pour exécuter le Integrity Lab et vérifier que l'avantage est réel) ;
  - un certificat de santé propre quand aucun des précédents ne s'applique.

```http
GET /api/journal
```

## Pourquoi c'est fiable

L'analyse comportementale est du code de domaine pur déterministe (`Core.Journal`) sans dépendance d'infrastructure — testé en unité pour surconcentration, défaillances répétées, biais perdant, le cas équilibré et le compte vide. Les faits viennent en premier ; le coach IA (Portfolio Digest) est une couche de récit optionnelle en haut, gatée sur la clé API Anthropic, donc le journal fonctionne entièrement sans IA configurée.
