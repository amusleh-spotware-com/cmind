---
description: "小売prop firm（FTMOスタイル）は評価アカウントを販売：トレーダーはリスク制限（最大日次損失、最大合計/トレーリングドローダウン、一貫性、タイムリミット）内に保ちながらプロフィットターゲットに到達する必要があります。"
---

# Prop-firm challenge simulation

小売prop firm（FTMOスタイル）は**評価アカウント**を販売：トレーダーはリスク制限
（最大日次損失、最大合計/トレーリングドローダウン、一貫性、タイムリミット）内に保ちながら
プロフィットターゲットに到達する必要があります。cMindでは、ユーザーが**任意の業界形状のカスタムchallengeを作成し、**
`TradingAccount`にバインドし、コピー取引操作のように**実行/停止、ノードでホスト、**
cTrader Open APIで**ライブ追跡**させ、集約が各ルールを決定論的に評価;
合格または違反時、challengeを終了、フラグ付け、ユーザーにアラート。

## ドメイン（境界コンテキスト：PropFirm）

`PropFirmChallenge` = 集約ルート（モジュール`Core.PropFirm`）、
`TradingAccount`へのstrong idのみで参照（cross-aggregate FKなし）。
ルール評価、フェーズ/ステートマシン、ノードリースを所有。

### 値オブジェクトとルールセット

- **`Money`**（非負）、**`MoneyAmount`**（符号付き）、**`Percent`**（0–100]、
  **`TradingDayRequirement`**（0–365）。
