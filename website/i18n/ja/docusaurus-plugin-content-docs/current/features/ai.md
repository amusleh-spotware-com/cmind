---
description: "cMind AI はプロバイダー不可知 — Anthropic、OpenAI、Azure OpenAI、Google Gemini、任意の OpenAI 互換エンドポイント（ローカルモデルを含む Ollama、LM Studio、vLLM）。プロバイダー、モデル、エンドポイントを選択します。すべての AI 機能が変更されずに機能します。"
---

# AI 機能

cMind の AI レイヤーは**プロバイダー不可知**です。すべての機能は単一のプロバイダー中立シーム（`IAiClient.CompleteAsync`）と通信します。**ルーティングクライアント**はアクティブなプロバイダー認証情報を解決し、一致するワイヤーアダプターにディスパッチします。プロバイダー + モデル + エンドポイント（そしてプロバイダーが必要な場合、キー）を選択します。すべての既存の機能は同じゲーティング、暗号化、復元力、および低下により変更なしで機能します。

**バッテリー付属:** **組み込みのローカル LLM がアプリと共に出荷され、デフォルトで有効**です。
（Microsoft.ML.OnnxRuntimeGenAI、例：Phi-3-mini）— したがって、すべてのデプロイメントには **API キーと外部サービスなしで**機能する AI があります。ホワイトラベルデプロイメントはそれを削除し、ユーザーが追加できるプロバイダーを制限できます。組み込みの以上に、任意の外部プロバイダーに接続します。

サポートされているプロバイダー:

- **組み込みのローカル AI**（`BuiltInOnnx`）— インプロセス ONNX GenAI モデル、キーなし、出荷済み+デフォルト有効。
- **Anthropic**（Claude — Messages API）
- **OpenAI** および **Azure OpenAI**（Chat Completions）
- **Google Gemini**（`generateContent`）
- **任意の OpenAI 互換エンドポイント**。**ローカルモデル**（Ollama、LM Studio、vLLM、
  llama.cpp `server`、LocalAI）と OpenAI 互換クラウド（OpenRouter、Groq、Together、Mistral、
  DeepSeek）を含む — すべて 1 つの OpenAI 互換アダプタ経由。ベース URL + モデル + キーのみが異なります。

ちょうど**1 つ**のプロバイダーが一度に アクティブです。認証情報は**暗号化**されて保存されます。
（`AiProviderCredential` アグリゲート + `IAiProviderStore` + `ISecretProtector`、`EncryptionPurposes.AiApiKey`）。ローカルエンドポイント**キーが必要ありません**。アクティブなプロバイダーが**ない**場合、すべての機能は無効な結果を返し、アプリの残りは変更されません（プラットフォームをビルド、テスト、または実行するためにキーが必要ありません）。

**後方互換性:** 既存のデプロイメントのレガシー `App:Ai:ApiKey`（または古い暗号化 `ai.api_key` 設定）は、デフォルトアクティブ **Anthropic** プロバイダーとして自動的に尊重されます — アクションが必要ありません。

AI 未設定 → AI ページはアクション を暗くし、バナーと **Settings → AI**（`AiFeatureNotice`）にプロバイダーを追加するための 1 回限りのプロンプトを表示します。`GET /api/ai/status`（`{ enabled, kind, model }`）でステータス。プロバイダーは（所有者のみ）`GET/PUT /api/ai/providers`、`POST /api/ai/providers/{id}/activate`、
`DELETE /api/ai/providers/{id}`、および `POST /api/ai/providers/test` 接続ピング経由で管理されます。

## デプロイメントデフォルト対ユーザー自身のプロバイダー

AI 認証情報には 2 つのスコープがあります:

- **デプロイメントデフォルト（所有者管理）。** 所有者はプロバイダーを設定します（または `App:Ai:Providers[]` / レガシー `App:Ai:ApiKey` 経由で 1 つを配信します）。すべてのユーザーの**共有デフォルト**になります — したがって、ブローカーまたはホスティングプロバイダーは、**ユーザーごとのセットアップもユーザーごとの制限もなく、すべてのユーザーの AI に資金を供給できます**。上記の所有者のみの `/api/ai/providers` ルート経由で管理されます。
- **ユーザー自身のプロバイダー（セルフサービス）。** 署名されたユーザーは `GET/PUT /api/ai/my-providers`、`POST /api/ai/my-providers/{id}/activate`、
`DELETE /api/ai/my-providers/{id}` の下で独自のプロバイダーを追加できます。存在する場合、彼らの**独自のアクティブなプロバイダーはデプロイメントデフォルトをオーバーライドします**。削除するとデフォルトにフォールバックします。

**解決順序**（`AiProviderStore`、リクエストユーザーあたり）: ユーザー自身のアクティブな認証情報 → デプロイメントデフォルト → レガシー設定キー → なし（AI 無効）。正確に 1 つの認証情報は**スコープごと**にアクティブです（`OwnerUserId` ごとの部分一意インデックス）。各スコープは独立して解決されるため、ユーザーが独自のキーをアクティブ化しても、共有デフォルトは影響を受けません。バックグラウンド/Web 以外のコンテキスト（リクエストユーザーなし）は常にデプロイメントデフォルトを解決します。

