---
description: "Créez, exécutez, backtestez des cBots cTrader (C# et Python, tous deux .NET) à partir de l'IDE Monaco intégré dans le navigateur, exécutez sur l'image officielle ghcr.io/spotware/ctrader-console."
---

# Créer et tester les cBots

Créez, exécutez, backtestez des cBots cTrader (C# **et** Python, tous deux .NET) à partir de l'IDE Monaco intégré dans le navigateur, exécutez sur l'image officielle `ghcr.io/spotware/ctrader-console`.

## Créer

- La page **Builder** héberge l'éditeur Monaco ; `CBotBuilder` compile le projet avec
  `dotnet build` **dans un conteneur jetable** (`AppOptions.BuildImage`, répertoire de travail lié en montage
  à `/work`), donc les cibles MSBuild de l'utilisateur non approuvé ne peuvent pas atteindre l'hôte. La restauration NuGet est mise en cache
  entre les builds via un volume partagé. L'hôte web a besoin d'accès au socket Docker.
- Les modèles de démarrage C# et Python se trouvent dans `src/Nodes/Builder/Templates/`.

## Exécuter et tester

- **Instances** = hiérarchie d'état TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transition remplace l'entité (changement d'id),
  l'id du conteneur est conservé.
- `NodeScheduler` sélectionne le nœud eligible le moins chargé ; `ContainerDispatcherFactory` l'envoie
  au nœud HTTP agent distant ou au dispatcher Docker local.
- Les pollers d'achèvement rapprochent les conteneurs sortis (les conteneurs de backtest se terminent d'eux-mêmes via
  `--exit-on-stop`) ; rapport présent → complété (stocker `ReportJson`), manquant → échoué.
- Les logs du conteneur en direct sont diffusés vers le navigateur via SignalR ; les courbes d'équité du backtest sont analysées à partir du
  rapport et affichées dans un graphique.

## Les données de marché du backtest sont mises en cache par compte

La console cTrader télécharge les données de tick/bar historiques dans son répertoire `--data-dir`. Ce répertoire est un
**cache stable et persistant indexé sur le compte de trading** (son numéro de compte) — lié en montage à partir du disque du nœud à son propre chemin de conteneur (`/mnt/data`), un **montage distinct et non imbriqué** du répertoire de travail par instance.
Ainsi, chaque backtest sur le même compte **réutilise** les données déjà téléchargées
au lieu de les retélécharger à chaque exécution. (Précédemment, le répertoire de données vivait sous le répertoire de travail par instance, dont l'id change à chaque exécution, ce qui forçait un téléchargement à nouveau à chaque backtest.) Le répertoire de travail par instance éphémère contient toujours l'algorithme, les paramètres, le mot de passe
et le rapport ; le cache de données partagé est compté dans l'utilisation des données de backtest d'un nœud et effacé par l'action de nettoyage du nœud.

## Paramètres du backtest

La boîte de dialogue **Backtest** expose les paramètres de backtest cTrader Console pouvant être ajustés par l'utilisateur, afin que vous n'ayez jamais besoin de
toucher une ligne de commande :

- **Symbole / Timeframe** — la timeframe est une **liste déroulante de chaque période cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, et les périodes Renko/Range/Heikin), dans
  la casse canonique de la console, afin que vous sélectionniez toujours une `--period` valide.
- **From / To** — la fenêtre de backtest (`--start` / `--end`).
- **Data mode** — l'un des trois modes cTrader (`--data-mode`) : **Tick data** (`tick`, précis),
  **barres m1** (`m1`, rapide), ou **Prix d'ouverture uniquement** (`open`, le plus rapide).
- **Starting balance** — par défaut `10000` (`--balance`). Un **solde de 0 ne place aucune transaction et fait
  que cTrader émet un rapport vide sur lequel il s'écrase alors** ("Message expected"), donc un solde non nul est
  toujours envoyé.
- **Commission** et **Spread** — `--commission` / `--spread` (spread en pips).

Le répertoire de données (`--data-file` / `--data-dir`) est géré par l'application elle-même (un cache par compte, voir
ci-dessus), pas exposé dans la boîte de dialogue.

## Page de détail de l'instance

