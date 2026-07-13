---
title: Dashboard
description: Le dashboard cMind — un centre de commandement en direct et mobile-first pour vos runs de cBot, backtests, ressources, et cluster de nœuds.
---

# Dashboard 📊

La première chose que vous voyez quand vous vous connectez, et honnêtement la page que vous laisserez ouverte toute la journée. La page d'accueil (`/`, `Components/Pages/Index.razor`) est un **centre de commandement en direct et mobile-first** pour l'activité de l'utilisateur connecté entre les runs de cBot, backtests, ressources et (pour les admins) le cluster de nœuds. Il se rafraîchit lui-même, regarde bien sur un téléphone, et ne vous oblige jamais à appuyer sur F5.

## Ce qu'il affiche

De haut en bas, prioriser pour un téléphone (chaque bloc est un élément de pile pleine largeur sur mobile, une grille réactive sur tablette/bureau) :

1. **En-tête** — titre, un indicateur en direct (un vrai point pulsant ; statique sous `prefers-reduced-motion`), l'heure de dernière mise à jour, et un **bascule de période** (`1H · 24H · 7D · 30D`) qui pilote les KPIs et le graphique.
2. **KPIs Hero** — quatre cartes en coup d'œil, chacune un grand nombre + une sparkline SVG inline, et (où significatif) un **delta vs la période précédente** :
   - **Actif maintenant** — runs + backtests en cours de démarrage/exécution.
   - **Taux de succès** — complétés ÷ (complétés + échoués) sur la période ; delta en points de pourcentage.
   - **Complétés** — runs/backtests terminés cette période ; delta vs période précédente.
   - **Échoués** — échecs cette période ; delta (moins c'est mieux, donc une baisse affiche vert).
3. **Graphique d'activité** — une zone ApexCharts d'une chronologie de commencé / complété / échoué par bac de temps.
4. **Anneau de statut d'instance** — un donut de en cours d'exécution / backtests / en attente / complétés / échoués, total au centre.
5. **Backtests** — un snapshot de trois tuiles (en cours d'exécution / complétés / échoués), click-through à `/backtest`.
6. **Copy trading** — vos profils de copy-trading avec un point de statut en direct, compte de destination, et un badge **Live** sur les profils en cours d'exécution ; click-through à `/copy-trading`.
7. **Agents IA** — vos agents de trading pilotés par persona avec état d'exécution (archétype · statut) et heure de dernière action ; click-through à `/agent-studio`.
8. **Flux d'activité en direct** — les 20 événements les plus récents (plus récent d'abord) avec un point coloré par statut et un horodatage relatif.
9. **Santé du cluster** (admins uniquement) — nœuds actifs-vs-totaux et une jauge de capacité utilisée.
10. **Tuiles de ressources** — cBots, comptes de trading, ID cTrader, clés MCP (click through vers leurs pages).

## Personnaliser votre dashboard

Chaque bloc ci-dessus est un **widget que vous contrôlez**. Appuyez sur **Personnaliser** (en haut à droite de l'en-tête) pour ouvrir une dialogue où vous **afficher/masquer** n'importe quel widget et **réordonner**-les avec des flèches haut/bas. **Réinitialiser par défaut** restaure l'ordre du catalogue. Votre choix est **persisté côté serveur par utilisateur**, donc il vous suit entre les navigateurs et les appareils — pas seulement cet onglet.

- Les widgets feature-gated et admin-only (Copy trading, Agents IA, Santé du cluster) n'apparaissent dans la dialogue que quand votre déploiement/rôle peut les utiliser.
- Le catalogue de widgets est une source unique de vérité dans `Core/Dashboard/DashboardWidgets.cs` ; la présentation (étiquette + icône + disponibilité) vit dans `Components/Dashboard/DashboardWidgetMeta.cs`.

## Comment il reste en direct

La page sonde `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` toutes les 10 secondes et re-rend les widgets sur place — pas de rechargement manuel. Un échec de fetch transitoire est avalé et réessayé au prochain tick ; la boucle s'arrête proprement à dispose. Le premier chargement affiche un squelette ; un échec persistant affiche une carte d'erreur avec **Réessayer** ; un utilisateur sans données voit des KPIs à zéro et copie d'état vide.

## Backend

- `Endpoints/DashboardEndpoints.cs` mappe `/overview` (et garde l'ancien `/stats` scalaire). C'est par utilisateur et admin-gated via `ICurrentUser` ; l'horloge vient de `TimeProvider`. Il mappe aussi `GET/PUT /api/dashboard/layout` — la mise en page de widget de l'utilisateur, chargée au démarrage de la page et sauvegardée depuis la dialogue Personnaliser.
- **Persistance de mise en page** est l'agrégat `UserDashboard` (`Core/Dashboard/UserDashboard.cs`) : un tableau par utilisateur (unique sur `UserId`), possédant une liste ordonnée de paramètres de widget (visible + ordre) stockée dans une colonne `jsonb`. La liste ordonnée n'est jamais mutée que par `Apply` / `Reset`, qui valident chaque clé contre le catalogue `DashboardWidgets` et gardent la collection complète et désduplicée. Les clés inconnues sont rejetées avec une `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` construit le modèle de lecture composite `DashboardOverview` : un snapshot de statut de tous les temps (comptages groupés), un ensemble fenêtré d'instances matérialisé une fois, et comptages de ressources/nœuds. Le statut d'instance et les horodatages terminaux vivent sur les sous-types TPH (pas de colonnes), donc les lignes sont lues en mémoire via les helpers partagés `InstanceEndpoints.GetStartedAt/GetStoppedAt`. Heure de l'événement = `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` tient les DTOs, le plan période→(fenêtre, compte de bac), et `DashboardMath` — pur, déterministe bac + math KPI/delta (pas d'I/O, `now` est passé).

Les deltas KPI comparent la fenêtre actuelle à celle immédiatement précédente (la requête récupère une double fenêtre pour cela). Il n'y a **pas de flux de P&L de compte en direct** — la plateforme n'a que l'équité pour les backtests et le suivi prop-firm — donc le dashboard est délibérément *opérationnel* (activité, débit, taux de succès), pas un ticker de solde de courtage.

## Design & tokens

Toute couleur vient des jetons de design (`var(--app-success|-warning|-error|-info|-primary|-text*)`), donc une palette white-label s'écoule gratuitement — incluant le graphique, dont les couleurs de série sont lues depuis les jetons résolus à l'exécution via `window.appReadTokens` (SVG ne peut pas consommer les variables CSS directement). Pas de hex dur-codé nulle part dans le dashboard. Voir [../ui-guidelines.md](../ui-guidelines.md).

## Le lien « Powered by cMind »

Le dashboard affiche un petit lien **« Powered by cMind »** de bon goût qui pointe vers ce site de documentation. C'est **affiché par défaut** — nous sommes fiers du projet et cela aide d'autres traders à le trouver — mais c'est entièrement votre choix. Les revendeurs exécutant une instance entièrement white-labelée basculez `App:Branding:ShowSiteLink` à `false` et il disparaît. Voir [Branding White-label](./white-label.md#powered-by-link).

## Tests

- **Style unit** (`tests/IntegrationTests/DashboardMathTests.cs`) — bacsage, taux de succès, deltas de période précédente, analyse de période, vides/limites (événement à `now`, garde de division par zéro).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — l'agrégat `UserDashboard` : graine par défaut, appliquer ordre/visibilité, append-omis, effondrement dupliqué, rejet de clé inconnue, réinitialisation.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — le modèle de lecture contre Postgres réel (statut/KPIs/activité/ressources, santé du nœud admin, chemin d'utilisateur vide), les nouvelles sections backtests/copy-profiles/agents, et un **round-trip** de mise en page (sauvegarder mise en page personnalisée → recharger → ordre + visibilité persistés).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — bureau + mobile : cartes KPI, graphique, anneau et flux rendent ; le bascule de période bascule la période active et recharge ; un KPI forer à travers vers `/run` ; **masquer un widget persiste entre un rechargement**, **Réinitialiser** l'amène, et la dialogue Personnaliser fonctionne sur un téléphone sans débordement horizontal. `/` est aussi dans `PageSmokeTests`, `MobileLayoutTests` (shell + no-overflow) et `MobileJourneyTests`.
