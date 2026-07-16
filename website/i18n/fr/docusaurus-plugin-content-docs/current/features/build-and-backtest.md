---
description: "Construire, exécuter, tester les cBots cTrader (C# et Python, tous .NET) à partir de l'IDE Monaco intégré au navigateur, exécuter sur l'image officielle ghcr.io/spotware/ctrader-console."
---

# Construire et tester les cBots

Construire, exécuter, tester les cBots cTrader (C# **et** Python, tous .NET) à partir de l'IDE Monaco intégré au navigateur, exécuter sur l'image officielle `ghcr.io/spotware/ctrader-console`.

## Construire

- La page **Builder** héberge l'éditeur Monaco ; `CBotBuilder` compile le projet avec
  `dotnet build` **dans un conteneur jetable** (`AppOptions.BuildImage`, répertoire de travail bind-monté
  à `/work`), donc les cibles MSBuild d'utilisateurs non autorisés ne peuvent pas atteindre l'hôte. La restauration NuGet est mise en cache
  entre les compilations via un volume partagé. L'hôte Web a besoin d'accès au socket Docker.
- Les modèles de démarrage C# + Python se trouvent dans `src/Nodes/Builder/Templates/`.

## Exécuter et tester

- **Instances** = hiérarchie d'état TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transition remplace l'entité (changement d'id),
  l'id du conteneur est conservé.
- `NodeScheduler` sélectionne le nœud admissible le moins chargé ; `ContainerDispatcherFactory` route vers
  l'agent HTTP du nœud distant ou le dispatcher Docker local.
- Les pollers d'achèvement réconclient les conteneurs terminés (les conteneurs de backtest se terminent automatiquement via
  `--exit-on-stop`) ; rapport présent → complété (stocke `ReportJson`), manquant → échoué.
- Les logs directs du conteneur sont diffusés au navigateur via SignalR ; les courbes d'équité du backtest sont analysées à partir du
  rapport et graphiques.

## Les données de marché du backtest sont mises en cache par compte

La Console cTrader télécharge les données tick/bar historiques dans son `--data-dir`. Ce répertoire est un
**cache stable et persistant indexé par le compte commercial** (son numéro de compte) — bind-monté à partir
du disque du nœud à son propre chemin de conteneur (`/mnt/data`), un **montage séparé et non imbriqué** du
répertoire de travail par instance. Donc chaque backtest sur le même compte **réutilise** les données déjà téléchargées
au lieu de les re-télécharger à chaque exécution. (Auparavant le
répertoire de données se trouvait sous le répertoire de travail par instance, dont l'id change à chaque exécution, ce qui forçait un
téléchargement frais à chaque backtest.) Le répertoire de travail par instance éphémère contient toujours l'algo, les paramètres, le mot de passe
et le rapport ; le cache de données partagé est compté dans l'utilisation des données de backtest d'un nœud et supprimé par l'action
de nettoyage du nœud.

## Paramètres de backtest

La boîte de dialogue **Backtest** expose les paramètres de backtest cTrader Console réglables par l'utilisateur, vous n'avez donc jamais besoin de
toucher une ligne de commande :

- **Symbol / Timeframe** — la timeframe est un **menu déroulant de chaque période cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, et les périodes Renko/Range/Heikin), dans
  la casse canonique de la console, donc vous choisissez toujours un `--period` valide.
- **From / To** — la fenêtre de backtest (`--start` / `--end`).
- **Data mode** — l'un des trois modes cTrader (`--data-mode`) : **Tick data** (`tick`, précis),
  **barres m1** (`m1`, rapide), ou **Prix d'ouverture uniquement** (`open`, le plus rapide).
- **Starting balance** — par défaut `10000` (`--balance`). Un **solde de 0 ne place aucune transaction et fait
  que cTrader émet un rapport vide sur lequel il s'écrase ensuite** ("Message expected"), donc un solde non nul est
  toujours envoyé.
- **Commission** — `--commission`.
- **Spread** — `--spread`, un **champ numérique en pips qui ne peut pas descendre en dessous de 0**. Il est **masqué en mode Tick
  data**, où cTrader dérive le spread à partir des données tick elles-mêmes (pas de `--spread` envoyé).

Le répertoire des données (`--data-file` / `--data-dir`) est géré par l'application elle-même (un cache par compte, voir
ci-dessus), pas exposé dans la boîte de dialogue.

:::note cTrader plante sur un backtest vide
Si un backtest produit **aucun résultat** — aucune transaction, ou aucune donnée de marché pour les dates/symboles choisis —
l'auteur de rapport de la propre Console cTrader lance `Message expected` et se termine sans rapport. L'application ne peut pas
corriger ce bug en amont, mais elle le détecte et marque l'instance **Failed** avec une raison exploitable
("no backtest results for the selected range…") au lieu d'une trace pile brute. Choisissez une plage de dates plus large
qui a des données de marché disponibles et réessayez.
:::

## Page de détail d'instance

