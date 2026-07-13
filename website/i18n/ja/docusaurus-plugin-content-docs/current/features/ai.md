---
description: "cMind AIはプロバイダー非依存 — Anthropic、OpenAI、Azure OpenAI、Google Gemini、OpenAI互換エンドポイント（ローカルモデル含む）。プロバイダ、モデル、エンドポイントを選択すれば、すべてのAI機能が同じゲーティング、暗号化、レジリエンス、低下で動作します。"
---

# AI機能

cMindのAIレイヤーは**プロバイダー非依存**です。すべての機能は単一のプロバイダー中和的な接着剤
（`IAiClient.CompleteAsync`）と通信し、**ルーティングクライアント**がアクティブなプロバイダー
認証情報を解決して、一致するWireアダプターにディスパッチします。プロバイダ + モデル +
エンドポイント（および必要に応じてキー）を選択すれば、既存のすべての機能が同じゲーティング、暗号化、
レジリエンス、低下で動作します。

**組み込み済み：** **組み込みローカルLLMがアプリにバンドルされデフォルトで有効**です
（Microsoft.ML.OnnxRuntimeGenAI、例：Phi-3-mini） — したがって、すべてのデプロイが**APIキー不要、
外部サービスなしで動作するAI**を備えています。ホワイトラベルデプロイはそれを削除して、ユーザーが
追加できるプロバイダを制限できます。組み込み以外にも、任意のプロバイダに接続できます。

対応プロバイダー：

- **組み込みローカルAI**（`BuiltInOnnx`）— インプロセスのONNX GenAIモデル、キー不要、船載 + デフォルトオン。
- **Anthropic**（Claude — Messages API）
- **OpenAI** および **Azure OpenAI**（Chat Completions）
- **Google Gemini**（`generateContent`）
- **任意のOpenAI互換エンドポイント**（ローカルモデル含む）（Ollama、LM Studio、vLLM、
  llama.cpp `server`、LocalAI）およびOpenAI互換クラウド（OpenRouter、Groq、Together、Mistral、
  DeepSeek） — すべて1つのOpenAI互換アダプター経由、ベースURL + モデル + キーのみで異なる。

同時に**1つの**プロバイダーのみがアクティブです。認証情報は**暗号化されて保存**
（`AiProviderCredential`集約 + `IAiProviderStore` + `ISecretProtector`、`EncryptionPurposes.AiApiKey`）;
ローカルエンドポイントには**キー不要**。アクティブなプロバイダー**なし**の場合、すべての機能は
無効結果を返し、アプリの残りの部分は変更なしで動作します（プラットフォームのビルド、テスト、実行にキーは不要）。

**後方互換性：** 既存のデプロイのレガシー`App:Ai:ApiKey`（または古い暗号化された`ai.api_key`設定）は
自動的にデフォルトのアクティブ**Anthropic**プロバイダーとして認識されます — ゼロアクション。

AI未設定 → AIページはアクションを淡色表示し、バナーと**Settings → AI**でプロバイダを追加する
ワンタイムプロンプトを表示（`AiFeatureNotice`）。ステータス：`GET /api/ai/status`
（`{ enabled, kind, model }`）; プロバイダ管理（所有者専用）は`GET/PUT /api/ai/providers`、
`POST /api/ai/providers/{id}/activate`、`DELETE /api/ai/providers/{id}`、
`POST /api/ai/providers/{id}/test`（接続テスト）を介して管理。

## デプロイデフォルト vs ユーザーのプロバイダ

AI認証情報には2つのスコープがあります：

- **デプロイデフォルト（所有者管理）。** 所有者がプロバイダを設定（または
  `App:Ai:Providers[]` / レガシー`App:Ai:ApiKey`経由で船載）。これは**すべてのユーザーの共有デフォルト**
  になります — ブローカーまたはホストプロバイダーが**ユーザーごとのセットアップとユーザーごとの制限なしで**
  すべてのユーザーのAIに資金を提供できます。所有者のみ`/api/ai/providers`ルートで管理。
