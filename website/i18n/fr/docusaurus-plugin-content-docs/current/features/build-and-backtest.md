---
description: "Construire, exécuter, backtester les cBots cTrader (C# et Python, tous deux .NET) depuis l'éditeur Monaco intégré au navigateur, exécuter sur l'image officielle ghcr.io/spotware/ctrader-console."
---

# Construire et backtester les cBots

Construire, exécuter, backtester les cBots cTrader (C# **et** Python, tous deux .NET) depuis l'éditeur Monaco intégré au navigateur, exécuter sur l'image officielle `ghcr.io/spotware/ctrader-console`.

## Construire

- La page **Builder** accueille l'éditeur Monaco ; `CBotBuilder` compile le projet avec
  `dotnet build` **dans un conteneur à usage unique** (`AppOptions.BuildImage`, répertoire de travail monté
  en bind à `/work`), afin que les cibles MSBuild d'un utilisateur non approuvé ne puissent accéder à l'hôte. La restauration NuGet est mise en cache
  entre les compilations via un volume partagé. L'hôte Web doit avoir accès au socket Docker.
- Les modèles de démarrage C# + Python se trouvent dans `src/Nodes/Builder/Templates/`.

## Exécuter et backtester

- **Instances** = hiérarchie d'état TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Une transition remplace l'entité (changement d'id),
  l'id du conteneur est conservé.
- `NodeScheduler` choisit le nœud éligible le moins chargé ; `ContainerDispatcherFactory` achemine vers
  l'agent HTTP du nœud distant ou le distributeur Docker local.
- Les pollers d'achèvement réconclient les conteneurs terminés (les conteneurs de backtest s'auto-terminent via
  `--exit-on-stop`) ; rapport présent → terminé (enregistrer `ReportJson`), manquant → échoué.
- Les journaux du conteneur en direct sont diffusés en continu vers le navigateur via SignalR ; les courbes d'équité du backtest sont analysées à partir du
  rapport et affichées en graphique.

## Les données de marché du backtest sont mises en cache par compte

La console cTrader télécharge les données historiques de ticks/barres dans son `--data-dir`. Ce répertoire est un
**cache stable et persistant clé par compte de trading** (son numéro de compte) — monté en bind depuis le disque du nœud
à son propre chemin de conteneur (`/mnt/data`), un **montage séparé et non imbriqué** du répertoire de travail par instance.
Ainsi, chaque backtest sur le même compte **réutilise** les données déjà téléchargées
au lieu de les re-télécharger à chaque exécution. (Auparavant, le
répertoire de données se trouvait sous le répertoire de travail par instance, dont l'id change à chaque exécution, ce qui forçait un
téléchargement frais à chaque backtest.) Le répertoire de travail par instance éphémère contient toujours l'algo, les paramètres, le mot de passe
et le rapport ; le cache de données partagé est compté dans l'utilisation des données de backtest d'un nœud et nettoyé par l'action
de nettoyage du nœud.

## Paramètres du backtest

Le dialogue **Backtest** expose les paramètres du backtest cTrader Console modifiables par l'utilisateur, afin que vous n'ayez jamais à
toucher une ligne de commande :

- **Symbole / Délai d'attente** — le délai d'attente est une **liste déroulante de chaque période cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, et les périodes Renko/Range/Heikin), en
  casse canonique de la console, afin que vous ayez toujours la garantie de choisir un `--period` valide.
- **De / À** — la fenêtre de backtest (`--start` / `--end`).
- **Mode de données** — l'un des trois modes cTrader (`--data-mode`) : **Données de tick** (`tick`, précis),
  **barres m1** (`m1`, rapide), ou **Prix d'ouverture uniquement** (`open`, le plus rapide).
- **Solde initial** — par défaut `10000` (`--balance`). Un **solde de 0 ne place aucune transaction et fait
  que cTrader émet un rapport vide sur lequel il s'arrête ensuite** ("Message expected"), donc un solde non-nul est
  toujours envoyé.