L'ouverture d'une instance (`/instance/{id}`) montre son état actif, ses logs et — pour un backtest — la courbe d'équité.
Le **titre de l'onglet du navigateur** reflète l'instance spécifique (**nom cBot · type · symbole**, par exemple
`TrendBot · Backtest · EURUSD`) donc un onglet de run actif et un onglet de backtest sont distinguables en un coup d'œil.
Un run et un backtest du même cBot sont suivis comme **lignées** distinctes (un id de lignée stable conservé
entre les transitions d'état), donc la page suit exactement une instance et ne mélange jamais les données d'un run avec
celles d'un backtest.

## Contrôles du cycle de vie de l'instance

Chaque ligne d'instance (et sa page de détail) a des contrôles corrects d'état. Une instance **active** affiche
**Stop** ; une instance **terminale** (Stopped / Completed / Failed) affiche **Start (▶)** pour la relancer avec
le même cBot, compte, symbole, timeframe, set de paramètres et image (un run redémarre comme un run, un
backtest comme un backtest). Cliquer sur Stop affiche un avis "Stopping…" et désactive l'icône jusqu'à ce qu'il se résolve, et un nouveau run créé
apparaît immédiatement dans la liste — sans rechargement de page.

Les logs console sont **persistes quand une instance se termine** — pour un run (sur Stop) et pour un
**backtest** (à l'achèvement) de même — donc les logs du dernier run restent consultables sur la page de détail et,
via la barre d'outils des logs, **copiés dans le presse-papiers** (icône Copier les logs) ou **téléchargés** (icône Télécharger les logs)
même après la disparition du conteneur. Les deux agissent sur le log console complet de l'instance, pas seulement la
queue visible à l'écran.

Un `.algo` **uploadé** n'a jamais été construit ici, donc sa colonne **Last Build** sur la page cBots est
laissée vide (elle affiche un temps de compilation uniquement pour les cBots que vous compilez dans le navigateur).

## Éditer et re-exécuter une instance arrêtée

Une instance **arrêtée** (run ou backtest) a un contrôle **Edit** — une icône sur sa ligne dans la liste **et**
à côté de Start/Stop sur sa page de détail — qui ouvre une boîte de dialogue **préremplie** avec sa configuration actuelle.
Vous pouvez changer le **compte commercial, symbole, timeframe, set de paramètres et balise d'image** (et, pour un
backtest, la **fenêtre et tous les paramètres de backtest** ci-dessus), puis **Save & start** le relance avec les
nouveaux paramètres (remplaçant l'instance arrêtée). Le contrôle est **désactivé tandis que l'instance est active** —
seule une instance arrêtée peut être éditée.

## Exécuter à partir de l'éditeur de code

En cliquant sur **Run** dans l'éditeur de code, une boîte de dialogue s'ouvre à la place d'une exécution aveugle codée en dur :

- **Trading account** (requis) — le compte cTrader auquel le cBot se connecte.
- **Parameter set** (optionnel) — choisissez un set existant, ou laissez-le vide pour exécuter avec les
  **valeurs de paramètres par défaut** du cBot. Un bouton **+** à côté du sélecteur crée un nouveau set de paramètres
  en ligne (voir ci-dessous) et le sélectionne.
- **Symbol / Timeframe** par défaut `EURUSD` / `h1` et peuvent être modifiés ; **Cancel** ou **Run**.

Sur **Run** l'éditeur sauvegarde + compile la source actuelle, démarre l'instance sur le compte choisi
avec les paramètres choisis, puis la queue des logs directs du conteneur. (Le flux de logs transmet le cookie d'authentification de l'utilisateur connecté au hub SignalR `/hubs/logs`, donc il se connecte au lieu d'échouer avec
`Invalid negotiation response received`.)

## Ensembles de paramètres

Un **parameter set** est un ensemble nommé et réutilisable de remplacements de paramètres cBot stocké en tant qu'objet JSON plat
mappant chaque nom de paramètre à une valeur scalaire, par exemple `{"Period": 14, "Label": "trend"}`. Au
moment du run/backtest il est transformé en fichier cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Vous pouvez créer/éditer un ensemble en JSON brut à partir de la boîte de dialogue **Parameter
sets** du cBot ou en ligne à partir de la boîte de dialogue Run.

Chaque ensemble de paramètres **appartient à un cBot** : la boîte de dialogue New Parameter Set liste tous vos cBots et vous
**devez en choisir un** — la création est bloquée jusqu'à ce qu'un cBot soit sélectionné. Le **nom d'un ensemble est unique par cBot** :
créer ou renommer un ensemble avec un nom qu'un autre ensemble du même cBot utilise déjà est rejeté (une erreur claire
dans la boîte de dialogue, `409 Conflict` à l'API). Le même nom peut être réutilisé sur un **cBot différent**.

Le JSON est **validé** à l'enregistrement : il doit être un unique objet plat dont les valeurs sont toutes des
scalaires (string / number / bool). Une racine non-objet, un tableau, un objet imbriqué, une valeur `null`, ou un
JSON mal formé est rejeté (une erreur claire dans la boîte de dialogue, `400 Bad Request` à l'API). Un objet vide `{}`
est autorisé et signifie "aucun remplacement".

## Notes sur la CLI Console cTrader

Les backtests ont besoin de `--data-mode` (par défaut `m1`), dates comme `dd/MM/yyyy HH:mm`, et
l'argument JSON `params.cbotset` positionnel ; `run` rejette `--data-dir` (backtest uniquement). Voir
`ContainerCommandHelpers`.

## Nœuds et mise à l'échelle

La capacité d'exécution se met à l'échelle en ajoutant des agents de nœud (auto-enregistrement + battement cardiaque). Voir
[node discovery](../operations/node-discovery.md) et [scaling](../deployment/scaling.md).

## Un compte commercial est requis

Exécuter ou tester un cBot nécessite un compte commercial cTrader pour se connecter. Jusqu'à ce que vous en ajoutiez un sous
**Trading accounts**, les boutons **Run New cBot** / **Backtest New cBot** sont désactivés (avec un
infobulle) et la page affiche un message invitant à la configuration du compte — vous ne frappez plus une erreur brute
`stream connect failed` d'un bot sans compte.
