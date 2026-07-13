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