- **Commission** — `--commission`.
- **Spread** — `--spread`, un **champ numérique en pips qui ne peut pas être inférieur à 0**. Il est **masqué en mode Données de tick**,
  où cTrader déduit le spread des données de tick elles-mêmes (pas de `--spread` n'est envoyé).

Le répertoire de données (`--data-file` / `--data-dir`) est géré par l'application elle-même (un cache par compte, voir
ci-dessus), non exposé dans le dialogue.

:::note cTrader s'arrête sur un backtest vide
Si un backtest ne produit **aucun résultat** — aucune transaction, ou aucune donnée de marché pour les dates/symboles choisis —
l'enregistreur de rapports propre à cTrader Console lève `Message expected` et se termine sans rapport. L'application ne peut pas
corriger ce bug en amont, mais elle le détecte et marque l'instance comme **échouée** avec une raison exploitable
("pas de résultats de backtest pour la plage sélectionnée…") au lieu d'une trace brute. Choisissez une plage de dates plus large
qui a des données de marché disponibles et réessayez.
:::

## Page de détail de l'instance

Ouvrir une instance (`/instance/{id}`) affiche son statut en direct, les journaux et — pour un backtest — la courbe
d'équité. Le **titre de l'onglet du navigateur** reflète l'instance spécifique (**nom du cBot · type · symbole**, par ex.
`TrendBot · Backtest · EURUSD`) afin qu'un onglet d'exécution en direct et un onglet de backtest soient distinguables d'un coup d'œil.
Une exécution et un backtest du même cBot sont suivis comme **lignages** distincts (un id de lignage stable
conservé entre les transitions d'état), afin que la page suive exactement une instance et ne mélange jamais les données d'une exécution avec celles d'un
backtest.

## Contrôles du cycle de vie de l'instance

Chaque ligne d'instance (et sa page de détail) a des contrôles corrects d'état. Une instance **active** affiche
**Arrêter** ; une **terminale** (Stopped / Completed / Failed) affiche **Lancer (▶)** pour la redémarrer avec
le même cBot, compte, symbole, délai d'attente, ensemble de paramètres et image (une exécution redémarre en tant qu'exécution, un
backtest en tant que backtest). En cliquant sur Arrêter, un message "Arrêt en cours…" s'affiche et désactive l'icône jusqu'à ce qu'il
se résolve, et une exécution nouvellement créée apparaît immédiatement dans la liste — pas de rechargement de page.

