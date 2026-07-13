---
description: "Open API cTrader позволяет один валидный access token per cTrader ID (cID). В момент выпуска нового токена предыдущий инвалидируется. Copy engine должен получить новый токен без потери live соединения."
---

# Open API token lifecycle

Open API cTrader позволяет **один валидный access token per cTrader ID (cID)**. В момент
выпуска нового токена — scheduled refresh, или re-authorization когда пользователь привязывает другой
счёт на тот же cID — предыдущий access token инвалидируется. Copy engine на удалённом узле держит
теперь мёртвый токен, поэтому новый токен должен достичь его без потери live соединения.

## Модель

- **`OpenApiAuthorization`** — агрегат, владеющий encrypted access + refresh токенами cID. Уникальный
  индекс на `(UserId, CtidUserId)` enforces **ровно одна авторизация per cID per пользователь**.
- **`TokenVersion`** — монотонный счётчик, увеличиваемый каждый раз при ротации токена (`Refresh()`,
  который также покрывает re-auth path когда привязывается другой счёт на тот же cID). Это
  version marker для single-valid-token rule и то, что работающий хост использует для детекции
  изменения даже если две token strings случайно коллизируют.
- Токены зашифрованы at rest через `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` /
  `OpenApiRefreshToken`). Никогда не логируются и не хранятся в plaintext.

## Propagation (graceful in-place swap)

1. Токен ротируется → новый токен + bumped `TokenVersion` персистятся.
2. `CopyEngineSupervisor` на хостирующем узле перечитывает план каждые reconcile cycle и
   вычисляет **token signature** (access tokens + versions). Изменение означает ротацию.
3. Вместо tearing down хост и restart (что дропнет master's execution stream), supervisor
   **пушит новый токен работающему хосту**.
4. Хост ре-аутентифицирует затрагиваемый счёт **на существующем сокете**
   (`ProtoOAAccountAuthReq` снова) через `SwapAccessTokenAsync`, затем делает light reconcile. Старый
   токен умирает; copy stream никогда не останавливается.

Это делает cross-cID case безопасным: пользователь добавляет второй счёт того же cID
mid-run инвалидирует старый токен, и работающий copy profile продолжает на новом.

## Refresh

`OpenApiTokenRefreshService` (background) проактивно refresh'ит авторизации до expiry;
`OpenApiAuthorization.IsExpiring(threshold, now)` gates it. cTrader ротирует **refresh** токен
при каждом refresh, поэтому новый refresh токен персистится немедленно; read-only cache который не может
персистить самоинвалидируется (релевантно для in-cluster test Job, который монтирует writable copy
secret).

### Failure escalation

Failed refresh не silent. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`
записывает `RefreshFailedAt`, инкрементирует `ConsecutiveRefreshFailures` и всегда raises
`AccessTokenRefreshFailed` (warning). Когда токен now within `App:OpenApi:TokenRefreshCriticalWindow`
(default 6h) expiry и refresh всё ещё failing, escalates **once** с `AccessTokenRefreshCritical`
domain event + `Critical` log so owner может re-authorize перед тем как copy/prop-firm operations lose token.
Failure counter и escalation latch reset на следующем успешном `Refresh`. Service продолжает retry
каждые `TokenRefreshInterval`, поэтому provider/maintenance outage self-heals когда refresh endpoint returns.

## Invalidation alert & auto-recovery (M1)

Partial/again-authorization на cID инвалидирует токен, который всё ещё держит работающий copy host. Когда
trading call reject'ится с `OpenApiErrorKind.TokenInvalid`, хост raises distinct
**`CopyTokenInvalidated`** alert (log 1078) — не generic failure — чтобы notification channel знал что
токен требует внимания. Recovery автоматический: supervisor перечитывает авторизацию каждые cycle и,
когда refresh'нутый токен меняет token signature, пушит его в работающий хост для **in-place
swap** — копирование продолжает без manual re-add. `NotLinkable` profile (token/auth temporarily
unresolvable) likewise re-evaluated every supervisor cycle и hosted the moment its plan builds again.

## Host liveness watchdog (M2)

Supervisor наблюдает run task каждого хостируемого профиля. Если хост exits или faults пока его profile
всё ещё assigned к этому узлу, watchdog cancels и **restarts** его в следующем cycle (log
`CopyHostRestarted`), поэтому wedged хост self-heals вместо needing manual restart — и один profile's
failure никогда не stalled others (per-profile isolation).

## Тесты

- **Unit** — `TokenVersion` bumped on `Refresh`; хост выполняет in-place swap без restart;
  cross-cID инвалидация swap'ит source и destination токены; **инвалидированный destination токен raises
  `CopyTokenInvalidated` и auto-recovers на следующем token push** (M1); watchdog `IsHostDead`
  decision restarts completed/faulted хост и оставляет reassigned profile alone (M2).
- **Integration** — `TokenVersion` persisting + incrementing через EF на реальном Postgres; token
  signature меняется на version bump даже если string unchanged.
