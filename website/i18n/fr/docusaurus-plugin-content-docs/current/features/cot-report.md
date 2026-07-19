# Engagement des Commerçants (COT)

cMind inclut un rapport **Engagement des Commerçants** intégré — la ventilation hebdomadaire de la CFTC pour savoir qui est long et court sur le marché à terme américain (couvreurs commerciaux, grands spéculateurs, fonds), avec des graphiques historiques interactifs, un **indice COT** normalisé, une API REST authentifiée pour les cBots et des outils MCP pour les clients IA. Les données proviennent directement des **ensembles de données Socrata publics de la CFTC** — sans clé API, sans agrégateur. Comme le calendrier économique, c'est un module découplé qui peut être désactivé sans aucun effet sur le noyau commercial.

## Ce qu'il vous offre

- **Les trois familles de rapports, contrats uniquement et contrats + options combinés :**
  - **Héritage** — Non Commercial (grands spéculateurs), Commercial (couvreurs), Non Signalé.
  - **Désagrégé** — Producteur/Commerçant, Courtiers de Swaps, Gestion d'Actifs, Autres Signalés.
  - **Commerçants en Contrats Financiers (TFF)** — Courtier, Gestionnaire d'Actifs, Fonds à Effet de Levier, Autres Signalés.
- **Un catalogue de marchés curé** — Paires de devises majeures, or/argent/cuivre, pétrole brut et gaz naturel, Obligations d'État, indices boursiers, cryptos et les principaux grains/matières premières — chacun mappé à son code de contrat CFTC stable et, le cas échéant, à un symbole négociable (par exemple Euro FX → `EURUSD`, Or → `XAUUSD`).
- **L'indice COT (0–100)** — où se situe la position nette actuelle du spéculateur dans sa plage historique (par défaut ~3 ans de rétrospective). Les lectures près des extrêmes signalent un positionnement encombré qui précède souvent une inversion ; le rapport marque un **extrême long** (≥80) ou un **extrême court** (≤20).
- **Exactitude ponctuelle.** Un rapport hebdomadaire est mesuré un mardi mais ne devient public que le vendredi suivant ; chaque lecture honore cet instant de libération, de sorte qu'un signal de positionnement backtesté ne voit jamais un rapport avant qu'il ne soit publié (pas d'anticipation).

## Utilisation de la page

Ouvrez **Engagement des Commerçants** à partir de la navigation de gauche. Choisissez un **marché**, un **type de rapport** (Héritage / Désagrégé / Financier) et activez **Contrats + options** pour basculer entre contrats uniquement et la variante combinée. La page affiche :

- **Positionnement net au fil du temps** — un graphique en lignes interactif de la position nette (long − court) de chaque catégorie de commerçants dans la fenêtre d'historique.
- **Indice COT** — un graphique en lignes de l'indice 0–100, avec la lecture la plus récente et son étiquette extrême.
- **Dernier instantané** — un tableau long / court / net / % d'intérêt ouvert par catégorie de commerçant, plus intérêt ouvert total et date du rapport.

## Comment les données circulent

Un travailleur d'ingestion hebdomadaire extrait les six ensembles de données CFTC pour les marchés suivis, met à jour le catalogue de marchés et ajoute chaque nouveau rapport **de manière idempotente** (réexécuter ne duplique jamais un instantané). La première exécution remplit plusieurs années d'historique ; les exécutions ultérieures resynchronisent les semaines les plus récentes pour détecter les révisions tardives. Tout fonctionne clé en main sans clé ; un jeton d'application Socrata optionnel n'augmente que la limite de débit.

## Configuration

Toutes les clés sont sous `App:Cot` (voir [bascules de fonctionnalités](./feature-toggles.md) et [paramètres propriétaire étiquette blanche](./white-label-owner-settings.md)) :

| Clé | Défaut | Objectif |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Indique si le travailleur d'ingestion hebdomadaire s'exécute. |
| `PollInterval` | `6h` | Fréquence à laquelle le travailleur sonde les ensembles de données CFTC. |
| `BackfillYears` | `5` | Années d'historique extraites lors de la première exécution. |
| `ReconcileLookbackWeeks` | `4` | Semaines récentes resynchronisées chaque cycle pour détecter les révisions. |
| `SocrataAppToken` | — | Jeton optionnel qui augmente la limite de débit anonyme. |
| `CotIndexLookbackWeeks` | `156` | Rapports hebdomadaires utilisés comme plage d'indice COT (~3 ans). |

## Portail

La visibilité est une porte à deux niveaux, identique au calendrier économique : la porte stricte étiquette blanche `App:Branding:EnableCot` (niveau compilation) **et** la bascule de fonctionnalité runtime `App:Features:Cot`. Avec l'une d'elles désactivée, le lien de navigation, la page, l'API REST et les outils MCP disparaissent tous (l'API retourne `404`). Comme la source de données n'a pas de clé, il n'y a pas de porte de clé de source de données — activé signifie visible.

## Pour les développeurs

- Domaine : `Core.Cot` — agrégats `CotMarket` et `CotReport`, objet valeur `CotPositions`, service de domaine `CotIndexCalculator`, et ports `ICotReports` / `ICotSource`.
- Infrastructure : `Infrastructure.Cot` — analyseur anti-corruption `CftcSocrataSource`, porte de débit, service d'écriture en ajout seul, côté lecture et travailleur d'ingestion hebdomadaire (schéma EF `cot`).
- Accès cBot & IA : l'[API cBot COT](./cot-cbot-api.md) (REST, JWT `market:read`) et les outils MCP `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
