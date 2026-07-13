---
description: "Per-owner лента безопасностно-релевантных copy-событий — назначение превысило rejection breaker, breach аккаунт-защиты или prop-rule, panic flatten. On по умолчанию…"
---

# Copy operational notifications (Фаза 2b)

Per-owner лента безопасностно-релевантных copy-событий — назначение превысило rejection breaker, breach аккаунт-защиты или prop-rule, panic flatten. **On по умолчанию** (`App:Copy:NotificationsEnabled`, по умолчанию `true`); установите false для отключения. Собственная концепция в Copy-контексте, отдельная от рыночного/AI агрегата `AlertRule`.

## Как это работает

Та же out-of-band host→sink→drainer схема, что и лог execution-transparency:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → отбрасывает (no-op; движок не изменён)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  резолвит owner каждого профиля, батчит
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Хост `Notify(...)` неблокирующий, никогда не выбрасывает — не трогает БД, не задерживает копирование.
- Drainer резолвит владеющий `UserId` из каждого уведомления профиля; уведомление для ненайденного профиля (owner не резолвится) дропается, не осиротеет.
- `CopyNotification` = append-only, per-row-acknowledgable лента (не агрегат).

## Что генерируется

| Kind | Severity | Когда |
|------|----------|-------|
| `DestinationTripped` | Warning | G8 rejection budget исчерпан; новые открытия приостановлены на cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling нарушен; открытия заблокированы (SellOut ликвидирует). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown нарушен; назначение flatten'ено + заблокировано на остаток UTC дня. |
| `FlattenAll` | Critical | Panic flatten выполнен; каждое назначение закрыто + заблокировано от новых открытий. |
| `TokenInvalidated` | (reserved) | Токен назначения был инвалидирован; ожидает ротации. |

## API

- `GET /api/copy/notifications` (owner-scoped) — недавние уведомления пользователя (до 200 последних) по всем профилям, плюс **непросмотренных** count.
- `POST /api/copy/notifications/{id}/acknowledge` — отметить одно как прочитанное.

## Конфигурация (`App:Copy`)

| Настройка | По умолчанию | Эффект |
|---------|---------|---------|
| `NotificationsEnabled` | `true` | Испускать уведомления безопасности + запускать drainer. `false` → no-op sink. |

## Тесты

- **Unit** (`CopyNotificationTests`) — превышенное назначение генерирует `DestinationTripped`; panic flatten генерирует `FlattenAll` на уровне профиля. Через capturing sink.
- **Integration** (`CopyNotificationDrainerTests`, реальный Postgres) — drainer резолвит owner + персистит; уведомление для неизвестного профиля дропается.
- **DST** — хост испускает fire-and-forget с no-op default sink, поэтому copy stress suite остаётся зелёным (23/23).
