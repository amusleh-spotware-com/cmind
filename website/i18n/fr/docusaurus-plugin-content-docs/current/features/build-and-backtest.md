---
description: "Construire, exécuter, backtester des cBots cTrader (C# et Python, tous deux .NET) depuis l'IDE Monaco intégré au navigateur, exécution sur l'image officielle ghcr.io/spotware/ctrader-console."
---

# Construire et backtester des cBots

Construire, exécuter, backtester des cBots cTrader (C# **et** Python, tous deux .NET) depuis l'IDE
Monaco intégré au navigateur, exécution sur l'image officielle `ghcr.io/spotware/ctrader-console`.

## Construire

- La page **Builder** accueille l'éditeur Monaco ; `CBotBuilder` compile le projet avec
  `dotnet build` **dans un conteneur jetable** (`AppOptions.BuildImage`, répertoire de travail
  bind-mount à `/work`), pour que les cibles MSBuild de l'utilisateur non fiable n'accèdent pas à
  l'hôte. La restauration NuGet est mise en cache entre les builds via un volume partagé. L'hôte
  web doit avoir accès au socket Docker.
- Les modèles de démarrage C# et Python se trouvent dans `src/Nodes/Builder/Templates/`.

## Exécuter et backtester

- **Instances** = hiérarchie d'état TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Les transitions remplacent l'entité (changement d'id),
  l'id du conteneur est conservé.
- `NodeScheduler` sélectionne le nœud éligible le moins chargé ; `ContainerDispatcherFactory`
  achemine vers l'agent HTTP d'un nœud distant ou le répartiteur Docker local.
- Les pollers de fin de traitement réconcillient les conteneurs terminés (les conteneurs de backtest
  se terminent automatiquement via `--exit-on-stop`) ; rapport présent → complété (stockage
  `ReportJson`), absent → échoué.
- Les journaux de conteneur en direct sont diffusés au navigateur via SignalR ; les courbes d'équité
  du backtest sont analysées à partir du rapport et affichées.

## Les données de marché du backtest sont mises en cache par compte

La console cTrader télécharge les données de tick/barre historiques dans son `--data-dir`. Ce
répertoire est un **cache stable et persistent clé sur le compte de trading** (son numéro de compte)
— bind-monté depuis le disque du nœud à son propre chemin de conteneur (`/mnt/data`), un **montage
séparé et non imbriqué** du répertoire de travail par instance. Ainsi, chaque backtest sur le même
compte **réutilise** les données déjà téléchargées au lieu de les retélécharger à chaque exécution.
(Auparavant, le répertoire de données se trouvait sous le répertoire de travail par instance, dont l'id
change à chaque exécution, ce qui forçait un nouveau téléchargement à chaque backtest.) Le répertoire
de travail par instance éphémère contient toujours l'algo, les paramètres, le mot de passe et le
rapport ; le cache de données partagé est comptabilisé dans l'utilisation des données de backtest
d'un nœud et effacé par l'action node-clean.

## Paramètres de backtest

Le dialogue **Backtest** expose tous les paramètres acceptés par la CLI de backtest cTrader Console,
pour que vous n'ayez jamais à toucher à une ligne de commande :

- **From / To** — la fenêtre de backtest (`--start` / `--end`).
- **Data mode** — `m1` (barres d'une minute) ou `tick` (`--data-mode`).
- **Starting balance** — par défaut `10000` (`--balance`). Un **solde de 0 ne place aucune transaction
  et fait que cTrader émet un rapport vide sur lequel il plante ensuite** (« Message expected »),
  donc un solde non nul est toujours envoyé.
- **Commission** et **Spread** (`--commission` / `--spread`, spread en pips).
- **Advanced options** — une boîte libre-forme `name=value` par ligne pour tout autre paramètre de
  backtest que cTrader supporte (par ex. `applyCommissionAutomatically=true`) ; chaque ligne devient
  un argument CLI `--name value`.

## Page de détail des instances

L'ouverture d'une instance (`/instance/{id}`) affiche son statut en direct, les journaux et — pour un
backtest — la courbe d'équité. Le **titre de l'onglet du navigateur** reflète l'instance spécifique
(**nom du cBot · type · symbole**, par ex. `TrendBot · Backtest · EURUSD`) pour qu'un onglet
d'exécution en direct et un onglet de backtest soient distinguables d'un coup d'œil. Une exécution et
un backtest du même cBot sont suivis comme des **lignées** distinctes (un id de lignée stable
conservé entre les transitions d'état), donc la page suit exactement une instance et ne mélange
jamais les données d'une exécution avec celles d'un backtest.

## Contrôles du cycle de vie des instances

Chaque ligne d'instance (et sa page de détail) dispose de contrôles corrects au point de vue l'état.
Une instance **active** affiche **Stop** ; une instance **terminale** (Stopped / Completed / Failed)
affiche **Start (▶)** pour la relancer avec le même cBot, compte, symbole, timeframe, ensemble de
paramètres et image (une exécution recommence comme une exécution, un backtest comme un backtest).
Cliquer sur Stop affiche un avis « Stopping… » et désactive l'icône jusqu'à ce qu'elle se résolve,
et une nouvelle exécution créée apparaît immédiatement dans la liste — aucun rechargement de page.

Les journaux de console sont **persistés quand une instance se termine** — pour une exécution (sur
Stop) et pour un **backtest** (à la fin) — donc les journaux de la dernière exécution restent
consultables sur la page de détail et, via la barre d'outils des journaux, **copiés dans le
presse-papiers** (icône Copier les journaux) ou **téléchargés** (icône Télécharger les journaux)
même après la disparition du conteneur. Les deux agissent sur le journal de console complet de
l'instance, pas seulement la queue visible à l'écran.

Un `.algo` **téléchargé** n'a jamais été construit ici, donc sa colonne **Last Build** sur la page
des cBots est laissée vierge (elle n'affiche une heure de construction que pour les cBots que vous
construisez dans le navigateur).

## Éditer et relancer une instance arrêtée

Une instance **arrêtée** (exécution ou backtest) dispose d'un contrôle **Edit** — une icône sur sa
ligne dans la liste **et** à côté de Start/Stop sur sa page de détail — qui ouvre un dialogue
**prérempli** avec sa configuration actuelle. Vous pouvez changer le **compte de trading, le symbole,
le timeframe, l'ensemble de paramètres et la balise d'image** (et, pour un backtest, la **fenêtre et
tous les paramètres de backtest** ci-dessus), puis **Save & start** le relance avec les nouveaux
paramètres (en remplaçant l'instance arrêtée). Le contrôle est **désactivé tant que l'instance est
active** — seule une instance arrêtée peut être modifiée.

## Exécuter depuis l'éditeur de code

Cliquer sur **Run** dans l'éditeur de code ouvre un dialogue au lieu de lancer une exécution aveugle
et codée en dur :

- **Trading account** (requis) — le compte cTrader auquel le cBot se connecte.
- **Parameter set** (optionnel) — choisir un ensemble existant, ou le laisser vide pour exécuter avec
  les **valeurs de paramètres par défaut du cBot**. Un bouton **+** à côté du sélecteur crée un
  nouvel ensemble de paramètres en ligne (voir ci-dessous) et le sélectionne.
- **Symbol / Timeframe** par défaut à `EURUSD` / `h1` et peuvent être modifiés ; **Cancel** ou
  **Run**.

À **Run**, l'éditeur enregistre + compile la source actuelle, démarre l'instance sur le compte
choisi avec les paramètres choisis, puis fait défiler les journaux de conteneur en direct. (Le flux
de journaux transmet le cookie d'authentification de l'utilisateur connecté au hub SignalR
`/hubs/logs`, pour qu'il se connecte au lieu d'échouer avec `Invalid negotiation response received`.)

## Ensembles de paramètres

Un **ensemble de paramètres** est un ensemble nommé et réutilisable de remplacements de paramètres
de cBot, stocké comme un objet JSON plat mappant chaque nom de paramètre à une valeur scalaire, par
ex. `{"Period": 14, "Label": "trend"}`. Au moment de l'exécution/backtest, il est transformé en
fichier cTrader `params.cbotset` (`{ "Parameters": { … } }`). Vous pouvez créer/modifier un ensemble
en tant que JSON brut depuis le dialogue **Parameter sets** du cBot ou en ligne depuis le dialogue
Run.

Chaque ensemble de paramètres **appartient à un cBot** : le dialogue New Parameter Set liste tous
vos cBots et vous **devez en choisir un** — la création est bloquée jusqu'à ce qu'un cBot soit
sélectionné. Le **nom d'un ensemble est unique par cBot** : créer ou renommer un ensemble avec un nom
qu'un autre ensemble du même cBot utilise déjà est rejeté (une erreur claire dans le dialogue,
`409 Conflict` à l'API). Le même nom peut être réutilisé sur un **cBot différent**.

Le JSON est **validé** à la sauvegarde : il doit être un objet plat unique dont toutes les valeurs
sont des scalaires (chaîne / nombre / booléen). Une racine non-objet, un tableau, un objet imbriqué,
une valeur `null`, ou du JSON malformé est rejeté (une erreur claire dans le dialogue, `400 Bad
Request` à l'API). Un objet vide `{}` est autorisé et signifie « aucun remplacement ».

## Notes sur la CLI cTrader Console

Les backtests nécessitent `--data-mode` (par défaut `m1`), les dates comme `dd/MM/yyyy HH:mm`, et
l'argument positionnel JSON `params.cbotset` ; `run` rejette `--data-dir` (backtest uniquement).
Voir `ContainerCommandHelpers`.

## Nœuds et mise à l'échelle

La capacité d'exécution augmente en ajoutant des agents de nœud (auto-enregistrement + battement de
cœur). Voir [node discovery](../operations/node-discovery.md) et [scaling](../deployment/scaling.md).

## Un compte de trading est nécessaire

Exécuter ou backtester un cBot nécessite un compte de trading cTrader auquel se connecter. Jusqu'à ce
que vous en ajoutiez un sous **Trading accounts**, les boutons **Run New cBot** / **Backtest New
cBot** sont désactivés (avec un conseil d'outil) et la page affiche une invite de lien vers la
configuration du compte — vous n'obtenez plus d'erreur brute `stream connect failed` d'un bot sans
compte.