## プロバイダー機能マトリックス

機能はプロバイダーごとにデフォルトで、所有者がオーバーライド可能です。機能がオフの場合、機能は**低下し、例外をスローしません**: Web 検索は無音で削除されます。Vision は型付けされた機能がサポートされていない失敗を返します。

| プロバイダー | 種類 | デフォルトベース URL | キー必須 | Web 検索 | ビジョン | メモ |
|---|---|---|---|---|---|---|
| 組み込みローカル AI | `BuiltInOnnx` | なし（インプロセス） | いいえ | ✖ | ✖ | 出荷済み ONNX GenAI モデル、デフォルト有効 |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | はい | ✅ | ✅ | Messages API、`web_search` ツール |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | はい | オプト-イン | オプト-イン | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | はい | ✅ | ✅ | デプロイメントパス + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | はい | ✅ | ✅ | `generateContent`、`google_search` グラウンディング |
| Ollama（ローカル） | `OpenAiCompatible` | `http://localhost:11434/v1/` | いいえ | ✖ | モデル依存 | OpenAI 互換アダプタ経由 |
| LM Studio（ローカル） | `OpenAiCompatible` | `http://localhost:1234/v1/` | いいえ | モデル依存 | モデル依存 | OpenAI 互換アダプタ経由 |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | あなたのサーブ URL | いいえ | ✖ | モデル依存 | OpenAI 互換アダプタ経由 |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | プロバイダー URL | はい | ✖ | モデル依存 | OpenAI 互換アダプタ経由 |

プロバイダーごとの完全なセットアップガイド（キー、URL、モデル ID、UI ステップ）: [AI プロバイダー — セットアップカタログ](../deployment/ai-providers.md)を参照してください。

## 組み込みローカル AI（出荷済み、デフォルト有効）

cMind は、[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) 経由で**インプロセスで実行される実際のローカル LLM** を出荷します。
（Phi-3-mini などのコンパクト指示モデル）。**API キーと外部サービスが必要ありません**。最初の起動時 — プロバイダーが設定されていないとき、ホワイトラベルゲートがそれを許可するとき — 自動的に**シードおよびアクティブ化**されるため、すべてのデプロイメントは既製で機能する AI を持っています。

- モデルディレクトリ（`genai_config.json` + トークナイザー + ウェイト）は `App:Ai:BuiltIn:ModelPath`（デフォルト `models/onnx`、アプリベースディレクトリに相対）で設定されます。モデルファイルが存在しない場合、プロバイダーは**インストールヒント付きの型付けされた失敗に低下**します — スローされず、アプリの残りは影響を受けません。
- すべてのテキスト AI 機能を強化します。コンパクトモデルであるため、テキストのみ（サーバー側の Web 検索またはビジョンなし）。生成はシリアル化されます（1 つのモデルインスタンス、遅延ロード後に再利用）。
- モデルの取得/バンドル: [AI プロバイダー → 組み込み](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped)を参照してください。

## ホワイトラベル制御

ホワイトラベルデプロイメントは `App:Branding`（すべてのプロバイダーアップサート上で サーバー側で実施）経由で AI を制限します:

- `AllowBuiltInAi`（デフォルト `true`）— `false` に設定して、**組み込みモデルを完全に削除**します。
- `AllowLocalProviders`（デフォルト `true`）— `false` に設定して、ローカル/自己ホスト型エンドポイント（ループバック / プライベート OpenAI 互換、例：Ollama/LM Studio/vLLM）を禁止します。
- `AllowedAiProviderKinds`（デフォルト空 = すべて）— デプロイメントが裁可する種類のみをリストして（例：`["Anthropic","OpenAiCompatible"]`）、ユーザーが追加できるプロバイダーをロックダウンします。

## 拡張: 将来の組み込みモデル

AI レイヤーは**アダプタベースで拡張するように構築**されています。各プロバイダーは `AiProviderKind` で選択された `IAiProvider`。機能向けシーム（`IAiClient`/`AiFeatureService`）は変わることはありません。後で新しい組み込みモデルランタイムを追加するのは（別の ONNX モデル、別のインプロセスエンジン、インプロセス GGUF/llama.cpp など）、ローカル化された変更です: `AiProviderKind` を追加し、1 つの `IAiProvider` アダプターを実装し、登録し、（オプションで）デフォルトシード + ダイアログオプションを配線します — 機能、エンドポイント、MCP ツールの変更なし。組み込み ONNX プロバイダーはこのパターンのリファレンス実装です。

## 機能