- **ユーザーのプロバイダ（自己管理）。** サインインしたユーザーは`GET/PUT /api/ai/my-providers`、
  `POST /api/ai/my-providers/{id}/activate`、`DELETE /api/ai/my-providers/{id}`で自分のプロバイダを
  追加できます。存在する場合、**自分のアクティブなプロバイダが自分のAI機能のデプロイデフォルトを**
  オーバーライドします; 削除するとデフォルトにフォールバック。

**解決順序**（`AiProviderStore`、リクエストユーザーごと）：ユーザーのアクティブな認証情報 →
デプロイデフォルト → レガシー設定キー → なし（AI無効）。各スコープで正確に1つの認証情報が
アクティブで（`OwnerUserId`ごとの部分的一意インデックス）、各スコープは獨立して解決されるため、
ユーザーが自分のキーをアクティブにしても共有デフォルトを乱すことはありません。
バックグラウンド/非Webコンテキスト（リクエストユーザーなし）は常にデプロイデフォルトを解決。

## プロバイダ機能マトリックス

機能はプロバイダーごとにデフォルトがあり、所有者がオーバーライド可能。機能がオフの場合、
**フィーチャは低下し、決してスローしません**：ウェブ検索はサイレントにドロップ;
ビジョンは型付き機能未サポート失敗を返します。

| プロバイダ | Kind | デフォルトベースURL | キー要 | ウェブ検索 | ビジョン | 備考 |
|---|---|---|---|---|---|---|
| 組み込みローカルAI | `BuiltInOnnx` | n/a（インプロセス） | 不要 | ✖ | ✖ | 船載ONNX GenAIモデル、デフォルトオン |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | 要 | ✅ | ✅ | Messages API、`web_search`ツール |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | 要 | オプトイン | オプトイン | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | 要 | ✅ | ✅ | デプロイパス + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | 要 | ✅ | ✅ | `generateContent`、`google_search`グリンディング |
| Ollama（ローカル） | `OpenAiCompatible` | `http://localhost:11434/v1/` | 不要 | ✖ | モデル依存 | OpenAI互換アダプター経由 |
| LM Studio（ローカル） | `OpenAiCompatible` | `http://localhost:1234/v1/` | 不要 | モデル依存 | モデル依存 | OpenAI互換アダプター経由 |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | サーバーURL | 不要 | ✖ | モデル依存 | OpenAI互換アダプター経由 |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | プロバイダURL | 要 | ✖ | モデル依存 | OpenAI互換アダプター経由 |

プロパイダごとの設定ガイド（キー、URL、モデルID、UI手順）：[AIプロバイダー — 設定カタログ](../deployment/ai-providers.md)を参照。

## 組み込みローカルAI（船載、デフォルトオン）

cMindは[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/)経由で**インプロセスで
動作する実際のローカルLLM**をバンドルしています（Phi-3-miniなどのコンパクトなインструкクションモデル）。
**APIキー不要、外部サービス不要**で、最初の起動時 — プロバイダが設定されておらず、
ホワイトラベルゲートが許可している場合に — **自動的にシードおよびアクティブ化されます**、
そのためすべてのデプロイが箱から出してすぐに動作するAIを備えています。

- モデルディレクトリ（`genai_config.json` + トークナイザ + 重み）は
  `App:Ai:BuiltIn:ModelPath`で設定（デフォルト`models/onnx`、アプリベースディレクトリからの相対パス）。
  モデルファイルが存在しない場合、プロバイダは**型付き失敗とインストールヒントに低下します** —
  スローすることはなく、アプリの残りに影響しません。
- すべてのテキストAI機能を強化します。コンパクトなモデルであるため、テキストのみ
  （サーバーサイドのウェブ検索またはビジョンなし）、生成はシリアル化されます
  （1つのモデルインスタンス、遅延読み込み後に再利用）。
- モデルの取得/バンドル：[AIプロバイダー → 組み込み](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped)を参照。

