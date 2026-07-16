---
description: "Générez, exécutez et testez les cBots cTrader (C# et Python, tous deux .NET) depuis l'éditeur Monaco intégré au navigateur, exécutés sur l'image officielle ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Générez, exécutez et testez les cBots cTrader (C# **et** Python, tous deux .NET) depuis l'éditeur Monaco intégré au navigateur, exécutés sur l'image officielle `ghcr.io/spotware/ctrader-console`.

## Build

- La page **Builder** héberge l'éditeur Monaco ; `CBotBuilder` compile le projet avec `dotnet build` **dans un conteneur temporaire** (`AppOptions.BuildImage`, répertoire de travail monté en bind à `/work`), afin que les cibles MSBuild d'utilisateurs non fiables n'accèdent pas à l'hôte. La restauration NuGet est mise en cache entre les compilations via un volume partagé. L'hôte web a besoin d'accès à la prise Docker.
- Les modèles de démarrage C# et Python se trouvent dans `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hiérarchie d'état TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). La transition remplace l'entité (changement d'id), l'id du conteneur est conservé.
- `NodeScheduler` sélectionne le nœud admissible le moins chargé ; `ContainerDispatcherFactory` achemine vers un agent HTTP de nœud distant ou un distributeur Docker local.
- Les pollers de fin de tâche rapprochent les conteneurs fermés (les conteneurs de backtest se ferment automatiquement via `--exit-on-stop`) ; le rapport présent → complété (stocke `ReportJson`), absent → échoué.
- Les journaux de conteneur en direct sont transmis au navigateur via SignalR ; les courbes de capital-actions de backtest sont analysées à partir du rapport et affichées sous forme de graphique.

## Backtest market data is cached per account

L'archive historique de données tick/bar téléchargée par cTrader Console se trouve dans son `--data-dir`. Ce répertoire est un **cache stable et persistant indexé sur le compte commercial** (son numéro de compte) — monté en bind à partir du disque du nœud à son propre chemin de conteneur (`/mnt/data`), un **montage séparé et non imbriqué** du répertoire de travail par instance. Ainsi, chaque backtest sur le même compte **réutilise** les données déjà téléchargées au lieu de les re-télécharger à chaque exécution. (Auparavant, le répertoire de données se trouvait sous le répertoire de travail par instance, dont l'id change à chaque exécution, ce qui forçait un téléchargement récent à chaque backtest.) Le répertoire de travail par instance éphémère contient toujours l'algo, les paramètres, le mot de passe et le rapport ; le cache de données partagé est compté dans l'utilisation des données de backtest d'un nœud et effacé par l'action node-clean.

## Backtest settings

La boîte de dialogue **Backtest** expose chaque paramètre que l'interface de ligne de commande du backtest cTrader Console accepte, afin que vous n'ayez jamais besoin de toucher une ligne de commande :

