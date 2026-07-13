---
title: 0004 — `CBotBuilder` работает на веб-хосте в контейнере sandbox
description: Почему untrusted построения cBot происходят на веб-хосте внутри контейнера throwaway SDK, а не на узле.
---

# 0004 — `CBotBuilder` работает на веб-хосте в контейнере sandbox

## Контекст

Построение cBot пользователя означает запуск **untrusted MSBuild** — произвольный код на время построения (targets, source generators, restore scripts). Это требует Docker socket для запуска SDK контейнера. Узлы запускают trading контейнеры и не должны также держать привилегии построения.

## Решение

`CBotBuilder` работает **на веб-хосте** (который уже имеет Docker socket), внутри **контейнера throwaway SDK** с:

- bind-mounted директорией `/work` (только inputs/outputs построения, не host filesystem);
- общий том `app-nuget-cache` для производительности restore;
- нет host сетевого доступа кроме того, что restore требует.

Так untrusted MSBuild не может доступить host filesystem или сеть. Контейнеры запуска/бэктеста, в отличие, запускаются на узлах выбранных `NodeScheduler`.

## Последствия

- Привилегия построения (Docker socket) ограничена веб-хостом; узлы только запускают разрешенные trading образы.
- Каждое построение изолировано в disposable контейнере — malicious построение не может persist или escape.
- Веб-хост должен иметь Docker socket доступный; это требование развертывания, не опциональное.