## ホワイトラベルコントロール

`App:Branding`（サーバーサイドで各プロバイダ upsert に適用）を通じてAIを制限：

- `AllowBuiltInAi`（デフォルト`true`）— `false`に設定すると**組み込みモデルを完全に削除**。
- `AllowLocalProviders`（デフォルト`true`）— `false`に設定するとローカル/自己ホストエンドポイント
  （ループバック/プライベートOpenAI互換、例：Ollama/LM Studio/vLLM）を禁止。
- `AllowedAiProviderKinds`（デフォルト空 = すべて）— デプロイが承認するkindのみを列表
  （例：`["Anthropic","OpenAiCompatible"]`）、ユーザーが追加できるプロバイダを制限。

## 拡張：将来の組み込みモデル

AIレイヤーは**成長するようにアダプター 기반으로構築**されています。各プロバイダは
`AiProviderKind`によって選択され、feature-facingシーム（`IAiClient`/`AiFeatureService`）は
決して変更されません。後で新しい組み込みモデルランタイムを追加する場合
（別のONNXモデル、別のインプロセ 엔진、GGUF/llama.cppインプロックなど）は、ローカライズされた
変更です： `AiProviderKind`を追加し、1つの`IAiProvider`アダプターを実装して登録し、
（オプションで）デフォルトシード + ダイアログオプションを配線 — 機能、エンドポイント、MCPツールの
変更なし。組み込みONNXプロバイダはこのパターンの参照実装です。

## 機能

- **cBotを構築** — プレーイングリッシュプロンプトから**生成 → 構築 → AI修復**自己修復ループ
  （`build-strategy`）で実行可能なcBotへ、`/ai/build`で。
- **パラメータ最適化** — クローズドループ：AIがパラメータセットを提案し、それぞれが永続化され、
  ノード全体でバックテストされます（`optimize-run` / `optimize-params`）。
- **自律型ポートフォリオエージェント** — フル決定ジャーナル付きのマンデート駆動プロポーザル
  （`AgentMandate` → `AgentProposal`）。
- **Actingリスクガード** — `AiRiskGuard`バックグラウンドサービスが実行中のBOTを評価し、
  重大なリスク時に**自動停止**できます（オプトイン）。
- **Prop-firmエクスポージャーガーディアン** — ドローダウン/エクスポージャー制限と自動フラット化。
- **マーケットアラート** — プロバイダがサポートしている場合、ウェブ検索グリoundedの
  AIセンチメント付きで`AlertRule`エンジンを使用。
- **分析** — cBotレビュー、バックテスト分析、事後分析、マーケットセンチメント、
  チャートビジョン設計、マーケットプレイスキュレーション。

## サーフェス

- `/api/ai/*`下のWebエンドポイント（build-strategy、generate-project、review、analyze-backtest、
  optimize-params、optimize-run、post-mortem、sentiment、vision、curate、…）。
- AIクライアント向けMCPツール（`AiTools`）— [mcp.md](mcp.md)を参照。
  プロバイダ選択はMCPクライアントに対して透過的。
- **AI** ナビグループ — 機能ごとのBlazor**ページ**：cBotを構築（`/ai/build`）、
  レビュー（`/ai/review`）、デベート（`/ai/debate`）、マーケットセンチメント（`/ai/sentiment`）、
  エクスポージャーチェック（`/ai/exposure`）、ポートフォリオ摘要（`/ai/digest`）、
  チューンアドバイザー（`/ai/tune`）、最適化（`/ai/optimize`）、+
  ポートフォリオエージェント、アラート、MCPキー。ページは`AiFeaturePageBase` + `AiOutputPanel`を共有;
  プロバイダが設定されていない場合、各ページは`AiFeatureNotice`を表示。
- **Settings → AI**（`/settings/ai`、所有者のみ）— プロバイダリスト +
  **プロバイダ追加/編集ダイアログ**（kind、ベースURL（kindごとのヒント含む、Ollama/LM Studioの
  localhostプリセット含む）、モデル、オプションのキー、機能トグル、「アクティブに設定」）+
  **接続テスト**ボタン。

