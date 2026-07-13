---
description: "Reflétez le compte maître cTrader sur un ou plusieurs comptes esclaves — cross-broker, cross-cID — avec contrôle par destination + réconciliation de qualité monétaire."
---

# Copie commerciale

Reflet du compte **maître** cTrader sur un ou plusieurs comptes **esclaves** — cross-broker, cross-cID — avec contrôle par destination + réconciliation de qualité monétaire.

## Concepts

- **Profil de copie** — un maître (`SourceAccountId`) + une ou plusieurs **destinations**. Cycle de vie : `Draft → Running → Paused → Stopped` (`Error` en cas d'échec). Racine d'agrégat : `CopyProfile` (possède `CopyDestination`).
- **Destination** — un compte esclave + ensemble de règles complet pour la façon dont le maître est copié sur celui-ci. Tout contrôle par destination, donc un maître alimente les esclaves conservateurs + agressifs à la fois.
- **Hôte du moteur de copie** — travail en cours pour le profil (`CopyEngineHost`). S'abonne au flux d'exécution maître, applique chaque événement à chaque destination.
- **Superviseur** — `CopyEngineSupervisor`, service d'arrière-plan sur chaque nœud. Profils hébergés assignés, auto-guérison sur le cluster (voir [mise à l'échelle](../deployment/scaling.md)).

## Ce qui est reflété

