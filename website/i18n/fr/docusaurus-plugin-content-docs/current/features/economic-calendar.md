# Calendrier économique

cMind livre son **propre** calendrier économique — calendrier des publications, actuals, prévisions, révisions
et modèle d'impact — sourcé depuis les **autorités primaires** (banques centrales et agences statistiques
nationales), avec **zéro dépendance** envers ForexFactory, FXStreet, Investing.com ou tout agrégateur. Il est
correct point-in-time, garde ≥10 ans d'historique, et est câblé dans le trading, l'API publique, MCP, les
cBots, l'IA, les alertes et les backtests. C'est un module découplé : il peut être désactivé sans effet sur
le cœur trading.

> **Statut.** P0–P4 sont implémentés et livrés. Le cœur domaine, la persistance (schéma EF `calendar`,
> lecture/écriture append-only, sources FRED + BLS + calendrier de banque centrale, worker d'ingestion
> gated par config avec suivi de fraîcheur par source), l'API REST JWT versionnée, l'UI mobile-first
> `/economic-calendar`, les outils MCP, l'API JWT cBot, les alertes d'événements à fort impact,
> la pause copy-trade en blackout de news, l'overlay d'événements backtest, le flux SSE, les webhooks
> signés HMAC et le `CmindCalendarClient` typé sont tous implémentés et testés en intégration. Les
> extras P5 (analyse des surprises, export iCal/CSV, recherche par mots-clés, consensus pluggable)
> sont les éléments restants — voir les phases de déploiement ci-dessous.

## Ce qui le différencie

Les plaintes récurrentes contre les calendriers leaders sont devenues nos contraintes de conception :

- **Pas de changement silencieux de notation d'impact.** Notre notation d'impact est **déterministe,
  versionnée et auditée**. Chaque changement est une révision enregistrée avec un horodatage — jamais un
  écrasement silencieux. Un utilisateur peut voir exactement *pourquoi* un événement est High.
- **Un ancrage UTC par événement.** Chaque événement est ancré à un instant UTC unique depuis le calendrier
  officiel de la source primaire ; le propre fuseau horaire de la source est stocké, et le rendu par
  utilisateur utilise un fuseau IANA explicite avec le DST géré par la base de zones — jamais un toggle
  manuel ±1h.
- **Chaînes de révision complètes, partout.** La valeur originale et chaque révision sont des citoyennes
  de première classe, exposées identiquement via l'API, MCP et les surfaces cBot.
- **≥10 ans d'historique, sans mur.** Plage de navigation illimitée ; pas de capot 60 jours, pas de porte
  d'enregistrement.
