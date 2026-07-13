---
description: "Les prop-firms au détail (style FTMO) vendent des comptes d'évaluation : le trader doit atteindre l'objectif de profit en restant dans les limites de risque (perte quotidienne max, tirage max…"
---

# Simulation de défi prop-firm

Les prop-firms au détail (style FTMO) vendent des **comptes d'évaluation** : le trader doit atteindre l'objectif de profit en
restant dans les limites de risque (perte quotidienne max, tirage total/traîné max, cohérence, limites de temps) avant
le financement. cMind permet à l'utilisateur de créer un **défi personnalisé de n'importe quelle forme de l'industrie**, le lier à
`TradingAccount`, **l'exécuter comme une opération de copie commerciale** — démarré/arrêté, hébergé sur un nœud,
suivi **en direct via l'API cTrader Open API**. L'agrégat évalue chaque règle de façon déterministe ; en
réussite ou violation, termine le défi, le marque, alerte l'utilisateur.

## Domaine (contexte borné : PropFirm)

`PropFirmChallenge` = racine d'agrégat (module `Core.PropFirm`), référence son `TradingAccount` par
id fort uniquement (pas de FK cross-agrégat). Possède l'évaluation de règles, la machine d'état/de phase, le bail de
nœud.

### Objets de valeur & ensemble de règles

- **`Money`** (non-négative), **`MoneyAmount`** (signée), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — la lecture fournie à l'agrégat.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — faits non-équité.
- **`DailyLossLimit`** `(percent, basis)` — base `Equity` (intraday, inclut P&L flottant) ou `Balance`
  (réalisé uniquement).