| Événement maître | Action esclave |
|--------------|--------------|
| Ouverture de position marché / plage de marché | Ouvrir une copie dimensionnée (étiquetée avec l'id de position source) |
| Commande en attente limite / arrêt / arrêt-limite | Placer la commande en attente correspondante |
| Amender la commande en attente | Amender la commande en attente reflétée en place |
| Annuler la commande en attente / expiration | Annuler la commande en attente reflétée |
| Fermeture partielle | Fermer la même proportion de la position esclave |
| Scale-in (augmentation de volume) | Ouvrir le volume ajouté (opt-in) |
| Changement de stop-loss / trailing-stop | Amender la protection de la position esclave |
| Fermeture complète | Fermer la copie esclave |

Chaque copie **étiquetée avec l'id de position/commande source**. Après reconnexion, l'hôte reconstruit l'état à partir de la réconciliation : ouvre les copies que le maître détient mais que l'esclave manque, ferme les « orphelins » esclaves que le maître ne détient plus — **sans dupliquer les trades**.

## Créer un profil

La boîte de dialogue **Nouveau profil** sur la page Copie commerciale collecte tout à l'avance : nom du profil, compte source (maître), comptes de destination (esclaves) (sélection multiple avec bouton **Sélectionner tout** ; maître choisi exclu de la liste des esclaves), + ensemble d'options complet par destination ci-dessous. Tous les entrées **validés avant la sauvegarde** — nom/source/destination manquant, paramètre de dimensionnement non positif, limites de lot négatif/incohérent, % tirage baissé hors limites, aucun type de commande activé, filtre de symbole vide, ou paires de carte de symbole mal formées surface comme liste d'erreurs + bloque la sauvegarde. À la confirmation, le profil est créé et chaque esclave sélectionné ajouté avec les paramètres choisis.

Les actions de rangée respectent le cycle de vie : **Démarrer** activé uniquement quand n'est pas en cours, **Arrêter** + **Pause** uniquement quand en cours, **Supprimer** désactivé pendant l'exécution et demande confirmation avant suppression du profil + destinations.

## Options par destination

Défini dans la boîte de dialogue Nouveau profil, sur le panneau par destination de la page Copie commerciale, ou via `POST /api/copy/profiles/{id}/destinations` :

- **Dimensionnement** (`MoneyManagementMode` + paramètre) : lot fixe, lot/multiplicateur notionnel, balance proportionnelle/équité/marge libre, risque fixe %, effet de levier fixe, auto-proportionnel, **risque-%-depuis-arrêt** (M7). Plus limites min/max lot + force-min-lot. **Risque depuis arrêt** dimensionne la destination afin qu'elle risque un pourcentage configuré du *sa propre* balance, dérivée de la **distance d'arrêt-perte du maître** (`maître risque 2% → risque auto-esclave 2%`) : `lots = balance×% ÷ (stopDistance × contractSize)`. L'ouverture maître **sans** stop-perte n'a pas de distance à dimensionner — utilise le **lot de secours risque max configuré** (M7) s'il est défini, sinon sauté (`no_stop_loss`) pas deviné. La balance proportionnelle-**équité**/**marge libre** se dimensionne à partir de l'**équité** réelle du compte (`balance + Σ P&L flottant`, dérivée par cTrader Open API qui ne livre pas l'équité), pas seulement le solde — donc le maître assis sur le bénéfice/perte ouvert dimensionne les copies correctement. La marge utilisée n'est pas exposée par l'API de réconciliation, donc la marge libre est traitée comme l'équité (proxy honest de fonds disponibles) ; les autres modes lisent le solde + sautent le tour de réévaluation supplémentaire.
- **Filtre de direction** : les deux / long uniquement / court uniquement. **Inverser** : retourner le côté (+ échange SL↔TP) pour la copie contraire.
- **Gérer uniquement** (Ignorer-Nouveaux-Trades / Fermer-Uniquement) : reflet des fermetures, fermetures partielles + changements de protection sur les positions déjà copiées, mais n'ouvre **aucune** nouvelle position/commande en attente (sauté `manage_only`). Utilisez pour réduire la destination sans couper les copies existantes.
- **Sync-Ouvert-au-démarrage** / **Sync-Fermé-au-démarrage** (par défaut activé) : sur le **premier** resync du profil, s'il faut ouvrir des copies pour les positions pré-existantes du maître, + s'il faut fermer les copies fermées maître pendant que le profil s'est arrêté. Les deux s'appliquent uniquement au démarrage — la reconnexion mid-run réconcilie toujours complètement pour que le désync récupère quel que soit.
- **Carte de symbole** + **filtre de symbole** (liste blanche / liste noire). Chaque entrée de carte de symbole porte un **multiplicateur de volume optionnel par symbole** (cMAM remplacement par symbole) dimensionnement taille de copie pour ce symbole en haut du dimensionnement de destination (1 = pas de changement). Carte entière importée/exportée en tant que **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv` ; colonnes `Source,Destination,VolumeMultiplier`) — chaque rangée validée via les objets de valeur du domaine, donc un fichier mal formé ne peut pas produire de carte invalide.
- **Fenêtre heures de trading** (C18) — fenêtre UTC quotidienne par destination (`start`/`end` minutes-du-jour, fin exclusive ; `start == end` = toute la journée). Les nouvelles ouvertures en dehors de la fenêtre sautées (`trading_hours`) ; la fenêtre avec `start > end` s'enroule après minuit (par ex. 22:00–06:00). Les positions existantes restent gérées.
- **Filtre d'étiquette source** (C18, équivalent cTrader du filtre de numéro magique MT) — quand défini, copiez uniquement les trades maître dont l'étiquette correspond **exactement** (par ex. les trades d'un bot, ou étiquette manuelle uniquement) ; sinon sauté (`source_label`). Vide = copier tous. Porté sur `ExecutionEvent.SourceLabel` à partir de la position/commande maître `TradeData.Label`, honoré sur resync aussi.
- **Protection de compte** (ZuluGuard / Global Account Protection) — regardez l'**équité en direct** de la destination (`balance + Σ P&L flottant`, interrogée chaque `CopyDefaults.EquityGuardInterval`) contre le plancher `StopEquity` et/ou plafond optionnel `TakeEquity`. En cas de dépassement, appliquez le mode : **CloseOnly** (arrêter les nouvelles copies, conserver la gestion des existantes), **Frozen** (arrêter l'ouverture), **SellOut** (fermer **chaque** copie sur la destination immédiatement). Une fois activée, destination verrouillée — pas de nouvelles ouvertures jusqu'au redémarrage de l'hôte — + alerte `CopyAccountProtectionTriggered` levée. `SellOut` nécessite `StopEquity` ; `TakeEquity` doit s'asseoir au-dessus de `StopEquity`. **Caveat sans garantie :** la vente utilise l'exécution au marché — comme l'équivalent de tous les concurrents, ne peut pas garantir le prix de remplissage en marché rapide/gaché.
- **Bouton de panique Aplatir tout** (C8) — `POST /api/copy/profiles/{id}/flatten` ferme immédiatement **chaque** position copiée sur chaque destination + verrouille contre les nouvelles ouvertures. Acheminé entre processus : l'API définit l'indicateur, le superviseur le livre à l'hôte en cours (réutilisant le canal de rotation de token), qui aplatit en place ; l'indicateur est effacé afin qu'il se déclenche exactement une fois (alerte `CopyFlattenAll`). L'utilisateur pause/arrête alors le profil.
- **Garde de règle prop-firm** (C7) — application de la règle prop-firm que les utilisateurs copiants demandent. Par destination, **plafond de perte quotidienne** (perte de l'équité d'ouverture du jour) et/ou limite **tirage descendant traîné** (perte du pic d'équité en cours), tous deux dans la devise de dépôt. En cas de dépassement, la destination est **aplatie automatiquement** (chaque copie fermée) + **verrouillée** le reste du jour UTC (les nouvelles ouvertures sautées `prop_lockout`) ; l'alerte `CopyPropRuleBreached` se déclenche. Le verrouillage s'efface quand le jour UTC bascule (nouveau ligne de base/pic pris). Partage le même sondage d'équité en direct que la protection de compte.
- **Gigue d'exécution** (C11, désactivé par défaut) — délai aléatoire `0..N` ms avant de placer chaque copie, pour décorréler les horodatages d'ordres presque identiques sur les comptes **propres** de l'utilisateur. **Caveat conformité :** aide pour les prop-firms qui *autorisent* la copie — **pas** outil pour éviter une firm qui l'interdit ; rester dans les règles de votre firm est votre responsabilité.
- **Verrouillage de config** (C9) — geler les paramètres de destination pour une période (`POST …/destinations/{id}/lock` avec minutes). Pendant le verrouillage, la destination ne peut pas être supprimée (l'agrégat rejette avec `CopyDestinationConfigLocked`) — garde délibérée contre les changements impulsifs pendant le tirage baissé. Le verrouillage expire automatiquement à son horodatage.
- **Pré-alerte de cohérence** (C10) — avertir (une fois par jour UTC) quand le **bénéfice quotidien** de la destination atteint le pourcentage configuré de l'équité d'ouverture du jour (`CopyConsistencyThresholdApproaching`), donc la règle de cohérence prop-firm respectée *avant* qu'elle se déclenche. Côté bénéfice, indépendant du verrouillage côté perte ; s'exécute à partir du même ligne de base du jour que la garde de règle prop.
- **Filtre de type de commande** — choisissez exactement quels types de commandes maître copier : marché, plage de marché, limite, arrêt, arrêt-limite (drapeaux `CopyOrderTypes` ; par défaut tous). Sélectivité style cMAM.
- **Copier SL / Copier TP** — reflet du stop-loss/take-profit maître, ou gérer la protection indépendamment.
- **Copier trailing-stop**, **reflet de fermeture partielle**, **reflet de scale-in** — chacun indépendamment bascule.
- **Copier expiration en attente** (par défaut activé) — reflet du horodatage d'expiration Good-Till-Date de la commande en attente maître.
- **Copier glissement maître** (par défaut activé) — pour les ordres de plage de marché + arrêt-limite, placez l'ordre esclave avec le glissement exact-en-points du maître (prix de base pris du spot en direct de l'esclave).
- **Gardes** : % tirage baissé max, plafond de perte quotidienne, délai max de copie, filtre de glissement (sauter la copie si le prix esclave s'est déplacé au-delà de N pips de l'entrée maître). Le **délai max de copie** est mesuré contre l'horodatage du serveur réel de l'événement maître (`ExecutionEvent.ServerTimestamp`) via l'injection `TimeProvider` : le signal plus ancien que le décalage max configuré est sauté, donc la copie obsolète ne place jamais tard (précédemment le délai était toujours zéro + la garde morte).
- **Normalisation de précision SL/TP** (M6) — le stop-loss/take-profit de copie arrondi à la **précision des chiffres du symbole de destination** avant amender, donc le prix maître à une précision plus fine (ou asymétrie de chiffres cross-broker) ne déclenche jamais le `INVALID_STOPLOSS_TAKEPROFIT` du serveur.
- **Disjoncteur de rejet / Garde follower** (G8) — la destination rejetant les ouvertures `CopyDefaults.RejectionBudget` de suite est **actionnée** : pas d'ouvertures nouvelles pour la fenêtre de refroidissement (alerte `CopyDestinationTripped` se déclenche), arrêtant l'orage de rejet d'enfoncer la compte (prop-firm). Les positions existantes sont toujours gérées + fermées pendant l'action ; le disjoncteur se réinitialise automatiquement après le refroidissement + une copie réussie efface le compteur.
- **Plafond santé lot** (C14) — taille de copie max absolue et/ou plafond de multiple maître. La copie calculée dépassant le plafond absolu, ou dépassant `N×` la propre taille de lot du maître, est **durement bloquée** (surfacée comme saut `lot_sanity`, comptée sur `cmind.copy.skipped`) pas placée — défend contre la classe catastrophique-surdimensionnée (maître 0.23-lot se transformant en 3 lots sur chaque récepteur via multiplicateur fugace ou bug d'arrondi). Les deux dimensions 0 par défaut (désactivé).

## Fiabilité & cas limites

Le moteur construit pour la réalité que n'importe quoi peut échouer n'importe quand :

- **Timeout de corrélation de remplissage esclave-en attente** (C13) — en attente esclave reflétée dont le maître en attente a disparu (ni au repos ni remplissement frais) annulé après timeout de corrélation, donc la copie esclave ne peut pas remplir non corrélée dans une position non gérée (`CopyPendingTimedOut`). Le resync nettoie aussi l'orphelin de commande remplissage en attente étiqueté-id.
- **Fermeture/aplatissement robuste** (M8) — la fermeture orpheline sur resync, ou l'aplatissement en cas de dépassement de garde, tolère la position courtier déjà fermée (`POSITION_NOT_FOUND`) : chaque fermeture s'exécute indépendamment, donc une id obsolète ne avorte jamais le resync ou laisse le reste du compte non aplati.

- **Démarrer avec le maître déjà en trades** — au démarrage l'hôte réconcilie + ouvre des copies pour les positions existantes du maître.
- **Chutes de connexion / désync** — sur reconnexion l'hôte réconcilie : ouvre les copies manquantes, ferme les orphelins, re-étiquette les en attente. Pas d'ordres dupliqués.
- **Échec du placement de l'ordre** — l'échec sur une destination enregistrée, n'avorte jamais les autres destinations.
- **Token unique valide par cID** — cTrader invalide le token d'accès ancien du cID au moment où un nouveau est émis. cMind échange le token de l'hôte en cours **en place** (re-auth sur le socket en direct) afin que la copie continue sans lâcher le flux. Voir [cycle de vie du token](token-lifecycle.md).

## Auditabilité

Chaque action émet un événement de log structuré, généré à la source (`LogMessages`) avec l'id du profil, le cID de destination, les ids d'ordre/position, + les valeurs — ordre placé/sauté (avec raison), fermeture partielle, protection appliquée, traîné appliqué, en attente placé/amendé/annulé, expiration reflétée, glissement de plage de marché reflété, token échangé, résumé resync. C'est la piste d'audit pour la conformité + résolution de différend.

Aux côtés des logs, le moteur émet des **métriques OpenTelemetry** sur le compteur `cMind.Copy` (enregistré dans le pipeline OTel partagé, exporté via OTLP / vers Azure Monitor comme le reste) : `cmind.copy.latency` (maître-événement → expédition, ms), `cmind.copy.dispatch.duration` (distribution à toutes les destinations, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (étiqueté par destination), `cmind.copy.skipped` (étiqueté par raison), + `cmind.copy.failed`. Celles-ci rendent la régression latence/glissement mesurable, pas seulement visible dans la ligne de log — la suite en direct affirme contre un budget.

## API

- `GET /api/copy/profiles` — lister.
- `POST /api/copy/profiles` — créer (avec ids de compte destination optionnels).
- `GET /api/copy/profiles/{id}` — détail complet incl. chaque option de destination.
- `POST /api/copy/profiles/{id}/destinations` — ajouter une destination avec l'ensemble d'options complet.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — supprimer.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — cycle de vie.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — modes de dimensionnement, filtres de décision, filtre de type de commande, copie d'expiration, glissement de plage de marché/arrêt-limite, bascules SL/TP, fermeture partielle, amend/annulation en attente, démarrage avec ouverture, déconnexion→désync→resync, échange de token en place, invalidation cross-cID. S'exécute contre `FakeTradingSession`, simulateur en mémoire fidèle cTrader.
- **Integration** (`tests/IntegrationTests/CopyLive`) — affinité nœud/réclamation de bail, propagation de version de token sur Postgres réel.
- **E2E** (`tests/E2ETests`) — option de destination aller-retour via API + UI, cycle de vie complet.
- **Stress / DST** (`tests/StressTests`) — test de simulation déterministe : charges de travail aléatoires ensemencées + injection de failles (basculement de socket, rejet d'ordre, rejet de plage de marché, rotation de token, mort du nœud) conduisent `CopyEngineHost` à la quiescence + affirment les invariants de convergence. Voir [testing/stress-testing.md](../testing/stress-testing.md). Cette suite a surfacé + corrigé une vraie course de démarrage : `OnReconnected` câblé avant le chargement de référence initial + resync, donc la bascule de socket pendant le démarrage pouvait exécuter un second resync simultanément + corrompre les dictionnaires non-concurrents de l'état de l'hôte — le chargement de démarrage + le premier resync s'exécutent maintenant sous `_stateGate`.
- **Live** — comptes de démo cTrader réels ; voir [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Voir [dev-credentials.md](../testing/dev-credentials.md) pour le fichier d'identifiants uniques de live + E2E tiers lu.