- **From / To** — la fenêtre de backtest (`--start` / `--end`).
- **Data mode** — l'un des trois modes cTrader (`--data-mode`) : **Tick data** (`tick`, précis), **m1 bars** (`m1`, rapide) ou **Open prices only** (`open`, le plus rapide).
- **Starting balance** — par défaut `10000` (`--balance`). Un **solde de 0 n'effectue aucune transaction et amène cTrader à émettre un rapport vide qu'il plante ensuite** (« Message expected »), donc un solde non nul est toujours envoyé.
- **Commission** et **Spread** — `--commission` / `--spread` (spread en pips).
- **Data file** (optionnel) — un chemin côté nœud vers un fichier de données historiques (`--data-file`) ; laissez vide pour utiliser les données téléchargées/mises en cache.
- **Expose environment variables** — un bouton bascule qui transmet les variables d'environnement de l'hôte au cBot (l'indicateur `--environment-variables`).

## Instance detail page

L'ouverture d'une instance (`/instance/{id}`) affiche son statut en direct, ses journaux et — pour un backtest — la courbe de capital-actions. Le **titre de l'onglet du navigateur** reflète l'instance spécifique (**nom du cBot · type · symbole**, par exemple `TrendBot · Backtest · EURUSD`) afin qu'un onglet run en direct et un onglet backtest soient distinguables en un coup d'œil. Une exécution et un backtest du même cBot sont suivis comme des **lignées** distinctes (un id de lignée stable porté à travers les transitions d'état), afin que la page suive exactement une instance et ne mélange jamais les données d'une exécution avec celles d'un backtest.

## Instance lifecycle controls

Chaque ligne d'instance (et sa page de détail) a des contrôles corrects par rapport à l'état. Une instance **active** affiche **Stop** ; une instance **terminale** (Stopped / Completed / Failed) affiche **Start (▶)** pour la relancer avec le même cBot, compte, symbole, délai, ensemble de paramètres et image (une exécution redémarre en tant qu'exécution, un backtest en tant que backtest). Cliquer sur Stop affiche une notification « Stopping… » et désactive l'icône jusqu'à ce qu'elle se résolve, et une nouvelle exécution créée apparaît dans la liste immédiatement — sans rechargement de page.

Les journaux de console sont **persistants lorsqu'une instance se termine** — pour une exécution (à l'arrêt) et pour un **backtest** (à la fin) de même — afin que les journaux de la dernière exécution restent affichables sur la page de détail et, via la barre d'outils du journal, **copiés dans le presse-papiers** (Icône Copier les journaux) ou **téléchargés** (Icône Télécharger les journaux) même après la disparition du conteneur. Les deux agissent sur le journal complet de la console de l'instance, pas seulement sur la queue affichée à l'écran.

Un `.algo` **téléchargé** n'a jamais été compilé ici, donc sa colonne **Last Build** sur la page des cBots est laissée vide (elle affiche un temps de compilation uniquement pour les cBots que vous compilez dans le navigateur).

## Edit & re-run a stopped instance

Une instance **arrêtée** (exécution ou backtest) a un contrôle **Edit** — une icône sur sa ligne dans la liste **et** à côté de Start/Stop sur sa page de détail — qui ouvre une boîte de dialogue **préremplie** avec sa configuration actuelle. Vous pouvez modifier le **compte commercial, le symbole, le délai, l'ensemble de paramètres et la balise d'image** (et, pour un backtest, la **fenêtre et tous les paramètres de backtest** ci-dessus), puis **Save & start** la relance avec les nouveaux paramètres (en remplaçant l'instance arrêtée). Le contrôle est **désactivé pendant que l'instance est active** — seule une instance arrêtée peut être modifiée.

## Run from the code editor

Cliquer sur **Run** dans l'éditeur de code ouvre une boîte de dialogue au lieu de déclencher une exécution aveugle et codée en dur :

- **Trading account** (obligatoire) — le compte cTrader auquel le cBot se connecte.
- **Parameter set** (optionnel) — sélectionnez un ensemble existant, ou laissez vide pour exécuter avec les **valeurs de paramètre par défaut du cBot**. Un bouton **+** à côté du sélecteur crée un nouvel ensemble de paramètres en ligne (voir ci-dessous) et le sélectionne.
- **Symbol / Timeframe** par défaut `EURUSD` / `h1` et peuvent être modifiés ; **Cancel** ou **Run**.

À la **Run**, l'éditeur enregistre et compile la source actuelle, démarre l'instance sur le compte choisi avec les paramètres choisis, puis suit les journaux de conteneur en direct. (Le flux de journaux transmet le cookie d'authentification de l'utilisateur connecté au hub SignalR `/hubs/logs`, afin qu'il se connecte au lieu d'échouer avec `Invalid negotiation response received`.)

## Parameter sets

Un **parameter set** est un ensemble nommé et réutilisable de remplacements de paramètres de cBot stockés sous la forme d'un objet JSON plat mappant chaque nom de paramètre à une valeur scalaire, par exemple `{"Period": 14, "Label": "trend"}`. Au moment de l'exécution/du backtest, il est transformé en fichier cTrader `params.cbotset` (`{ "Parameters": { … } }`). Vous pouvez créer/modifier un ensemble en tant que JSON brut à partir de la boîte de dialogue **Parameter sets** du cBot ou en ligne à partir de la boîte de dialogue Run.

Chaque ensemble de paramètres **appartient à un cBot** : la boîte de dialogue New Parameter Set répertorie tous vos cBots et vous **devez en sélectionner un** — la création est bloquée jusqu'à ce qu'un cBot soit sélectionné. Le **nom d'un ensemble est unique par cBot** : créer ou renommer un ensemble avec un nom qu'un autre ensemble du même cBot utilise déjà est rejeté (une erreur claire dans la boîte de dialogue, `409 Conflict` à l'API). Le même nom peut être réutilisé sur un **cBot différent**.

Le JSON est **validé** à l'enregistrement : il doit être un seul objet plat dont toutes les valeurs sont des scalaires (string / number / bool). Une racine non-objet, un tableau, un objet imbriqué, une valeur `null` ou un JSON malformé est rejeté (une erreur claire dans la boîte de dialogue, `400 Bad Request` à l'API). Un objet vide `{}` est autorisé et signifie « aucun remplacement ».

## cTrader Console CLI notes

Les backtests ont besoin de `--data-mode` (par défaut `m1`), les dates sous la forme `dd/MM/yyyy HH:mm` et l'argument positionnel JSON `params.cbotset` ; `run` rejette `--data-dir` (backtest uniquement). Voir `ContainerCommandHelpers`.

## Nodes & scale

La capacité d'exécution s'étend en ajoutant des agents de nœud (auto-inscription et heartbeat). Voir [node discovery](../operations/node-discovery.md) et [scaling](../deployment/scaling.md).

## A trading account is required

L'exécution ou le backtest d'un cBot nécessite un compte commercial cTrader auquel se connecter. Jusqu'à ce que vous en ajoutiez un sous **Trading accounts**, les boutons **Run New cBot** / **Backtest New cBot** sont désactivés (avec une info-bulle) et la page affiche une invite liant à la configuration du compte — vous ne frappez plus d'erreur brute `stream connect failed` d'un bot sans compte.