- **`EquitySnapshot`** `(equity, balance)` — 集約に供給される読み取り値。
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — 非エクイティファクト。
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity`（intra-day、浮动P&Lを含む）
  または`Balance`（実現済みのみ）。
- **`DrawdownLimit`** — `Static`（開始バランスから）、`TrailingPercent`（ピークエクイティから）、
  `TrailingThresholdDollar`（一定ドル金額でエクイティピークをtrailし、閾値に達すると**開始バランスでロック** —
  先物スタイル）。
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — 1日が総利益を占める場合に合格をブロック。
- **`ChallengeRules`**は以下の上和+ `MaxCalendarDays`、`MaxInactivityDays`、`MaxOpenPositions`、
  `AllowWeekendHolding`、`AllowNewsTrading`、`Kind`、`SingleStep`を運ぶ。
  ルール数学はVOで живут（`DrawdownLimit.IsBreached`、`DailyLossLimit.IsBreached`、
  `ConsistencyRule.IsSatisfied`）; 集約がorchestrates。

### Challenge種類とテンプレート

`ChallengeTemplates.For(kind)`が`OnePhase`、`TwoPhase`、`ThreePhase`、`InstantFunding`、
または`Custom`（完全制御）の有効なプリセットを構築。UIがテンプレートを事前入力;
ユーザーは任意のフィールドを調整可能。

### フェーズとステータス

- **フェーズ：** `Evaluation → Verification → Funded`（シングルステップはVerificationをスキップ）。
- **ステータス：** `Active`、`Passed`、`Failed`、plus ライフサイクル`Stopped`
  （追跡一時停止） — `Create`でchallengeを`Active`で開始;
  `Stop()`/`Resume()` 토글 `Active↔Stopped`。
- **`BreachReason`：** `DailyLoss`、`MaxDrawdown`、`Consistency`、`TimeLimit`、
  `Inactivity`、`WeekendHolding`、`NewsTrading`、`MaxExposure`。

### ルール評価

- **`RecordEquity(EquitySnapshot, now)`** — 取引日を境界でロール
  （一貫性ルールのため前日の利益を取得）、peak/daily peaksを更新、
  次に**最初の違反で失敗**（日次損失 → ドローダウン → タイムリミット → 不活性、順序で）
  または利益ターゲット、最小取引日、一貫性要件がすべて満たされた場合にフェーズを進行。
  順序違いのスナップショットと终端challengeでのレコードは`DomainException`をスロー。
- **`RecordActivity(ActivitySnapshot, now)`** — 行動ルールを評価
  （最大オープンポジション、週末保有、ニュース取引）、不活性ルールのアクティビティをスタンプ。

ソフト **`PropFirmDrawdownWarning`**は、エクイティ使用率が設定可能な閾値を超えた場合に一度だけ発火。

ドメインイベント：`PropFirmChallengeStarted`、`PropFirmChallengeStopped`、`PropFirmPhasePassed`、
`PropFirmChallengePassed`、`PropFirmChallengeBreached`、`PropFirmDrawdownWarning`。

## ライブ追跡（実行）— ノードホスト、自己修復

追跡はコピー取引ホストスタックと正確に同じ;
prop tracker = コピーエンジンの**読み取り専用cousin**。

- **`PropFirmTrackingSupervisor`**（`src/Nodes/PropFirm`）—
  各ノード上の`BackgroundService`、`App:PropFirm:Enabled`でゲート。
  各サイクルは自己修復リース（`AssignedNode` + `LeaseExpiresAt`; リース期限切れ後の死亡ノードのchallengeを回収 —
  コピー取引と同じアトミック`ExecuteUpdate`クレーム、因此2つのノードが二重追跡しない）で
  アクティブなchallengeを**クレーム**、リースを更新、インプレースでローテーションされたトークンを推送、
  challengeが`Active`を離れたホストを停止。
- **`PropFirmTrackingHost`**（`src/Nodes/PropFirm`）—
  challengeごとに1つ。アカウント用に`IOpenApiTradingSession`を開き、
  `App:PropFirm:EquityPollInterval`でライブエクイティを再計算、集約に供給。
  ローテーションでアクセス tokenをインプレースで交換（セッションドロップなし）。
  challengeが`Active`ではなくなると終了。
- **`PropFirmEquityCalculator`**（`src/CTraderOpenApi/Client`）—
  cTrader-faithfulエクイティ数学。エクイティはOpen APIでは提供되지 않으므로、派生：
  `equity = balance + Σ(unrealized P&L)`、各ポジションのP&Lは
  `priceDifference × units × quote→deposit rate + swap + commission`
  （`units = wire volume / 100`; ロングはbidで再評価、ショートはask）。
  バランスは`ProtoOATrader`から; ポジション（エントリー価格、スワップ、手数料）はreconcileから;
  スポットサブスクリプションからのライブbid/ask。純粋で分離 —
  urrency conversionホットスポットはそれ自体はユニットテスト済み。

## アラート

`PropFirmAlertNotifier`（`src/Infrastructure/PropFirm`）は合格/違反/警告ドメインイベントを購読
（`IDomainEventHandler<>`として登録、`SaveChanges`成功後にディスパッチ）、
構造化alert/監査証跡（`LogMessages`）を通じてユーザーに通知。
ライブUIは同じステータス変更を反映。これはcross-context reaction —
 决してchallenge集約を突然変異しない。

## API（`/api/prop-firm`、フィーチャ`PropFirm`、ロールUser+）

| メソッド | ルート | 目的 |
|--------|-------|---------|
| GET | `/challenges` | ユーザーのchallenge列表（kind、phase、status、ライブエクイティ、リース） |
| GET | `/challenges/{id}` | 1つのchallenge |
| GET | `/templates` | 作成ダイアログの業界プリセット |
| POST | `/challenges` | テンプレートからまたは完全にカスタムルールセットで作成 |
| POST | `/challenges/{id}/start` | 追跡再開（Stopped → Active） |
| POST | `/challenges/{id}/stop` | 追跡停止（Active → Stopped、リース解放） |
| POST | `/challenges/{id}/equity` | エクイティスナップショットを記録 → 再評価（手動/ライブフィードなしパス） |
| DELETE | `/challenges/{id}` | ソフト削除（Active中はブロック） |

MCP：`Mcp/Tools/PropFirmTools.cs`はリスト/作成（テンプレートから）/記録エクイティ/開始/停止を、
`PropFirm` featureでゲートされて公開。

UI：`/prop-firm`（ナビ*Prop Firm*、`PropFirm`フラグでゲート）は
**Start/Stop/Delete**行アクション（Stopped時はStart、Active時はStop、Active中はDeleteが無効）
でchallengeを列表、`NewPropFirmChallengeDialog`（テンプレートピッカー + 完全ルールエディタ）で作成。
すべての作成/編集はMudBlazorダイアログ経由。

## ライブエクイティフィード — 解決済み

以前の「ライブアカウントP&Lフィードなし」ギャップがクローズ：`App:PropFirm:Enabled`が設定された場合、
ノードはOpen APIでアカウントをライブで追跡、自動的にエクイティを供給。
それなし（デフォルト）で、ドメインと**手動エクイティ**パス（`POST …/equity`）は変更なしで実行 —
 ビルド/テスト/E2EにcTrader認証情報不要。

## テスト

- **ユニット** — `UnitTests/PropFirm/`：
  `PropFirmChallengeTests`（フェーズ進行、最低日、static/trailing drawdown、日次損失、
  terminal/out-of-orderガード）;
  `PropFirmChallengeRulesTests`（balance vs equity日次損失basis、trailing-threshold-dollar trail+lock、
  consistency block/allow、タイムリミット、不活性、最大エクスポージャー、週末、ニュース、
  stop/resume、リース境界、合格でリース解放、ドローダウン警告）;
  `PropFirmValueObjectTests`（VO範囲 + ルールVO数学）;
  `PropFirmEquityCalculatorTests`（ロング/ショートP&L、スワップ/手数料、quote→deposit変換、
  欠落 pricing）;
  `PropFirmTrackingHostTests`（ライブエクイティが拡張されたfake sessionに対して合格/不合格を駆動）;
  `PropFirmAlertNotifierTests`。
  時刻明示的 / `FakeTimeProvider` — wall-clock読み取りなし。
- **統合** — `IntegrationTests/`：
  `PropFirmChallengePersistenceTests`（ラウンドトリップ + 記録エクイティ + ソフト削除、
  enrichedルール + リースラウンドトリップ）と
  `PropFirmTrackingLeaseTests`（クレーム、競合リース、期限切れ後の回収、2つのノードID間）を
  実際のPostgresで実行。
- **E2E** — `E2ETests/PropFirmTests.cs`：作成 + 記録エクイティで`Passed`へ;
  stop→start→違反フロー; テンプレートエンドポイント。
- **ストレス / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`：
  多くの混合ルールchallenge 전반で、シードされたランダム化されたエクイティ/アクティビティ
  ストリーム（日次ロール、急騰、暴落、重複 + 順序違いスナップショット、
  エクスポージャー/週末/ニュース）、粘着的なexactly-once终端状態をアサート、
  peak-bounds-current不変量、根拠のある失敗。

## 設定（`App:PropFirm`）

`Enabled`（デフォルトオフ）、`ReconcileInterval`、`EquityPollInterval`、`LeaseTtl`、
`DrawdownWarnThresholdPercent`、`NodeName`。