## 設定

`App:Ai`はレガシー単一キーとマルチプロバイダー seeding の両方をサポート：

- レガシー：`ApiKey`、`Model`（デフォルト`claude-opus-4-8`）、`BaseUrl`、`MaxTokens` —
  まだデフォルトのAnthropicプロバイダーとして認識されています。
- マルチプロバイダー：`ActiveProvider`（kind）および`Providers[]`
  （`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`） —
  認証情報がまだ存在しない場合、起動時にストアにインポートされるため、
  オプションチームはappsettings/envのみで（ローカルLLMを含む）設定されたデプロイを船載できます。

`RiskGuardEnabled`、`RiskGuardAutoStop`、`RiskGuardInterval`は変更なし。
テスト/開発用の設定キーは`/api/ai/providers`のの下に配置されます。

## 信頼性

プロバイダは信頼できないものとして扱われます — プロバイダが何をする場合でも、
アプリをダウンさせることはできません。これはクラウドとローカルエンドポイントで同一に保持されます
（デッドOllamaはスロットルされたAnthropicと正確に同じ方法でリトライ затем低下）：

- **グレースフル低下。** すべての失敗モード（プロバイダなし、HTTP 4xx/5xx/429、タイムアウト、
  不正なボディ、空のコンテンツ、未サポート機能）は型付き`AiResult.Fail(reason)`を返します —
  クライアントはページ、MCPツール、ホストサービスにスローしません。
- **レジリエンスパイプライン。** `AddAiHttpClient`は、1つの共有AI `HttpClient`に一時的な
  5xx/ネットワーク障害に対するバウンドリトライ（指数バックオフ + ジャター） +
  各試行および合計タイムアウト（`AiHttp`）を与え、すべてのアダプターで再利用されます。

## 	fakeローカルLLMでのテスト

AIレイヤーは、`FakeLocalLlmServer`により**外部依存なしで** end-to-end で証明されます —
  小さなインプロセス**OpenAI互換**エンドポイントでdeterministicな固定replyを返し、
  Ollama/LM Studio/vLLMとwire-identicalです。これは以下をバックアップします：

- **ユニット** — アダプターごとのリクエスト翻訳 + レスポンス解析テスト、ルーティング/機能低下。
- **統合** — OpenAI互換アダプターのend-to-end、すべてのアダプターにわたるパラメータ化された
  レジリエンス理論、MCP AIツール。
- **E2E** — `AiLocalFixture`はアプリを出力先が	fakeサーバー（または開発者が
  `AI_E2E_BASEURL`（+ オプション`AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`）を設定した場合は
  **実際**のプロバイダ — 実際の 자격情報優先）にポインターして起動し、実際のUIを通じて
  すべてのAI機能を駆動します。AI機能を追加または変更する場合は、**このfixtureを通じたE2Eテストが**
  必要です（repoテストマンデートを参照）。
  オプトインレーン（`AI_LOCAL_LLM=1`）は**Ollama** Testcontainerを介して1つの実際の完了を実行します。

## 組み込みローカルAI — デフォルトでゼロ設定

組み込みONNXローカルLLMは箱から出してすぐに動作します：モデルディレクトリが存在せず、
`App:Ai:BuiltIn:AutoDownload`が`true`（デフォルト）の場合、appは一度だけバックグラウンドで
`App:Ai:BuiltIn:DownloadBaseUrl`からモデルをダウンロードします。ダウンロード実行中、AI呼び出し
（およびSettings → AIの**接続テスト**）は明確な「モデルをダウンロード中（初回セットアップ）」メッセージを
返し、ハード障害ではありません。エアギャップ/従量制デプロイは`AutoDownload=false`を設定し、
`App:Ai:BuiltIn:ModelPath`でモデルディレクトリを事前プロビジョニングします。
`App:Branding:AllowBuiltInAi`ゲートはまだ適用されます。
