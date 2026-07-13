# Calendrier économique

cMind livre son **propre** calendrier économique — calendrier de sortie, réels, prévisions, révisions et un modèle d'impact basé sur les données — sourcé à partir des **autorités primaires** (banques centrales et agences statistiques nationales), avec **zéro dépendance** envers ForexFactory, FXStreet, Investing.com ou tout agrégateur. Il est correct point-dans-le-temps, maintient ≥10 années d'historique, et est câblé dans le commerce, l'API publique, MCP, cBots, IA, alertes et backtests. C'est un module découplé : il peut être désactivé sans aucun effet sur le cœur du commerce.

> **Statut.** Le cœur du domaine (modèle d'impact, mappage pays→symbole, politique de fenêtre d'actualités, chaînes de révision point-dans-le-temps, gating à deux niveaux) **et** la persistance (schéma Postgres `calendar`, côté lecture/écriture en ajout-seul, connecteur FRED et worker d'ingestion gatée de config) sont implémentés et testés (unité + intégration Testcontainers). L'API REST JWT, les outils MCP et l'interface utilisateur arrivent dans les phases de déploiement suivantes décrites ci-dessous.

## Ce qui rend cela différent

Les plaintes récurrentes contre les calendriers les plus importants sont devenues nos contraintes de conception :

- **Pas de modifications silencieuses de notation d'impact.** Notre notation d'impact est **déterministe, versionnée et auditable**. Chaque changement est une révision enregistrée avec un horodatage — jamais une surcharge silencieuse. Un utilisateur peut voir exactement *pourquoi* un événement est High.
- **Une ancre UTC par événement.** Chaque événement est ancré à un instant UTC unique provenant du calendrier officiel de la source primaire ; le fuseau horaire propre de la source est stocké, et le rendu par utilisateur utilise un fuseau horaire IANA explicite avec DST géré par la base de données des zones — jamais un basculement manuel ±1h.
- **Chaînes de révision complètes, partout.** La valeur originale et chaque révision sont de première classe, exposées de manière identique via les surfaces API, MCP et cBot.
- **≥10 années d'historique, pas de mur.** Plage de navigation sans restriction ; pas de limite de 60 jours, pas de porte d'enregistrement.
- **Point-dans-le-temps par construction.** Chaque fait porte `KnownAt` (quand *nous* l'avons appris) et `EffectiveAt` (l'instant de l'événement). "Comme le calendrier ressemblait à l'instant T" est une requête de première classe, donc une règle d'actualités backtestée se comporte exactement comme live — pas d'anticipation à partir de valeurs révisées en historique.

## Le modèle d'impact

La notation d'impact est une fonction pure déterministe dans `[0, 100]`, bandée en Low / Medium / High / Critical. Ses entrées sont uniquement des données connues au moment du score (pas de fuite future) :

- **Antécédent de série** — un poids de base par classe d'indicateur (une décision de taux dépasse CPI, qui dépasse une enquête mineure).
- **Empreinte de volatilité réalisée** — le retour absolu médian des symboles principalement affectés dans la fenêtre après les sorties passées de cette série : "cette sortie déplace historiquement le prix de cette quantité."
- **Sensibilité à la surprise** — à quel point la surprise absolue (un z-score) a historiquement corrélé avec le mouvement post-sortie.

Le score les mélange avec des poids fixes et marque un `ImpactModelVersion`. Recalculer est une opération explicite enregistrée qui produit une **nouvelle révision** — jamais une mutation — donc le score est toujours reproductible à partir de ses entrées.

## Mappage pays → devise → symbole

La papercut d'intégration d'algo la plus citée est résolue une fois, en tant que fonction pure : un pays se mappe à sa devise (chaque membre de la zone euro s'éventaille en EUR), et une devise se mappe aux symboles de la watchlist la citant sur l'une ou l'autre jambe. Donc **EURUSD est affecté par les événements de l'UE et des États-Unis** ; XAUUSD est exposé à l'USD ; US500 se mappe à l'USD. Cela entraîne le filtre d'actualités, la résolution des symboles affectés et les mathématiques de blackout.

## Politique de fenêtre d'actualités

Un `NewsWindowRule` est `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Une seule implémentation pure partagée répond à "l'instant T est-il à l'intérieur d'un blackout pour le symbole S?" — utilisée par le filtre d'actualités cBot, la pause de copie-trading et la garde de risque IA, afin qu'elles ne puissent jamais diverger. En cas d'incertitude, la réponse de blackout revient à la valeur conservatrice configurée (fail-closed par défaut) afin qu'une lacune de données ne green-light jamais silencieusement le commerce à travers une sortie à fort impact.

## Point-dans-le-temps & révisions

Les réels, les prévisions et les scores d'impact sont **en ajout-seul**. Chaque événement possède une chaîne ordonnée de révisions, monotones dans `KnownAt` :

- `Scheduled` — l'événement a été d'abord programmé (impact avant, pas de réel).
- `Released` — la première impression réelle est arrivée.
- `Revised` — une valeur révisée ultérieure est arrivée.
- `Rescheduled` — la source a déplacé l'instant de sortie (auditable, alertable).
- `Rescored` — la notation d'impact a été recalculée selon une nouvelle version de modèle.

Interroger `as of` un instant passé retourne exactement la révision connue alors — la garantie qui tue l'anticipation dans les règles d'actualités backtestées.

## Prévision / consensus

La médiane de l'enquête des économistes n'est **pas** librement publiée par les sources primaires — c'est la valeur ajoutée propriétaire de l'agrégateur, et nous ne la fabriquons pas. Le schéma d'événement porte une nullable `Forecast` ; un déploiement peut câbler un flux de consensus autorisé via le port optionnel `IForecastProvider` (apporter votre clé, désactivé par défaut). Les valeurs précédentes et les révisions viennent toujours de la source officielle.

## Sources de données

Deux couches découplées, toutes primaires — jamais un agrégateur :

- **Calendrier / timing :** calendrier de sortie FRED ; agences statistiques nationales (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan) ; calendriers de réunion des banques centrales (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Valeurs réelles :** FRED (avec dates de millésime pour révisions et point-dans-le-temps), plus BLS, BEA, Census, ECB SDW, Eurostat et APIs OECD SDMX.

Une source morte dégrade la couverture pour **cette source seule** ; le calendrier continue à servir tout le reste et superficies la lacune en tant que métrique de fraîcheur.

## Limitation de débit & le plan de secours

Les fournisseurs externes publient des limites de débit (FRED autorise ~120 requêtes/minute). Le calendrier est construit de sorte qu'il **ne dépasse jamais une limite du fournisseur**, et de sorte qu'être limité ou coupé ne dégrade jamais les lectures :

- **Limitation proactive.** Le client HTTP de chaque source passe par une porte de limite de débit partagée thread-safe qui espace les requêtes sortantes selon un budget configuré (`App:Calendar:FredRequestsPerMinute`, par défaut 100 — délibérément sous le plafond du fournisseur). Les requêtes sont mises en file d'attente et espacées, jamais en rafale.
- **Honorer `429 Retry-After`.** Si un fournisseur retourne jamais `429 Too Many Requests`, la porte recule la source entière par le refroidissement demandé par le serveur (ou `App:Calendar:RateLimitBackoff`, par défaut 60 secondes) avant l'appel suivant — pas de boucle de nouvelle tentative serrée.
- **Résilience standard.** Chaque client source hérite également du gestionnaire de résilience à l'échelle de l'application (nouvelle tentative avec backoff + gigue, disjoncteur, timeouts), donc les pépites transitoires sont absorbées et une source défaillante persistante est garée (sa couverture devient stale) sans affecter les autres.
- **Le plan de secours — le cache de lecture-passante durable.** Les lectures ne sont **jamais** servies en appelant un fournisseur. Une fois une plage récupérée, elle est persistée en ajout-seul à Postgres et servie à partir de là pour toujours (voir §"Charge à la demande"). Donc même quand une source est limitée en débit ou baisse, le calendrier continue à répondre à partir de données cachées, point-dans-le-temps-correctes ; la plage manquante reste simplement découverte et est retentée lors du prochain cycle d'ingestion. Les réponses de blackout se dégradent en outre de la valeur conservatrice par défaut en cas d'incertitude, donc une lacune de données ne green-light jamais le commerce à travers une sortie.
- **Sondage bon marché.** La récupération conditionnelle (ETag / If-Modified-Since / curseurs de millésime source) et le cache "récupérer une plage une fois, jamais à nouveau" gardent le volume de requête réel bien en dessous de toute limite dans un fonctionnement normal — la porte de limite de débit est un filet de sécurité, pas le chemin commun.

## Activer / Désactiver

Deux niveaux indépendants, exactement comme d'autres fonctionnalités cMind :

- **Tier 1 — basculement de fonctionnalité runtime** (`Feature.EconomicCalendar`) basculé à partir de l'interface utilisateur d'administration des Fonctionnalités ; pas de redéploiement, prend effet en direct.
- **Tier 2 — porte blanche-étiquette dure** (`App:Branding:EnableEconomicCalendar`, par défaut `true`). Un revendeur la définit `false` pour supprimer complètement la fonction ; un opérateur ne peut alors pas la réactiver.

L'état effectif est `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Quand désactivé, l'entrée nav est masquée et `/economic-calendar`, `/api/calendar/**` et les outils de calendrier MCP retournent une `404` propre de fonctionnalité-désactivée — jamais un `500`. L'historique persisté est conservé lors d'un basculement runtime-off afin que la réactivation soit instantanée.

## Phases de déploiement

- **P0 — cœur du domaine** *(implémenté)* : agrégats, objets de valeur, ports, modèle d'impact, mappage pays→symbole, politique de fenêtre d'actualités, gating à deux niveaux, suite d'unité complète.
- **P1 — persistance + une source** *(implémenté)* : schéma EF `calendar` (tables propres, ajout-seul, hot index), lecteur `IEconomicCalendar` read-through avec `asOf` point-dans-le-temps, service de lecture/écriture en ajout-seul idempotent, connecteur FRED derrière un client typé résilient, et worker d'ingestion gatée de config ; tests d'intégration Testcontainers (persistance, PIT, idempotence, blackout).
- **P2 — API REST JWT publique + Interface utilisateur Web** *(implémenté)* : l'API versionnée sécurisée JWT `/api/calendar/v1` — émission de client, échange de jetons et points de terminaison de lecture centraux (events, history, series, surprises, next, blackout, affected-symbols, health) avec application des scopes et gating à deux niveaux, intégration-testés. Plus la page mobile-first **`/economic-calendar`** — un agenda gatée, entièrement localisée (23 langues) des sorties à venir sous forme de cartes adaptées aux téléphones avec des chips d'impact bande-couleur et une boîte de dialogue de filtrage MudBlazor (devises + impact minimum + un sélecteur **From-date** pour sauter à **toute** date passée sur l'historique complet — pas de limite de 60 jours, pas de mur) ; entrée nav, smoke/mobile/a11y/E2E testée. Une page d'historique de **série par indicateur** (`/economic-calendar/series/{code}`, liée depuis chaque événement) liste l'historique d'impression complet d'une série. Les graphiques de surprise + navigateur de scroll infini suivent.
- **P3 — plus de sources & préchauffage** *(commencé)* : un **catalogue de séries centrales** (CPI, Core CPI, NFP, chômage, PIB, PCE, taux des fonds fédéraux, ventes de détail → leurs identifiants FRED) est amorcé automatiquement au démarrage, et un **remplissage proactif** idempotent, unique et par année, récupère leur historique ≥10-ans afin que le cas commun soit préchauffé sans attendre une absence utilisateur. **L'ingestion est activée par défaut** (`App:Calendar:IngestionEnabled`, par défaut `true`) : la **source du calendrier des banques centrales** ne nécessite **aucune clé API**, donc le calendrier des décisions FOMC / ECB / BoE se remplit prêt à l'emploi — le remplissage amorce ces dates de réunion sur **l'historique récent et l'horizon prospective**, donc naviguer *le mois dernier* (ou toute fenêtre passée) montre les réunions même avant que toute clé FRED/BLS soit configurée ; la série de valeurs se remplit une fois leurs clés définies. Les workers honorent la porte à deux niveaux du calendrier — un déploiement white-label ou la désactivation par le propriétaire de la fonction calendrier économique arrête l'ingestion, et `App:Calendar:IngestionEnabled=false` la désactive explicitement. **Fraîcheur par source** est maintenant réelle aussi : le worker enregistre le dernier sondage réussi de chaque source, le nombre de défaillances consécutives et un drapeau de circuit déclenché (persisté dans les paramètres d'application, inter-processus), et le point de terminaison `/health` + outil MCP `calendar_health` signalent un verdict `stale` véridique par source. **BLS** (une 2e source de valeur) et la **source du calendrier des banques centrales** (dates de décision FOMC / ECB / BoE, remplies en historique et synchronisées en avant dans une fenêtre d'horizon par le worker) sont activées. À venir : sources de valeur BEA/Census/ECB-SDW/Eurostat/OECD et la passe de réconciliation.
- **P4 — intégration profonde** : **Outils MCP** *(implémentés — parité API de lecture complète : `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gatés sur la fonction)* et le **déclencheur `EconomicEvent` d'alertes** *(implémenté — une `AlertRule` qui se déclenche N minutes avant une sortie à venir au/au-dessus d'un impact choisi, opérationnellement rétrécir aux devises ; évalué par le worker d'alertes existant sans IA, dédupliqué par sortie ; créé via `POST /api/alerts/rules/economic-event`)*. La porte de blackout d'actualités de prop-guard **et la pause de blackout de copie-trading** sont activées (§5.1 — un opt-in `App:Copy:NewsPauseEnabled`, par défaut désactivé : une source ouverte dont le symbole s'assit dans un blackout Critical-impact est sauté, chemin chaud byte-identique quand désactivé). La **surcouche d'événement de backtest** est activée — `GET /api/calendar/v1/for-symbol` et l'outil MCP `calendar_events_for_symbol` retournent les événements point-dans-le-temps-corrects affectant un symbole dans une fenêtre, et la **page de rapport d'instance/backtest** rend les sorties à fort impact qui sont tombées dans la fenêtre du backtest sous la courbe d'équité (afin qu'un auteur voie les transactions qui ont atterri sur NFP), gatées et localisées. Le plan entier est maintenant implémenté.
- **P5 — extras** : analytique de surprise, export iCal/CSV, recherche par mots-clés, consensus enfichable.

Voir la [référence API cBot & REST](calendar-cbot-api.md) pour la surface d'intégration.