- **Point-in-time par construction.** Chaque fait porte `KnownAt` (quand *nous* l'avons appris) et
  `EffectiveAt` (l'instant de l'événement). « Telle que le calendrier se présentait au temps T » est une
  requête de première classe, ainsi une règle news backtestée se comporte exactement comme en live — pas
  d'anticipation de l'utilisation de valeurs révisées dans l'historique.

## Le modèle d'impact

Le score d'impact est une fonction pure et déterministe dans `[0, 100]`, bandée en Low / Medium / High /
Critical. Ses entrées sont uniquement des données connues au moment du scoring (pas de fuite future) :

- **Série prior** — un poids de base par classe d'indicateur (une décision de taux l'emporte sur le CPI, qui
  l'emporte sur une enquête mineure).
- **Empreinte de volatilité réalisée** — la médiane du retour absolu des symboles primaires affectés dans la
  fenêtre après les *anciennes* publications de cette série : « cette publication déplace historiquement le prix
  de cette ampleur ».
- **Sensibilité à la surprise** — avec quelle force la surprise absolue (un z-score) a historiquement corrélé
  avec le mouvement post-publication.

Le score mélange ces éléments avec des poids fixes et estampille un `ImpactModelVersion`. Le recompute est une
opération explicite et loguée qui produit une **nouvelle révision** — jamais une mutation — ainsi le score est
toujours reproductible depuis ses entrées.

## Mapping pays → devise → symbole

La critique d'intégration algo la plus citée est résolue une fois pour toutes, comme une fonction pure : un
pays map vers sa devise (chaque membre de la zone euro se ramifie vers EUR), et une devise map vers les symboles
de watchlist qui la cotent sur l'une ou l'autre jambe. Ainsi **EURUSD est affecté par les événements EU et US** ;
XAUUSD est exposé à l'USD ; US500 map vers l'USD. Cela pilote le filtre news, la résolution des symboles
affectés et les maths de blackout.

## Politique de fenêtre de news

Un `NewsWindowRule` est `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Une implémentation
unique, partagée et pure répond « l'instant T est-il à l'intérieur d'un blackout pour le symbole S ? » —
utilisée par le filtre news cBot, la pause copy-trade et la garde-barre risque IA, ainsi ils ne peuvent jamais
diverger. En cas d'incertitude, la réponse de blackout est par défaut la valeur conservative configurée
(fail-closed par défaut) ainsi un gap de données ne met jamais silencieusement en vert le trading à travers
une publication à fort impact.

## Point-in-time et révisions

Les actuals, prévisions et scores d'impact sont **append-only**. Chaque événement possède une chaîne ordonnée
de révisions, monotone en `KnownAt` :

- `Scheduled` — l'événement a été впервые planifié (impact prior, pas d'actual).
- `Released` — le premier actual imprimé est arrivé.
- `Revised` — une valeur révisée ultérieure est arrivée.
- `Rescheduled` — la source a déplacé l'instant de publication (auditée, peut déclencher des alertes).
- `Rescored` — le score d'impact a été recalculé sous une nouvelle version du modèle.

Interroger `as of` un instant passé retourne exactement la révision connue alors — la garantie qui élimine
l'anticipation dans les règles news backtestées.

## Prévision / consensus

La médiane du consensus des économistes **n'est pas** librement publiée par les sources primaires — c'est la
valeur ajoutée propriétaire des agrégateurs, et nous ne la fabriquons pas. Le schéma d'événement porte un
`Forecast` nullable ; un déploiement peut câbler un flux de consensus sous licence à travers le port optionnel
`IForecastProvider` (apportez votre propre clé, désactivé par défaut). Les valeurs précédentes et révisions
proviennent toujours de la source officielle.

## Sources de données

Deux couches découplées, toutes primaires — jamais un agrégateur :

- **Calendrier / timing :** calendrier de publication FRED ; agences statistiques nationales (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan) ; calendriers des réunions des banques centrales
  (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Valeurs actuelles :** FRED (avec dates de vintage pour les révisions et point-in-time), plus les APIs
  BLS, BEA, Census, ECB SDW, Eurostat et OECD SDMX.

Une source morte dégrade la couverture **pour cette source uniquement** ; le calendrier continue à servir
tout le reste et expose le gap comme métrique de fraîcheur.

## Limitation de débit et plan de secours

Les fournisseurs externes publient des limites de débit (FRED permet ~120 requêtes/minute). Le calendrier
est construit pour **ne jamais déclencher la limite d'un fournisseur**, et pour que le fait d'être limité
ou coupé ne dégrade jamais les lectures :

- **Limitateur de débit proactif.** Chaque client HTTP d'une source passe par un limitateur partagé,
  thread-safe qui espace les requêtes sortantes selon un budget configuré
  (`App:Calendar:FredRequestsPerMinute`, par défaut 100 — délibérément sous le plafond du fournisseur).
  Les requêtes sont mises en file et cadencées, jamais en burst.
- **Honorer `429 Retry-After`.** Si un fournisseur retourne `429 Too Many Requests`, le limitateur repousse
  toute la source du cooldown demandé par le serveur (ou `App:Calendar:RateLimitBackoff`, par défaut 60s)
  avant le prochain appel — pas de boucle de retry serrée.
- **Résilience standard.** Chaque client source hérite aussi du handler de résilience global de l'app
  (retry avec backoff + jitter, disjoncteur, timeouts), ainsi les à-coups transitoires sont absorbés et une
  source qui échoue de façon persistante est mise en pause (sa couverture devient stale) sans affecter les
  autres.
- **Le plan de secours — le cache read-through durable.** Les lectures ne sont **jamais** servies en
  appelant un fournisseur. Une fois une plage fetchée, elle est persistée append-only dans Postgres et
  servie de là pour toujours (voir §« Chargement à la demande »). Ainsi même quand une source est limitée
  ou hors ligne, le calendrier continue de répondre depuis des données cached et correctes point-in-time ;
  la span manquante reste simplement non couverte et sera retentée au prochain cycle d'ingestion. Les
  réponses de blackout échouent en plus vers la valeur par défaut conservative sous incertitude, ainsi un
  gap de données ne met jamais en vert le trading à travers une publication.
- **Polling bon marché.** Fetch conditionnel (ETag / If-Modified-Since / curseurs de vintage source) et le
  cache « fetch une span une fois, jamais à nouveau » gardent le volume réel de requêtes bien en dessous
  de toute limite en opération normale — le limitateur de débit est un filet de sécurité, pas le chemin
  commun.

## Activation / désactivation

Deux niveaux indépendants, exactement comme les autres fonctionnalités cMind :

- **Niveau 1 — toggle de fonctionnalité runtime** (`Feature.EconomicCalendar`) basculé depuis l'UI admin
  Features ; pas de redeploy, prend effet live.
- **Niveau 2 — gate hard white-label** (`App:Branding:EnableEconomicCalendar`, par défaut `true`). Un
  revendeur le met à `false` pour supprimer entièrement la fonctionnalité ; un opérateur ne peut alors pas
  le ré-activer.

L'état effectif est `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Quand désactivé,
l'entrée nav est cachée et `/economic-calendar`, `/api/calendar/**` et les outils MCP calendrier retournent
un 404 propre feature-disabled — jamais un 500. L'historique persisté est conservé lors d'un toggle-off
runtime ainsi le ré-activation est instantané.

## Phases de rollout

- **P0 — cœur domaine** *(implémenté)* : agrégats, value objects, ports, modèle d'impact,
  mapping pays→symbole, politique de fenêtre de news, gating deux-niveaux, suite unit complète.
- **P1 — persistance + une source** *(implémenté)* : schéma EF `calendar` (ses propres tables, append-only,
  indexes hot), le reader `IEconomicCalendar` read-through avec point-in-time `asOf`, le service d'écriture
  idempotent append-only, le connecteur FRED derrière un client typé résilient, et le worker d'ingestion
  gated par config ; tests d'intégration Testcontainers (persistance, PIT, idempotence, blackout).
- **P2 — API REST JWT publique + UI Web** *(implémenté)* : l'API `/api/calendar/v1` versionnée et sécurisée
  par JWT — émission de client, échange de token et endpoints de lecture core (events, history, series,
  surprises, next, blackout, affected-symbols, health) avec application des scopes et gating deux-niveaux,
  testée en intégration. Plus la **page `/economic-calendar` mobile-first** — agenda des publications à venir
  en cartes phone-friendly avec chips d'impact colorés par bande et un **dialogue filtre MudBlazor**
  (devises + impact minimum + un **sélecteur From-date** pour sauter vers **n'importe quelle** date passée
  à travers l'historique complet — pas de capot 60 jours, pas de mur) ; entrée nav, testée smoke/mobile/a11y/E2E.
  Une **page d'historique de série par indicateur** (`/economic-calendar/series/{code}`, liée depuis chaque
  événement) liste l'historique d'impression complet d'une série. Les graphiques de surprise et le
  navigateur à scroll infini suivent.
- **P3 — plus de sources & warm-up** *(commencé)* : un **catalogue de séries core** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → leurs IDs FRED) est ensemencé automatiquement au
  démarrage, et un **backfill proactif one-time et idempotent** par chunks d'année tire leur historiquegénérique
  de ≥10 ans ainsi le cas commun est warm sans attendre qu'un utilisateur manque. **L'ingestion est activée
  par défaut** (`App:Calendar:IngestionEnabled`, par défaut `true`) : la **source calendrier des banques
  centrales** n'a **pas besoin de clé API**, ainsi le calendrier des décisions FOMC / ECB / BoE se
  popule out-of-the-box — le backfill ensemence ces dates de réunion à travers **l'historique récent et
  l'horizon forward**, ainsi naviguer *le mois dernier* (ou n'importe quelle fenêtre passée) montre les
  réunions même avant qu'une clé FRED/BLS soit configurée ; les séries de valeurs se remplissent une fois
  leurs clés configurées. Les workers honorent le gating deux-niveaux du calendrier — un déploiement
  white-label ou le propriétaire désactivant la fonctionnalité calendrier économique arrête l'ingestion,
  et `App:Calendar:IngestionEnabled=false` l'arrête explicitement. **La fraîcheur par source** est aussi
  réelle : le worker enregistre le dernier poll réussi de chaque source, le compte d'échecs consécutifs et
  un flag de disjoncteur déclenché (persistés dans les app settings, cross-process), et l'endpoint `/health`
  + l'outil MCP `calendar_health` rapportent un verdict `stale` véridique par source. **BLS** (une 2e source
  de valeurs) et la **source calendrier banque centrale** (dates de décision FOMC / ECB / BoE, backfillé
  à travers l'historique et sync forward dans une fenêtre d'horizon par le worker) sont en place. Encore
  à venir : sources de valeurs BEA/Census/ECB-SDW/Eurostat/OECD et le passage de réconciliation.
