---
description: "安全に関連するコピーイベントのper-ownerフィード — 宛先がrejectionブレーカーを 作動、アカウント保護またはprop-rule侵害、パニックフラット化。デフォルトオン; App:Copy:NotificationsEnabled（デフォルトtrue）で制御。"
---

# Copy operational notifications (フェーズ2b)

安全に関連するコピーイベントのper-ownerフィード — 宛先がrejectionブレーカーを作動、アカウント保護またはprop-rule侵害、パニックフラット化。**デフォルトオン**（`App:Copy:NotificationsEnabled`、デフォルト`true`）；falseに設定するとサイレント。コピーコンテキストでのOwnコンセプト市場/AI `AlertRule`アグリゲートとは別。

## 動作原理

実行透明性ログと同じアウトオブバンドhost→sink→drainerパターン：

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → 破棄（no-op；エンジン変更なし）
             (notifications on)  ChannelCopyNotificationSink → バウンドDropOldestチャネル
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  各プロファイルの所有者を解決、バッチ処理
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Hostの`Notify(...)`はノンブロッキング、決してスロー — データベースに触れず、コピーを遅延させない。
- ドレイナーは各通知のプロファイルから 所有`UserId`を解決；プロファイルがなくなった（所有者が解決不能）通知はドロップされ孤児にならない。
- `CopyNotification` = append-only、per-row-acknowledgeable feed（而非アグリゲート）。

## 何が発生するか

| Kind | 重大度 | いつ |
|------|----------|------|
| `DestinationTripped` | Warning | G8リjection budgetが使い果たされた；新しいオープンがクールダウン間一時停止。 |
| `AccountProtectionTriggered` | Critical | ZuluGuardエクイティフロア/天井が侵害された；オープンがラッチ（SellOutが清算）。 |
| `PropRuleBreached` | Critical | Propデイリー損失/トレーリングドローダウンが侵害された；宛先が当日flatten + ロックアウト。 |
| `FlattenAll` | Critical | パニックフラット化が実行された；すべての宛先が決済 + ロック。 |
| `TokenInvalidated` | (reserved) | 宛先のトークンが無効化された；ローテーション待ち。 |

## API

- `GET /api/copy/notifications`（owner-scoped） — ユーザーの全プロファイルにわたる最近通知（最新200）+ **未確認**数。
- `POST /api/copy/notifications/{id}/acknowledge` — 1つを既読としてマーク。

## 設定（`App:Copy`）

| 設定 | デフォルト | 効果 |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | 安全通知の放出+ドレイナーの実行。`false` → no-op sink。 |

## テスト

- **ユニット**（`CopyNotificationTests`） — 作動した宛先が`DestinationTripped`を発生；パニックフラットがプロファイルレベルの`FlattenAll`を発生。キャプチャリングsink介して。
- **統合**（`CopyNotificationDrainerTests`、real Postgres） — ドレイナーは所有者を解決+永続化；不明なプロファイルの通知はドロップ。
- **DST** — hostはno-opデフォルトsinkでfire-and-forgetを発するため、コピーストレススイートは緑のままであり（23/23）。