- **cBot をビルド** — プレーン英語プロンプト → **生成 → ビルド → AI 修正**自己修復ループ経由で実行可能な cBot（`build-strategy`）、`/ai/build` で。
- **パラメーター最適化** — クローズドループ: AI が param セットを提案し、各セットはノード間で永続化 + バックテストされます（`optimize-run` / `optimize-params`）。
- **自律ポートフォリオエージェント** — マンデート駆動提案、完全な決定ジャーナル（`AgentMandate` → `AgentProposal`）。
- **リスク保護エージェント** — `AiRiskGuard` バックグラウンドサービスは実行中のボットを評価し、重大なリスク時に**自動停止**できます（オプトイン）。
- **Prop-firm 露出保護者** — ドローダウン/露出制限および自動フラッテン。
- **マーケットアラート** — プロバイダーがサポートする場合、AI センチメント（Web 検索グラウンディング）を備えた `AlertRule` エンジン。
- **分析** — cBot レビュー、バックテスト分析、事後分析、マーケットセンチメント、チャートビジョンデザイン、マーケットプレイスキュレーション。

## サーフェス

- `/api/ai/*`の下の Web エンドポイント（ビルド戦略、生成プロジェクト、レビュー、バックテスト分析、パラメーター最適化、最適化実行、事後分析、センチメント、ビジョン、キュレート、...）。
- AI クライアント用 MCP ツール（`AiTools`）— [mcp.md](mcp.md)を参照してください。プロバイダー選択は MCP クライアントに対して透過的です。
- **AI** ナビグループ — 機能あたり 1 つの Blazor**ページ**: cBot をビルド（`/ai/build`）、レビュー（`/ai/review`）、議論（`/ai/debate`）、マーケットセンチメント（`/ai/sentiment`）、露出チェック（`/ai/exposure`）、ポートフォリオダイジェスト（`/ai/digest`）、チューンアドバイザー（`/ai/tune`）、最適化（`/ai/optimize`）、ポートフォリオエージェント、アラート、MCP キー。ページは `AiFeaturePageBase` + `AiOutputPanel` を共有; プロバイダーが設定されていない場合、それぞれが `AiFeatureNotice` を表示します。
- **Settings → AI**（`/settings/ai`、所有者のみ）— **プロバイダー追加/編集ダイアログ**を備えたプロバイダーリスト（種類、Ollama/LM Studio localhost プリセットを含むキーごとのヒント付きベース URL、モデル、オプションキー、機能トグル、「アクティブに設定」）および**接続テスト**ボタン。

## 設定

`App:Ai` は、レガシー単一キーとマルチプロバイダーシード の両方をサポートします:

- レガシー: `ApiKey`、`Model`（デフォルト `claude-opus-4-8`）、`BaseUrl`、`MaxTokens` — 依然としてデフォルト Anthropic プロバイダーとして尊重されます。
- マルチプロバイダー: `ActiveProvider`（種類）および `Providers[]`（`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`）— スタートアップ時に認証情報が存在しない場合はストアにインポートされるため、ops チームは純粋に appsettings/env 経由で設定された（ローカル LLM を含む）デプロイメントを出荷できます。

`RiskGuardEnabled`、`RiskGuardAutoStop`、`RiskGuardInterval` は変更されません。テスト/開発の場合、設定キーは [dev-credentials ファイル](../testing/dev-credentials.md)の統一の下に `Ai` にあります。

## 信頼性

プロバイダーは信頼できないものとして扱われます — それが行うことは何もアプリを停止できません。これはクラウドとローカルエンドポイントで同じに保持されます（デッドの Ollama は throttled Anthropic と同じように再試行して低下します）:

- **グレースフルデグラデーション。** すべての失敗モード（プロバイダーなし、HTTP 4xx/5xx/429、タイムアウト、不正な形式のボディ、空のコンテンツ、サポートされていない機能）は型付けされた `AiResult.Fail(reason)` を返します — クライアントはページ、MCP ツール、またはホストされたサービスにスローしません。
- **復元力パイプライン。** `AddAiHttpClient` は共有 AI `HttpClient` に、一時的な 5xx /ネットワーク障害（指数バックオフ + ジッター）と試行ごとの寛大なタイムアウト + 合計タイムアウト（`AiHttp`）に有界再試行を与え、すべてのアダプタで再利用されます。

## 偽のローカル LLM でテスト

AI レイヤーは `FakeLocalLlmServer` によって**外部依存なしで**エンドツーエンドで証明されます — 決定的なカンニング答えを返す小さなインプロセス **OpenAI 互換**エンドポイント、Ollama/LM Studio/vLLM と配線同一。それは次の通りです:

- **ユニット** — アダプター毎のリクエスト翻訳 + レスポンスパーステスト、ルーティング/機能低下。
- **統合** — OpenAI 互換アダプターエンドツーエンド、すべてのアダプター間のパラメーター化された復元力理論、および**MCP AI ツール**。
- **E2E** — `AiLocalFixture` は偽のサーバーを指すアプリをブート（または開発者が `AI_E2E_BASEURL`（+ オプション `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`）を設定するとき、**実際の**プロバイダー — 実際の creds が勝つ）して、実際の UI を通じてすべての AI 機能を駆動します。任意の AI 機能を追加または変更するには、このフィクスチャを通じた E2E テスト**が必要**です（リポジトリテスト命令を参照）。オプト-イン レーン（`AI_LOCAL_LLM=1`）は、**Ollama** Testcontainer を通じて 1 つの実際の完了を実行します。
