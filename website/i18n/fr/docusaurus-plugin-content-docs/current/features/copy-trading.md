---
description: "Reproduisez le compte cTrader maître sur un ou plusieurs comptes esclaves — multi-courtier, multi-cID — avec contrôle par destination et réconciliation à la norme bancaire."
---

# Copy trading

Reproduisez le compte cTrader **maître** sur un ou plusieurs comptes **esclaves** — multi-courtier, multi-cID — avec contrôle par destination et réconciliation à la norme bancaire.

## Concepts

- **Profil de copy** — un maître (`SourceAccountId`) + une ou plusieurs **destinations**. Cycle de vie : `Draft → Running → Paused → Stopped` (`Error` en cas d'échec). Racine agrégée : `CopyProfile` (possède `CopyDestination`).
- **Destination** — un compte esclave + ensemble complet de règles pour la façon dont le maître est copié dessus. Toute configuration par destination, afin qu'un maître alimente à la fois des esclaves conservateurs et agressifs.
- **Hôte du moteur de copy** — worker en cours d'exécution pour le profil (`CopyEngineHost`). S'abonne au flux d'exécution maître, applique chaque événement à chaque destination.
- **Superviseur** — `CopyEngineSupervisor`, service de fond sur chaque nœud. Héberge les profils assignés, s'auto-répare sur le cluster (voir [scalabilité](../deployment/scaling.md)).

## What gets mirrored

| Événement maître | Action esclave |
|--------------|--------------|
| Ouverture de position marché / plage de marché | Ouvrir une copie redimensionnée (libellée avec l'id de position source) |
| Ordre en attente limite / stop / stop-limite | Placer l'ordre en attente correspondant |
| Modification d'ordre en attente | Modifier l'ordre en attente en miroir sur place |
| Annulation d'ordre en attente / expiration | Annuler l'ordre en attente en miroir |
| Fermeture partielle | Fermer la même proportion de la position esclave |
| Scale-in (augmentation de volume) | Ouvrir le volume supplémentaire (opt-in) |
| Modification du stop-loss / trailing-stop | Modifier la protection de la position esclave |
| Fermeture complète | Fermer la copie esclave |

Chaque copie **libellée avec l'id de position/ordre source**. Après reconnexion, l'hôte reconstruit l'état à partir de la réconciliation : ouvre les copies que le maître détient mais que l'esclave manque, ferme les « orphelins » esclaves que le maître ne détient plus — **sans dupliquer les trades**.

## Creating a profile

**New Profile** ouvre un formulaire **page entière** dédié (`/copy-trading/new`), pas un dialogue — l'ensemble des options est suffisamment vaste qu'une page se lit mieux sur téléphone et ordinateur de bureau. Il collecte tout d'avance : nom du profil, source (compte maître), destinations (comptes esclaves) (multi-select avec bouton **Sélectionner tout** ; maître choisi exclu de la liste esclave), + l'ensemble complet des options par destination. **Seuls les comptes liés via l'Open API cTrader sont sélectionnables** comme maître ou destination — la copie place des ordres sur l'Open API, donc un compte ajouté manuellement (cID uniquement) ne peut pas copier et n'est pas listé ; quand aucun n'est lié, la page affiche un avis pointant vers Comptes de trading. Les modes de dimensionnement, la direction et le filtre de symbole s'affichent en tant que **libellés humains** avec une **explication en puces par mode** sur l'info-bulle d'aide de gestion de l'argent. **Chaque contrôle porte une info-bulle d'aide** expliquant ce qu'il fait et comment l'utiliser. Les entrées structurées utilisent **des contrôles validés appropriés** — nombres/pourcentages via champs numériques, modes/direction/filtre via sélections, le filtre de symbole via une liste d'ajout/suppression de puces de symbole, et la carte de symbole via un tableau d'ajout/suppression de lignes `Source → Destination (× multiplicateur)` — jamais un blob texte séparé par des virgules. Toutes les entrées **validées avant la sauvegarde** — nom/source/destination manquants, paramètre de dimensionnement non positif, limites de lot négatives/incohérentes, pourcentage de drawdown hors plage, aucun type de commande activé, ou filtre de symbole vide affichent une liste d'erreurs + bloquent la sauvegarde. À la création, le profil est créé + chaque esclave sélectionné ajouté avec les paramètres choisis, puis la page revient à la liste Copy Trading.

**Import / export.** L'ensemble complet des paramètres peut être **exporté vers un fichier JSON** et **réimporté** pour préremplir le formulaire, afin qu'une configuration puisse être réutilisée entre les profils sans ressaisie. La carte de symbole peut également être **exportée / importée sous forme de fichier CSV** (`Source,Destination,VolumeMultiplier`) — préparez une grande carte de symboles de courtier dans une feuille de calcul et chargez-la en une étape. Les mêmes contrôles de symbole et import/export CSV sont également disponibles dans le dialogue de destination sur la page Copy Trading.

Les actions de ligne respectent le cycle de vie : **Démarrer** activé uniquement quand pas en cours d'exécution, **Arrêter** + **Pause** uniquement en cours d'exécution, **Supprimer** désactivé pendant l'exécution + demande confirmation avant suppression du profil + destinations.

## Per-destination options

Défini sur la page Nouveau profil, dans le dialogue de destination sur la page Copy Trading, ou via `POST /api/copy/profiles/{id}/destinations` :

- **Dimensionnement** (`MoneyManagementMode` + paramètre) : lot fixe, multiplicateur lot/notionnel, solde proportionnel/equity/marge libre, risque fixe %, levier fixe, auto-proportionnel, **risque-%-depuis-stop** (M7). Plus limites min/max lot + forcer-min-lot. **Risque-depuis-stop** dimensionne la destination afin qu'elle risque le pourcentage configuré de *son propre* solde, dérivé de la **distance du stop-loss du maître** (`maître risque 2% → esclave risque automatiquement 2%`) : `lots = solde×% ÷ (stopDistance × contractSize)`. Ouverture maître **sans** stop-loss n'a pas de distance pour dimensionner → utilise le lot **max-risque fallback** configuré (M7) s'il est défini, sinon ignoré (`no_stop_loss`) pas deviné. **Equity**/**marge libre** proportionnelle dimensionne à partir de l'**equity** réel du compte (`solde + Σ flottant P&L`, dérivé par Open API cTrader qui ne fournit pas d'equity), pas simplement solde — donc maître assis sur profit/perte ouvert dimensionne les copies correctement. Marge utilisée non exposée par API de réconciliation, donc marge libre traitée comme equity (proxy sincère de fonds disponibles) ; autres modes lisent solde + ignorent tour d'évaluation supplémentaire.
- **Filtre de direction** : les deux / long uniquement / short uniquement. **Inverser** : retourner le côté (+ échanger SL↔TP) pour copie contraire.
- **Gérer uniquement** (Ignorer-Nouveaux-Trades / Fermer-Uniquement) : miroir les fermetures, fermetures partielles + modifications de protection sur les positions déjà copiées, mais ouvre **aucune** nouvelle position/ordre en attente (ignoré `manage_only`). Utilisez pour réduire la destination sans couper les copies existantes.
- **Sync-Ouvert-au-démarrage** / **Sync-Fermé-au-démarrage** (défaut activé) : sur la **première** resync du profil, si ouvrir des copies pour les positions pré-existantes du maître, + si fermer les copies que le maître a fermées pendant que le profil était arrêté. Les deux s'appliquent uniquement au démarrage — la reconnexion en cours d'exécution réconcilie toujours complètement pour que la désynchronisation se récupère indépendamment.
- **Carte de symbole** + **filtre de symbole** (liste blanche / liste noire). Chaque entrée de carte de symbole porte un **multiplicateur de volume par symbole** optionnel (remplacement par symbole cMAM) dimensionnant la taille de copie pour ce symbole en haut du dimensionnement de destination (1 = pas de changement). Carte entière importe/exporte comme **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv` ; colonnes `Source,Destination,VolumeMultiplier`) — chaque ligne validée via objets de valeur de domaine, un fichier mal formé ne peut donc pas produire de carte invalide.
- **Fenêtre d'heures de trading** (C18) — fenêtre UTC quotidienne par destination (`start`/`end` minutes du jour, fin exclusive ; `start == end` = tout le jour). Les nouvelles ouvertures en dehors de la fenêtre ignorées (`trading_hours`) ; fenêtre avec `start > end` se fond après minuit (par exemple 22:00–06:00). Les positions existantes restent gérées.
- **Filtre d'étiquette source** (C18, équivalent cTrader du filtre magic-number MT) — quand défini, copier uniquement les trades maître dont l'étiquette correspond **exactement** (par exemple, trades d'un bot, ou étiquette manuelle uniquement) ; sinon ignoré (`source_label`). Vide = copier tous. Porté sur `ExecutionEvent.SourceLabel` depuis `TradeData.Label` de position/ordre maître, honoré sur resync aussi.
- **Protection de compte** (ZuluGuard / Global Account Protection) — surveiller l'**equity en direct** de destination (`solde + Σ flottant P&L`, sondé tous les `CopyDefaults.EquityGuardInterval`) contre le plancher `StopEquity` et/ou plafond optionnel `TakeEquity`. En cas de violation, appliquer le mode : **CloseOnly** (arrêter les nouvelles copies, continuer à gérer les existantes), **Frozen** (arrêter l'ouverture), **SellOut** (fermer **chaque** copie sur destination immédiatement). Une fois déclenché, destination verrouillée — aucune nouvelle ouverture jusqu'au redémarrage de l'hôte — + alerte `CopyAccountProtectionTriggered` levée. `SellOut` nécessite `StopEquity` ; `TakeEquity` doit se situer au-dessus de `StopEquity`. **Caveat sans garantie :** sell-out utilise l'exécution marché — comme tous les équivalents des concurrents, ne peut pas garantir le prix de remplissage en marché rapide/gapé.
- **Bouton de panique Flatten-All** (C8) — `POST /api/copy/profiles/{id}/flatten` ferme immédiatement **chaque** position copiée sur chaque destination + verrouille contre les nouvelles ouvertures. Routé inter-processus : l'API définit le drapeau, le superviseur livre à l'hôte en cours d'exécution (réutilisant le canal de rotation de token), qui se flatten sur place ; drapeau effacé afin que se déclenche exactement une fois (alerte `CopyFlattenAll`). L'utilisateur met ensuite le profil en pause/arrête.
- **Garde de règle prop-firm** (C7) — application des règles que les utilisateurs copieurs prop-firm demandent. Par destination, **cap de perte quotidienne** (perte depuis l'equity d'ouverture du jour) et/ou limite de **drawdown traînant** (perte depuis l'equity de pic courant), les deux en devise de dépôt. En cas de violation, destination **auto-aplatie** (chaque copie fermée) + **verrouillée** reste de jour UTC (nouvelles ouvertures ignorées `prop_lockout`) ; alerte `CopyPropRuleBreached` se déclenche. Le verrouillage se nettoie quand le jour UTC bascule (nouvelle baseline/pic pris). Partage le même sondage d'equity en direct que protection de compte.
- **Gigue d'exécution** (C11, désactivé par défaut) — délai aléatoire `0..N` ms avant de placer chaque copie, pour décorréler les timestamps d'ordre presque identiques entre les propres comptes de l'utilisateur. **Caveat de conformité :** aide pour les prop-firms qui *autorisent* la copie — **pas** outil pour contourner la firm qui l'interdit ; rester dans les règles de votre firm est votre responsabilité.
- **Verrouillage de configuration** (C9) — geler les paramètres de destination pour la période (`POST …/destinations/{id}/lock` avec minutes). Tant que verrouillé, la destination ne peut pas être supprimée (l'agrégat rejette avec `CopyDestinationConfigLocked`) — garde délibérée contre les changements impulsifs pendant le drawdown. Le verrouillage expire automatiquement à son timestamp.
- **Pré-alerte de cohérence** (C10) — avertir (une fois par jour UTC) quand le **profit quotidien** de destination atteint le pourcentage configuré de l'equity d'ouverture du jour (`CopyConsistencyThresholdApproaching`), afin que la règle de cohérence prop-firm soit respectée *avant* de se déclencher. Côté profit, indépendant du verrouillage côté perte ; s'exécute hors même baseline de jour que la garde de règle prop.
- **Filtre de type d'ordre** — choisir exactement quels types d'ordres maître copier : marché, plage de marché, limite, stop, stop-limite (drapeaux `CopyOrderTypes` ; défaut tous). Sélectivité style cMAM.
- **Copier SL / Copier TP** — refléter le stop-loss / take-profit du maître, ou gérer la protection indépendamment.
- **Copier trailing stop**, **miroir fermeture partielle**, **miroir scale-in** — chacun indépendamment basculable.
- **Copier expiration en attente** (défaut activé) — refléter le timestamp d'expiration Good-Till-Date de l'ordre en attente du maître.
- **Copier le slippage maître** (défaut activé) — pour ordres de plage de marché + stop-limite, placer l'ordre esclave avec le slippage exacte en points du maître (prix de base pris depuis le spot en direct de l'esclave).
- **Gardes** : max drawdown %, cap de perte quotidienne, délai de copie max, filtre de slippage (ignorer la copie si le prix esclave s'est déplacé au-delà de N pips de l'entrée maître). **Délai de copie max** mesuré contre le timestamp du serveur réel de l'événement maître (`ExecutionEvent.ServerTimestamp`) via `TimeProvider` injecté : signal plus ancien que le max-lag configuré ignoré, donc la copie ancienne ne se place jamais tard (auparavant le délai était toujours zéro + la garde morte).
- **Normalisation de précision SL/TP** (M6) — prix stop-loss/take-profit copiés arrondis à la **précision de chiffre du symbole** destination avant modification, afin que le prix maître à précision plus fine (ou décalage de chiffre multi-courtier) ne déclenche jamais `INVALID_STOPLOSS_TAKEPROFIT` du serveur.
- **Disjoncteur de rejet / Follower Guard** (G8) — destination rejetant `CopyDefaults.RejectionBudget` ouvertures d'affilée est **déclenchée** : aucune nouvelle ouverture pour fenêtre d'amortissement (alerte `CopyDestinationTripped` se déclenche), arrêtant la tempête de rejet de marteler (prop-firm) compte. Les positions existantes sont toujours gérées + fermées pendant le déclenchement ; le disjoncteur se réinitialise automatiquement après refroidissement + copie réussie efface le compteur.
- **Plafond de santé des lots** (C14) — taille de copie maximale absolue et/ou cap multiple du maître. La copie calculée dépassant le cap absolu, ou dépassant `N×` la taille de lot du maître lui-même, **durement bloquée** (surface comme skip `lot_sanity`, comptée sur `cmind.copy.skipped`) pas placée — défend contre la classe de sur-dimensionnement catastrophique (maître de 0,23 lot se transformant en 3 lots sur chaque récepteur via multiplicateur échappé ou bug d'arrondi). Les deux dimensions défaut `0` (off).

## Reliability & edge cases

Le moteur construit pour la réalité que tout peut échouer à tout moment :

- **Timeout de corrélation de remplissage en attente esclave** (C13) — esclave en attente en miroir dont le maître en attente a disparu (ni reposant ni fraîchement rempli) annulé après timeout de corrélation, afin que la copie esclave ne se remplisse pas de manière non corrélée en position non gérée (`CopyPendingTimedOut`). La resync nettoie également l'orphelin en attente rempli étiqueté avec id d'ordre.
- **Fermeture/aplatissement robuste** (M8) — fermeture d'orphelin sur resync, ou aplatissement sur violation de garde, tolère la position déjà fermée par le courtier (`POSITION_NOT_FOUND`) : chaque fermeture s'exécute indépendamment, afin qu'un id ancien ne avorte jamais la resync ou laisse le reste du compte non aplati.

- **Démarrage avec maître déjà dans les trades** — au démarrage l'hôte réconcilie + ouvre des copies pour les positions existantes du maître.
- **Connexion interrompue / désynchronisation** — à la reconnexion l'hôte réconcilie : ouvre les copies manquantes, ferme les orphelins, re-libelle les attentes. Aucun ordre dupliqué.
- **Échec de placement d'ordre** — échec sur une destination consigné, jamais bloque les autres destinations.
- **Token unique valide par cID** — cTrader invalide le vieux token d'accès de cID au moment où un nouveau est émis. cMind échange le token de l'hôte en cours d'exécution **sur place** (re-auth sur socket en direct) afin que la copie continue sans interrompre le flux. Voir [cycle de vie du token](token-lifecycle.md).

## Auditability

Chaque action émet un événement de log structuré généré à la source (`LogMessages`) avec id de profil, cID de destination, ids d'ordre/position, + valeurs — ordre placé/ignoré (avec raison), fermeture partielle, protection appliquée, trailing appliqué, en attente placé/modifié/annulé, expiration en miroir, slippage de plage de marché en miroir, token échangé, résumé resync. C'est la piste d'audit pour conformité + résolution de dispute.

Aux côtés des logs, le moteur émet des **métriques OpenTelemetry** sur le compteur `cMind.Copy` (enregistré dans le pipeline OTel partagé, exporté sur OTLP / vers Azure Monitor comme le reste) : `cmind.copy.latency` (événement maître → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out à toutes les destinations, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagué par destination), `cmind.copy.skipped` (tagué par raison), + `cmind.copy.failed`. Ces rendent la régression de latence/slippage mesurable, pas seulement visible en ligne de log — la suite en direct affirme contre le budget.

## API

- `GET /api/copy/profiles` — lister.
- `POST /api/copy/profiles` — créer (avec ids de compte destination optionnels).
- `GET /api/copy/profiles/{id}` — détail complet incl. chaque option de destination.
- `POST /api/copy/profiles/{id}/destinations` — ajouter une destination avec l'ensemble d'options complet.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — supprimer.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — cycle de vie.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — modes de dimensionnement, filtres de décision, filtre de type d'ordre, copie d'expiration, slippage de plage de marché/stop-limite, toggles SL/TP, fermeture partielle, modification/annulation en attente, démarrage-avec-ouvert, déconnexion→désynchronisation→resync, échange de token sur place, invalidation multi-cID. S'exécute sur `FakeTradingSession`, simulateur en mémoire fidèle à cTrader.
- **Intégration** (`tests/IntegrationTests/CopyLive`) — affinité de nœud/réclamation de bail, propagation de version de token sur Postgres réel.
- **E2E** (`tests/E2ETests`) — aller-retour des options de destination via API + UI, cycle de vie complet.
- **Stress / DST** (`tests/StressTests`) — test de simulation déterministe : charges aléatoires avec graine + injection de faute (flap de socket, rejet d'ordre, rejet de plage de marché, rotation de token, mort de nœud) entraînent `CopyEngineHost` jusqu'à quiescence + affirment les invariants de convergence. Voir [testing/stress-testing.md](../testing/stress-testing.md). Cette suite a mis en évidence + corrigé une course véritable au démarrage : `OnReconnected` câblé avant chargement de référence initial + resync, afin que le flap de socket au démarrage puisse exécuter une seconde resync de manière concurrente + corrompre les dictionnaires non-concurrents de l'état de l'hôte — le chargement au démarrage + première resync s'exécutent maintenant sous `_stateGate`.
- **En direct** — comptes de démo cTrader réels ; voir [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Voir [dev-credentials.md](../testing/dev-credentials.md) pour fichier d'identifiants unique que les tiers en direct + E2E lisent.
## Profile controls and destination management

Start/stop sont des boutons d'icône sur chaque ligne de profil (désactivés quand l'action ne s'applique pas). Les comptes source et destination sont affichés par leur **numéro de compte**, jamais un id interne. Cliquer sur un profil ouvre un **dialogue** pour gérer ses comptes de destination (ajouter/supprimer avec paramètres complètes par destination).
