---
description: "Build, run, backtest cTrader cBots (C# и Python, оба .NET) из in-browser Monaco IDE, работает на официальном образе ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **и** Python, оба .NET) из in-browser Monaco
IDE, работает на официальном образе `ghcr.io/spotware/ctrader-console`.

## Build

- Страница **Builder** содержит Monaco editor; `CBotBuilder` компилирует проект с
  `dotnet build` **в одноразовом контейнере** (`AppOptions.BuildImage`, рабочая директория bind-mount
  в `/work`), поэтому недоверенные пользовательские MSBuild-таргеты не имеют доступа к файловой системе хоста. NuGet restore кэшируется
  между сборками через shared volume. Веб-хост требует доступ к Docker socket.
- C# + Python стартовые шаблоны живут в `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = иерархия состояний TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Переход заменяет сущность (смена id),
  id контейнера сохраняется.
- `NodeScheduler` выбирает наименее загруженный подходящий узел; `ContainerDispatcherFactory` маршрутизирует к
  удалённому HTTP-агенту узла или локальному Docker-диспетчеру.
- Completion poller'ы сверяют вышедшие контейнеры (бэктест-контейнеры выходят сами через
  `--exit-on-stop`); отчёт есть → completed (сохраняется `ReportJson`), отсутствует → failed.
- Логи работающего контейнера стримятся в браузер через SignalR; кривые капитала бэктестов парсятся из
  отчёта и чартятся.

## Заметки о CLI cTrader Console

Бэктестам нужен `--data-mode` (по умолчанию `m1`), даты как `dd/MM/yyyy HH:mm`, и
`params.cbotset` JSON positional arg; `run` отклоняет `--data-dir` (только для бэктеста). См.
`ContainerCommandHelpers`.

## Nodes & scale

Вместимость исполнения масштабируется добавлением агентов-узлов (саморегистрация + heartbeat). См.
[node discovery](../operations/node-discovery.md) и [scaling](../deployment/scaling.md).
