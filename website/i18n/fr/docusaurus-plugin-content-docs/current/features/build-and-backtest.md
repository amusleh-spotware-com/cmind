---
description: "Créer, exécuter, backtester des cBots cTrader (C# et Python, tous les deux .NET) depuis l'IDE Monaco intégré au navigateur, exécuter sur l'image ghcr.io/spotware/ctrader-console officielle."
---

# Créer & backtester cBots

Créez, exécutez, backtestez des cBots cTrader (C# **et** Python, tous les deux .NET) depuis l'IDE Monaco
intégré au navigateur, exécutez sur l'image officielle `ghcr.io/spotware/ctrader-console`.

## Créer

- La page **Builder** héberge l'éditeur Monaco ; `CBotBuilder` compile le projet avec
  `dotnet build` **dans un conteneur jetable** (`AppOptions.BuildImage`, répertoire de travail bind-mount
  à `/work`), donc les cibles MSBuild d'utilisateurs non fiables n'atteignent pas l'hôte. La restauration NuGet est mise en cache
  entre les builds via un volume partagé. L'hôte web a besoin d'un accès au socket Docker.
- Les modèles de démarrage C# + Python vivent dans `src/Nodes/Builder/Templates/`.

## Exécuter & backtester

- **Instances** = hiérarchie d'état TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). La transition remplace l'entité (changement d'id),
  l'id de conteneur est maintenu.
- `NodeScheduler` choisit le nœud le moins chargé éligible ; `ContainerDispatcherFactory` achemine vers
  l'agent HTTP du nœud distant ou le distributeur Docker local.
- Les pollers de réalisation réconcilient les conteneurs sortis (les conteneurs de backtest se quittent via
  `--exit-on-stop`) ; rapport présent → complété (stocker `ReportJson`), manquant → échec.
- Les logs de conteneur en direct se transmettent au navigateur via SignalR ; les courbes d'équité de backtest sont analysées à partir du
  rapport + tracées.

## Notes de CLI cTrader Console

Les backtests nécessitent `--data-mode` (par défaut `m1`), les dates comme `dd/MM/yyyy HH:mm`, et
l'argument positionnel JSON `params.cbotset` ; `run` rejette `--data-dir` (backtest uniquement). Voir
`ContainerCommandHelpers`.

## Nœuds & échelle

La capacité d'exécution s'étend en ajoutant des agents nœud (auto-enregistrement + pulsation). Voir
[découverte de nœud](../operations/node-discovery.md) et [mise à l'échelle](../deployment/scaling.md).

## Exécuter depuis l'éditeur de code

Cliquer sur **Exécuter** dans l'éditeur de code ouvre une boîte de dialogue au lieu de lancer une exécution aveugle et codée en dur :

- **Compte de trading** (obligatoire) — le compte cTrader auquel le cBot se connecte.
- **Jeu de paramètres** (facultatif) — choisissez un jeu existant, ou laissez vide pour exécuter avec les **valeurs de paramètres par défaut** du cBot. Un bouton **+** à côté du sélecteur crée un nouveau jeu de paramètres en ligne (voir ci-dessous) et le sélectionne.
- **Symbole / Unité de temps** sont par défaut `EURUSD` / `h1` et modifiables ; **Annuler** ou **Exécuter**.

Lors de l'**Exécution**, l'éditeur enregistre et compile le code source actuel, démarre l'instance sur le compte choisi avec les paramètres choisis, puis suit les logs du conteneur en direct. (Le flux de logs transmet le cookie d'authentification de l'utilisateur connecté au hub SignalR `/hubs/logs`, de sorte qu'il se connecte au lieu d'échouer avec `Invalid negotiation response received`.)

## Jeux de paramètres

Un **jeu de paramètres** est un ensemble nommé et réutilisable de remplacements de paramètres du cBot, stocké sous forme d'objet JSON plat associant chaque nom de paramètre à une valeur scalaire, p. ex. `{"Period": 14, "Label": "trend"}`. Au moment de l'exécution/du backtest, il est converti en fichier cTrader `params.cbotset` (`{ "Parameters": { … } }`). Vous pouvez créer/modifier un jeu en JSON brut depuis la boîte de dialogue **Jeux de paramètres** du cBot ou en ligne depuis la boîte de dialogue Exécuter.

Le JSON est **validé** à l'enregistrement : il doit être un objet plat unique dont toutes les valeurs sont scalaires (chaîne / nombre / booléen). Une racine non-objet, un tableau, un objet imbriqué, une valeur `null` ou un JSON mal formé est rejeté (erreur claire dans la boîte de dialogue, `400 Bad Request` côté API). Un objet vide `{}` est autorisé et signifie « aucun remplacement ».