L'ouverture d'une instance (`/instance/{id}`) affiche son statut en direct, les logs et — pour un backtest — la courbe d'équité.
Le **titre de l'onglet du navigateur** reflète l'instance spécifique (**nom du cBot · type · symbole**, p. ex.
`TrendBot · Backtest · EURUSD`) afin qu'un onglet d'exécution en direct et un onglet de backtest soient distinguables d'un coup d'œil.
Une exécution et un backtest du même cBot sont suivis comme des **lignées** distinctes (un id de lignée stable porté
à travers les transitions d'état), afin que la page suive exactement une instance et ne mélange jamais les données d'une exécution avec celles d'un
backtest.

## Contrôles du cycle de vie de l'instance

Chaque ligne d'instance (et sa page de détail) a des contrôles corrects pour l'état. Une instance **active** affiche
**Stop** ; une **terminale** (Stopped / Completed / Failed) affiche **Start (▶)** pour la relancer avec
le même cBot, compte, symbole, timeframe, ensemble de paramètres et image (une exécution redémarre comme une exécution, un
backtest comme un backtest). Cliquer sur Stop affiche une notification "Stopping…" et désactive l'icône jusqu'à ce qu'elle
se résolve, et une nouvelle exécution apparaît immédiatement dans la liste — aucun rechargement de page.

Les logs de la console sont **persistés lorsqu'une instance se termine** — pour une exécution (on Stop) et pour un
**backtest** (à l'achèvement) — donc les logs de la dernière exécution restent visibles sur la page de détail et,
via la barre d'outils des logs, **copiés dans le presse-papiers** (icône Copier les logs) ou **téléchargés** (icône Télécharger les logs)
même après la disparition du conteneur. Les deux agissent sur le log complet de la console de l'instance, pas seulement la
queue affichée à l'écran.

Un `.algo` **uploadé** n'a jamais été créé ici, donc sa colonne **Last Build** sur la page des cBots est laissée
vide (elle affiche une heure de build uniquement pour les cBots que vous créez dans le navigateur).

## Éditer et réexécuter une instance arrêtée

Une instance **arrêtée** (exécution ou backtest) a un contrôle **Edit** — une icône sur sa ligne dans la liste **et**
à côté de Start/Stop sur sa page de détail — qui ouvre une boîte de dialogue **préremplie** avec sa configuration actuelle.
Vous pouvez modifier le **compte de trading, symbole, timeframe, ensemble de paramètres et étiquette d'image** (et, pour un
backtest, la **fenêtre et tous les paramètres de backtest** ci-dessus), puis **Save & start** le relance avec les
nouveaux paramètres (en remplaçant l'instance arrêtée). Le contrôle est **désactivé tant que l'instance est active** —
seule une instance arrêtée peut être éditée.

## Exécuter à partir de l'éditeur de code

En cliquant sur **Run** dans l'éditeur de code, une boîte de dialogue s'ouvre au lieu d'exécuter une exécution aveugle et codée en dur :

- **Trading account** (obligatoire) — le compte cTrader auquel le cBot se connecte.
- **Parameter set** (facultatif) — choisissez un ensemble existant, ou laissez-le vide pour exécuter avec les **valeurs de paramètres par défaut** du cBot.
  Un bouton **+** à côté du sélecteur crée un nouvel ensemble de paramètres
  en ligne (voir ci-dessous) et le sélectionne.
- **Symbol / Timeframe** sont par défaut `EURUSD` / `h1` et peuvent être modifiés ; **Cancel** ou **Run**.

On **Run**, l'éditeur enregistre + construit la source actuelle, démarre l'instance sur le compte choisi
avec les paramètres choisis, puis suit les logs du conteneur en direct. (Le flux de logs transmet le cookie d'authentification de l'utilisateur connecté vers le hub SignalR `/hubs/logs`, afin qu'il se connecte au lieu d'échouer avec
`Invalid negotiation response received`.)

## Ensembles de paramètres

Un **parameter set** est un ensemble nommé et réutilisable de remplacements de paramètres de cBot stockés en tant qu'objet JSON plat
mappant chaque nom de paramètre à une valeur scalaire, p. ex. `{"Period": 14, "Label": "trend"}`. Au
moment de l'exécution/backtest, il est transformé en fichier cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Vous pouvez créer/éditer un ensemble en tant que JSON brut à partir de la boîte de dialogue **Parameter
sets** du cBot ou en ligne à partir de la boîte de dialogue Run.

Chaque ensemble de paramètres **appartient à un cBot** : la boîte de dialogue New Parameter Set liste tous vos cBots et vous
**devez en sélectionner un** — la création est bloquée jusqu'à ce qu'un cBot soit sélectionné. Le **name** d'un ensemble **est unique par cBot** :
créer ou renommer un ensemble avec un nom qu'un autre ensemble du même cBot utilise déjà est rejeté (une erreur claire
dans la boîte de dialogue, `409 Conflict` à l'API). Le même nom peut être réutilisé sur un **cBot différent**.

Le JSON est **validé** lors de l'enregistrement : il doit être un objet plat unique dont toutes les valeurs sont des scalaires
(string / number / bool). Une racine non-objet, un tableau, un objet imbriqué, une valeur `null`, ou du JSON malformé
est rejeté (une erreur claire dans la boîte de dialogue, `400 Bad Request` à l'API). Un objet vide `{}`
est autorisé et signifie "aucun remaniement".

## Notes sur la CLI de cTrader Console

Les backtests nécessitent `--data-mode` (par défaut `m1`), les dates sous forme `dd/MM/yyyy HH:mm`, et
l'argument JSON positionnel `params.cbotset` ; `run` rejette `--data-dir` (backtest uniquement). Voir
`ContainerCommandHelpers`.

## Nœuds et mise à l'échelle

La capacité d'exécution est mise à l'échelle en ajoutant des nœuds agents (auto-enregistrement + heartbeat). Voir
[node discovery](../operations/node-discovery.md) et [scaling](../deployment/scaling.md).

## Un compte de trading est obligatoire

L'exécution ou le backtest d'un cBot nécessite un compte de trading cTrader auquel se connecter. Jusqu'à ce que vous en ajoutiez un sous
**Trading accounts**, les boutons **Run New cBot** / **Backtest New cBot** sont désactivés (avec un
infobulle) et la page affiche une invitation reliant à la configuration du compte — vous n'atteignez plus une
erreur brute `stream connect failed` d'un bot sans compte.
