---
title: 0006 — Копирование хостинга координируется атомным DB lease
description: Почему copy профили заявлены через атомный Postgres lease вместо dedicated coordinator, и как это предотвращает двойное-копирование.
---

# 0006 — Копирование хостинга координируется атомным DB lease

## Контекст

Работающий copy профиль должен быть размещен **ровно одним** узлом — два хоста на одном профиле означает каждый исходный trade зеркален дважды (реальные деньги потеряны). Узлы приходят и идут (масштабирование, крахи, rolling updates), и мы не хотим отдельный coordinator сервис для запуска и поддержания alive.

## Решение

Каждый `CopyEngineSupervisor` заявляет профили с **атомным DB lease** на `CopyProfiles` таблице:

- **Claim** — атомный `ExecuteUpdate` (или `FOR UPDATE SKIP LOCKED` когда capping per-node) берет профили, которые unassigned *или* чьи lease истекли. Atomicity означает двое racing supervisors никогда не претендуют на один и тот же row.
- **Renew** — live узел обновляет свой lease каждый цикл, поэтому он сохраняет свой claim.
- **Reclaim** — crashed узел lease истекает, и survivor подбирает профиль на его следующий цикл (self-heal). На graceful shutdown узел **releases** его leases немедленно так failover быстро.
- **Watchdog** — хост чья задача exited пока профиль еще наш перезапускается.
- Reconcile jittered чтобы избежать thundering herd `UPDATE`s на масштабе.

## Последствия

- Нет standalone coordinator для развертывания или поддержания здоровым — Postgres является единственным источником истины.
- Двойное-копирование предотвращено row-level atomicity, не application-level блокировкой.
- Failover latency ограничена lease TTL (минус fast-path graceful release).
- Это деньги path; он охраняется deterministic stress suite (DST) — никогда не ослабляйте DST сценарий чтобы сделать его pass.