Les journaux de la console sont **conservés quand une instance se termine** — pour une exécution (à l'arrêt) et pour un
**backtest** (à l'achèvement) — afin que les journaux de la dernière exécution restent visibles sur la page de détail et,
via la barre d'outils de journaux, **copiés dans le presse-papiers** (icône Copier les journaux) ou **téléchargés** (icône Télécharger les journaux)
même après la disparition du conteneur. Les deux agissent sur le journaux complet de la console de l'instance, pas seulement la
queue visible à l'écran.

Un **backtest terminé** persiste également son **rapport cTrader** dans les deux formats — le **JSON** brut
(le même que celui que la courbe d'équité et l'analyse IA lisent) et le rapport complet au format **HTML**. Les deux sont
téléchargeables à partir de la ligne de backtest **et** de la page de détail via des icônes dédiées. Seuls les
**rapports de la dernière exécution** sont conservés, et les icônes sont **désactivées** pour tout backtest qui n'a pas commencé, qui est en cours d'exécution ou qui a échoué (et ne sont jamais affichées pour une instance d'exécution) — seul un
backtest terminé a un rapport à télécharger.

Un `.algo` **téléchargé** n'a jamais été construit ici, donc sa colonne **Dernière compilation** sur la page des cBots est
laissée vierge (elle n'affiche un temps de compilation que pour les cBots que vous compilez dans le navigateur).

## Éditer et réexécuter une instance arrêtée

Une instance **arrêtée** (exécution ou backtest) a un contrôle **Éditer** — une icône sur sa ligne dans la liste **et**
à côté de Lancer/Arrêter sur sa page de détail — qui ouvre un dialogue **pré-rempli** avec sa configuration actuelle.
Vous pouvez modifier le **compte de trading, le symbole, le délai d'attente, l'ensemble de paramètres et l'étiquette d'image** (et, pour un
backtest, la **fenêtre et tous les paramètres de backtest** ci-dessus), puis **Enregistrer et lancer** la redémarre avec les
nouveaux paramètres (remplaçant l'instance arrêtée). Le contrôle est **désactivé pendant que l'instance est active** —
seule une instance arrêtée peut être éditée.

## Exécuter à partir de l'éditeur de code

En cliquant sur **Run** dans l'éditeur de code, un dialogue s'ouvre au lieu de démarrer une exécution aveugle et figée dans le code :

- **Compte de trading** (requis) — le compte cTrader auquel le cBot se connecte.
- **Ensemble de paramètres** (optionnel) — choisissez un ensemble existant, ou laissez-le vide pour exécuter avec les
  **valeurs de paramètre par défaut du cBot**. Un bouton **+** à côté du sélecteur crée un nouvel ensemble de paramètres
  en ligne (voir ci-dessous) et le sélectionne.
- **Symbole / Délai d'attente** par défaut à `EURUSD` / `h1` et peuvent être modifiés ; **Annuler** ou **Lancer**.

En cliquant sur **Lancer**, l'éditeur enregistre et compile la source actuelle, démarre l'instance sur le compte choisi
avec les paramètres choisis, puis affiche les journaux du conteneur en direct. (Le flux de journaux transmet le cookie d'authentification de l'utilisateur connecté au hub SignalR `/hubs/logs`, afin qu'il se connecte au lieu d'échouer avec
`Invalid negotiation response received`.)

## Ensembles de paramètres

Un **ensemble de paramètres** est un ensemble nommé et réutilisable de paramètres de remplacement cBot stocké en tant qu'objet JSON plat
mappant chaque nom de paramètre à une valeur scalaire, par ex. `{"Period": 14, "Label": "trend"}`. Au
moment de l'exécution/backtest, il est transformé en fichier cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Vous pouvez créer/éditer un ensemble en JSON brut à partir du dialogue **Ensembles de paramètres** du cBot ou en ligne à partir du dialogue Lancer.

Chaque ensemble de paramètres **appartient à un cBot** : le dialogue Nouvel ensemble de paramètres répertorie tous vos cBots et vous
**devez en choisir un** — la création est bloquée jusqu'à ce qu'un cBot soit sélectionné. Le **nom d'un ensemble est unique par cBot** :
créer ou renommer un ensemble à un nom qu'un autre ensemble du même cBot utilise déjà est rejeté (une erreur claire
dans le dialogue, `409 Conflict` à l'API). Le même nom peut être réutilisé sur un **cBot différent**.

Le JSON est **validé** à l'enregistrement : il doit être un seul objet plat dont les valeurs sont toutes des scalaires
(chaîne / nombre / booléen). Une racine autre qu'un objet, un tableau, un objet imbriqué, une valeur `null`, ou un
JSON mal formé est rejeté (une erreur claire dans le dialogue, `400 Bad Request` à l'API). Un objet vide `{}`
est autorisé et signifie « pas de remplacements ».

## Notes CLI de cTrader Console

Les backtests ont besoin de `--data-mode` (par défaut `m1`), les dates sous forme `dd/MM/yyyy HH:mm`, et
l'argument `params.cbotset` JSON positionnel ; `run` rejette `--data-dir` (backtest uniquement). Voir
`ContainerCommandHelpers`.

## Nœuds et mise à l'échelle

La capacité d'exécution se met à l'échelle en ajoutant des agents de nœud (auto-enregistrement et pulsation). Voir
[découverte de nœud](../operations/node-discovery.md) et [mise à l'échelle](../deployment/scaling.md).
## Un compte de trading est requis

Exécuter ou backtester un cBot nécessite un compte de trading cTrader auquel se connecter. Jusqu'à ce que vous en ajoutiez un sous
**Comptes de trading**, les boutons **Lancer un nouveau cBot** / **Backtester un nouveau cBot** sont désactivés (avec un
info-bulle) et la page affiche une invite vous reliant à la configuration du compte — vous ne rencontrez plus d'erreur brute
`stream connect failed` d'un bot sans compte.