- **`DrawdownLimit`** — `Static` (à partir du solde de démarrage), `TrailingPercent` (à partir du pic d'équité), ou
  `TrailingThresholdDollar` (traîne le pic d'équité par montant fixe en dollars, puis **verrouille au solde de
  démarrage** une fois que l'équité atteint le seuil — style futures).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — bloque la réussite tandis qu'un jour domine le profit total.
- **`ChallengeRules`** porte ci-dessus plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Les mathématiques de règles vivent sur les VO
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`) ; l'agrégat
  orchestre.

### Types de défi & modèles

`ChallengeTemplates.For(kind)` construit un preset valide pour `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, ou `Custom` (contrôle complet). L'UI pré-remplit le modèle ; l'utilisateur peut ajuster n'importe quel champ.

### Phases & statut

- **Phases :** `Evaluation → Verification → Funded` (une seule étape saute Verification).
- **Statut :** `Active`, `Passed`, `Failed`, plus cycle de vie `Stopped` (suivi pausé) — `Create` démarre
  le défi `Active` ; `Stop()`/`Resume()` basculent `Active↔Stopped`.
- **`BreachReason` :** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Évaluation des règles

- **`RecordEquity(EquitySnapshot, now)` — roule le jour de trading aux limites du jour (capture le profit du jour
  précédent pour la règle de cohérence), met à jour les pics/pics du jour, puis **échoue à la première violation**
  (perte quotidienne → tirage → limite de temps → inactivité, dans l'ordre) ou avance de phase quand l'objectif de profit,
  le jour de trading minimum, les exigences de cohérence sont tous remplis. Les snapshots hors ordre et les enregistrements sur
  le défi terminal lèvent `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)` — évalue les règles de comportement (max de positions ouvertes, maintien du
  weekend, commerce de nouvelles), horodatage l'activité pour la règle d'inactivité.
- Soft **`PropFirmDrawdownWarning`** se déclenche une fois quand l'utilisation d'équité franchit le seuil configurable.

Événements de domaine : `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Suivi en direct (Exécution) — hébergé sur nœud, auto-guérison

Le suivi reflète exactement la pile d'hébergement du copie commerciale ; suivi prop = cousin **en lecture seule** du
moteur de copie.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` sur chaque nœud, contrôlé sur
  `App:PropFirm:Enabled`. Chaque cycle **demande** les défis actifs sur le bail d'auto-guérison
  (`AssignedNode` + `LeaseExpiresAt` ; les défis du nœud mort reclamés une fois que le bail expire —
  même revendication `ExecuteUpdate` atomique que le copie commerciale, afin que deux nœuds ne font jamais double-suivi), renouvellent les baux,
  poussent les tokens tournés en place, arrêtent les hôtes dont le défi a quitté `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — un par défi. Ouvre `IOpenApiTradingSession`
  pour le compte et, sur `App:PropFirm:EquityPollInterval`, recalcule l'équité en direct, alimente le
  l'agrégat. Échange le token d'accès en place lors de la rotation (pas de baisse de session). Sort quand le défi
  n'est plus `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — maths d'équité fidèle cTrader.
  L'équité **n'est pas** livrée par l'API Open, donc dérivée : `équité = solde + Σ(P&L non réalisé)`,
  où le P&L de chaque position est `priceDifference × units × taux quote→dépôt + swap + commission`
  (`units = volume câblé / 100` ; long réévalue à l'offre, court à demander). Solde de
  `ProtoOATrader` ; positions (prix d'entrée, swap, commission) de réconcilie ; offre/demande en direct du spot
  souscriptions. Pur et isolé — conversion de devises point chaud testé unitaire de sa propre volonté.

## Alertes

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) s'abonne aux événements de domaine de réussite/violation/avertissement
(enregistré en tant que `IDomainEventHandler<>`, distribué après le `SaveChanges` réussi), notifie l'utilisateur
via piste d'audit d'alerte/structurée (`LogMessages`). L'UI en direct reflète le même changement de statut. Ceci
= réaction entre contextes — n'a jamais muté l'agrégat de défi.

## API (`/api/prop-firm`, feature `PropFirm`, rôle User+)

| Méthode | Route | Objectif |
|--------|-------|---------|
| GET | `/challenges` | lister les défis de l'utilisateur (genre, phase, statut, équité en direct, bail) |
| GET | `/challenges/{id}` | un défi |
| GET | `/templates` | presets de l'industrie pour le dialogue de création |
| POST | `/challenges` | créer à partir du modèle **ou** ensemble de règles entièrement personnalisé |
| POST | `/challenges/{id}/start` | reprendre le suivi (Stopped → Active) |
| POST | `/challenges/{id}/stop` | arrêter le suivi (Active → Stopped, libérer le bail) |
| POST | `/challenges/{id}/equity` | enregistrer snapshot d'équité → réévaluer (chemin manuel/no-live-feed) |
| DELETE | `/challenges/{id}` | soft-delete (bloqué pendant Active) |

MCP : `Mcp/Tools/PropFirmTools.cs` expose list/create(from template)/record-equity/start/stop, contrôlé sur
la feature `PropFirm`.

UI : `/prop-firm` (nav *Prop Firm*, contrôlé par l'indicateur `PropFirm`) liste les défis avec les actions de rangée **Start/Stop/Delete** (Start quand Stopped, Stop quand Active, Delete désactivé pendant Active), les crée via
`NewPropFirmChallengeDialog` (sélecteur de modèle + éditeur de règles complet). Tous les créer/éditer via boîte de dialogue MudBlazor.

## Flux d'équité en direct — résolu

L'écart antérieur « pas de flux P&L du compte en direct » fermé : quand `App:PropFirm:Enabled` défini, les nœuds suivent
le compte en direct sur l'API Open, flux d'équité automatiquement. Sans elle (par défaut), le domaine et le
chemin **d'équité manuelle** (`POST …/equity`) s'exécutent inchangés — pas d'identifiants cTrader nécessaires pour la construction/test/E2E.

## Tests

- **Unit** — `UnitTests/PropFirm/` : `PropFirmChallengeTests` (avancement de phase, jours min, tirage statique/traîné,
  perte quotidienne, gardes terminal/hors-ordre) ; `PropFirmChallengeRulesTests` (base de perte quotidienne solde vs
  équité, traîné de seuil en dollars traîné+verrouillage, bloc de cohérence/permettre, limite de temps, inactivité,
  exposition max, weekend, nouvelles, arrêt/reprise, limite de bail, pass libère le bail, avertissement de tirage) ;
  `PropFirmValueObjectTests` (plages VO + maths de VO de règles) ; `PropFirmEquityCalculatorTests` (P&L long/court,
  swap/commission, conversion quote→dépôt, tarification manquante) ; `PropFirmTrackingHostTests` (l'équité en direct
  conduit pass/fail contre la session fausse étendue) ; `PropFirmAlertNotifierTests`. Temps explicite /
  `FakeTimeProvider` — pas de lectures d'horloge murale.
- **Integration** — `IntegrationTests/` : `PropFirmChallengePersistenceTests` (aller-retour + record-équité +
  soft-delete, règles enrichies + aller-retour de bail) et `PropFirmTrackingLeaseTests` (demande, bail contesté,
  reclaim après expiration sur deux identités de nœud) sur Postgres réel.
- **E2E** — `E2ETests/PropFirmTests.cs` : créer + enregistrer-équité à `Passed` ; arrêt→démarrage→flux de violation ;
  endpoint des modèles.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs` : flux d'équité/activité aléatoires ensemencés
  (rouleaux du jour, pics, crashes, snapshots dupliqués + hors-ordre) sur de nombreux défis à règles mixtes, affirmant
  les états finaux sticky exactement une fois, invariant de limites de pic-courant, échecs raisonnés.

## Configuration (`App:PropFirm`)

`Enabled` (désactivé par défaut), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