- **P4 — intégration profonde** *(implémenté — parité complète de l'API lecture MCP : `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated sur la fonctionnalité)* et le
  **trigger d'alerte `EconomicEvent`** *(implémenté — un `AlertRule` qui se déclenche N minutes avant une
  publication à venir à/au-dessus d'un impact choisi, optionnellement réduit aux devises ; évalué par le
  worker d'alertes existant sans IA, dédupliqué par publication ; créé via
  `POST /api/alerts/rules/economic-event`)*. La **gate de blackout news prop-guard** et la **pause
  copy-trade blackout** sont en place (§5.1 — un opt-in `App:Copy:NewsPauseEnabled`, par défaut off : une
  source ouverte dont le symbole est dans un blackout d'impact Critical est sautée, chemin hot byte-identique
  quand off). Le **backtest event overlay** est en place — `GET /api/calendar/v1/for-symbol` et l'outil
  MCP `calendar_events_for_symbol` retournent les événements point-in-time-corrects affectant un symbole
  dans une fenêtre, et la **page de rapport instance/backtest** rend les publications à fort impact tombées
  à l'intérieur de la fenêtre de backtest sous la courbe d'équité (ainsi un auteur voit quels trades ont
  atterri sur NFP), gated et localisé. Le plan entier est maintenant implémenté.
- **P5 — extras** : analytique de surprise, export iCal/CSV, recherche par mot-clé, consensus enfichable.

Voir la [référence API REST cBot &](calendar-cbot-api.md) pour la surface d'intégration.

## Une source de données est requise (la fonctionnalité est cachée sans)

Le calendrier expose les valeurs actual/forecast/previous uniquement depuis une source de valeurs configurée
(FRED ou BLS). Sans `App:Calendar:FredApiKey` ou `App:Calendar:BlsApiKey`, la fonctionnalité est **cachée**
de la navigation ; si elle est forcée (white-label/propriétaire) sans clé, la page montre un message
exploitable « configurez une source de données » au lieu de valeurs vides, et l'action de filtre reste
cachée jusqu'à ce qu'une source soit configurée. Les lignes d'événement affichent le **nom** de la série
(depuis le catalogue), pas le code de série brut.
