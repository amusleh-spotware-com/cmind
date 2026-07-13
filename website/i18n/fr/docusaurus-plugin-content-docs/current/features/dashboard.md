---
title: Dashboard
description: Le dashboard cMind — un centre de commande mobile-first et live pour vos runs cBot, backtests, ressources et cluster de nœuds.
---

# Dashboard

La première chose que vous voyez en vous connectant, et honnêtement la page que vous laissez ouverte toute la journée.
La landing page (`/`, `Components/Pages/Index.razor`) est un **centre de commande mobile-first et live** pour
l'activité de l'utilisateur connecté à travers les runs cBot, backtests, ressources et (pour les admins) le
cluster de nœuds. Elle se rafraîchit elle-même, est belle sur mobile, et ne vous demande jamais de faire F5.

## Ce qu'elle affiche

De haut en bas, priorisé pour un téléphone (chaque bloc est un élément stack full-width sur mobile,
une grille responsive sur tablette/desktop) :

1. **Header** — titre, un indicateur live (un vrai point pulsant ; statique sous `prefers-reduced-motion`),
   l'heure du dernier rafraîchissement, et un **toggle de période** (`1H · 24H · 7D · 30D`) qui pilote
   les KPIs et le graphique.
2. **KPIs Hero** — quatre cartes d'un coup d'œil, chacune avec un gros chiffre + un sparkline SVG inline,
   et (là où c'est pertinent) un **delta vs la période précédente** :
   - **Active now** — runs + backtests en cours de démarrage/exécution.
   - **Taux de réussite** — complétés ÷ (complétés + échoués) sur la période ; delta en points de pourcentage.
   - **Complétés** — runs/backtests terminés cette période ; delta vs période précédente.
   - **Échoués** — échecs cette période ; delta (moins c'est mieux, donc une baisse montre du vert).
3. **Graphique d'activité** — timeline area ApexCharts de démarrés / complétés / échoués par bucket de temps.
4. **Anneau de statut des instances** — donut de running / backtests / pending / completed / failed, total
   au centre.
5. **Backtests** — snapshot trois-tuiles (running / completed / failed), clic vers `/backtest`.
6. **Copy trading** — vos profils de copy-trading avec un point de statut live, nombre de destinations,
   et un badge **Live** sur les profils en cours ; clic vers `/copy-trading`.
7. **Agents IA** — vos agents de trading pilotés par persona avec état d'exécution (archétype · statut)
   et heure de dernière action ; clic vers `/agent-studio`.
8. **Flux d'activité live** — les 20 événements les plus récents (plus récent en premier) avec un point
   coloré par statut et un horodatage relatif.
9. **Santé du cluster** (admins uniquement) — nœuds actifs vs total et jauge de capacité utilisée.
10. **Tuiles de ressources** — cBots, comptes de trading, cTrader IDs, clés MCP (clic vers leurs pages).

## Personnalisez votre dashboard

Chaque bloc ci-dessus est un **widget que vous contrôlez**. Cliquez sur **Customize** (en haut à droite du
header) pour ouvrir un dialogue où vous **affichez/cachez** n'importe quel widget et **réorganisez**-les avec
les flèches haut/bas. **Réinitialiser par défaut** restaure l'ordre du catalogue. Votre choix est **persistant
coté serveur par utilisateur**, ainsi il vous suit à travers navigateurs et appareils — pas juste cet onglet.

- Les widgets gated par fonctionnalité et admin-only (Copy trading, Agents IA, Santé du cluster) n'apparaissent
  dans le dialogue que quand votre déploiement/rôle peut les utiliser.
- Le catalogue de widgets est une seule source de vérité dans `Core/Dashboard/DashboardWidgets.cs` ; la
  présentation (label + icône + disponibilité) vit dans `Components/Dashboard/DashboardWidgetMeta.cs`.

## Comment il reste live

La page interroge `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` toutes les 10 secondes et ré-affiche
les widgets sur place — pas de rechargement manuel. Un échec de fetch transitoire est ignoré et retenté au
prochain tick ; la boucle s'arrête proprement sur dispose. Le premier chargement montre un skeleton ; un
échec persistant montre une carte d'erreur avec **Réessayer** ; un utilisateur sans donnée voit des KPIs à zéro
et un copy d'état vide.

## Backend

- `Endpoints/DashboardEndpoints.cs` mappe `/overview` (et garde les anciens scalaires `/stats`). Il est
  par utilisateur et gated admin via `ICurrentUser` ; l'horloge vient de `TimeProvider`. Il mappe aussi
  `GET/PUT /api/dashboard/layout` — la disposition des widgets de l'utilisateur, chargée au démarrage de
  la page et sauvegardée depuis le dialogue Customize.
- **La persistance du layout** est l'agrégat `UserDashboard` (`Core/Dashboard/UserDashboard.cs`) : un tableau
  par utilisateur (unique sur `UserId`), possédant une liste ordonnée de paramètres de widgets (visible + ordre)
  stockée comme colonne `jsonb`. La liste ordonnée n'est mutée qu'à travers `Apply` / `Reset`, qui valident
  chaque clé contre le catalogue `DashboardWidgets` et gardent la collection complète et dédupliquée. Les
  clés inconnues sont rejetées avec une `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` construit le read model composite `DashboardOverview` : un snapshot d'état
  all-time (comptes groupés), un ensemble fenêtré d'instances materialisé une fois, et les comptes de
  ressources/nœuds. Le statut de l'instance et les horodatages terminaux vivent sur les sous-types TPH
  (pas des colonnes), ainsi les lignes sont lues en mémoire via les helpers partagés
  `InstanceEndpoints.GetStartedAt/GetStoppedAt`. L'heure de l'événement = `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` détient les DTOs, le plan période→(fenêtre, nombre de buckets) et
  `DashboardMath` — bucketing + maths KPI/delta purs et déterministes (pas d'I/O, `now` est passé en param).

Les deltas KPI comparent la fenêtre actuelle contre celle immédiatement précédente (la requête fetch une
double fenêtre pour cela). Il n'y a **pas de flux live de P&L de compte** — la plateforme ne dispose d'équité
que pour les backtests et le suivi prop-firm — ainsi le dashboard est délibérément *opérationnel* (activité,
débit, taux de réussite), pas un ticker de solde de brokerage.

## Design et tokens

Toute la couleur vient des tokens de design (`var(--app-success|-warning|-error|-info|-primary|-text*)`), ainsi
une palette white-label s'écoule gratuitement — y compris le graphique, dont les couleurs de série sont lues
des tokens résolus à l'exécution via `window.appReadTokens` (le SVG ne peut pas consommer directement les
variables CSS). Aucun hex codé en dur nulle part dans le dashboard. Voir [../ui-guidelines.md](../ui-guidelines.md).

## Le lien « Powered by cMind »

Le dashboard affiche un petit lien discret **« Powered by cMind »** pointant vers ce site de documentation.
Il est **affiché par défaut** — nous sommes fiers du projet et ça aide d'autres traders à le trouver —
mais c'est entièrement votre choix. Les revendeurs exécutant une instance entièrement white-labeled passent
`App:Branding:ShowSiteLink` à `false` et il disparaît. Voir
[White-label branding](./white-label.md#powered-by-link).

## Tests

- **Style unit** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, taux de réussite,
  deltas de période précédente, parsing de période, cas limites/vides (événement à `now`, garde divide-by-zero).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — l'agrégat `UserDashboard` : ensemencement
  par défaut, apply ordre/visibilité, append-omitted, duplicate-collapse, rejet de clé inconnue, reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — le read model
  contre Postgres réel (statut/KPIs/activité/ressources, santé des nœuds admin, chemin utilisateur vide), les
  nouvelles sections backtests/copy-profiles/agents, et un **aller-retour** de layout (sauvegarder layout
  custom → recharger → ordre + visibilité persistés).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mobile : les cartes
  KPI, le graphique, l'anneau et le flux s'affichent ; le toggle de période change la période active et
  recharge ; un KPI fore toward `/run` ; **cacher un widget persiste à travers un rechargement**, **Réinitialiser**
  le ramène, et le dialogue Customize fonctionne sur un téléphone sans overflow horizontal. `/` est aussi
  dans `PageSmokeTests`, `MobileLayoutTests` (shell + pas d'overflow) et `MobileJourneyTests`.
