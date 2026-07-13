---
description: "cMindがサポートするすべてのAIプロバイダーのセットアップカタログ — Anthropic、OpenAI、Azure OpenAI、Google Gemini、およびOllama、LM Studio、vLLM、llama.cpp、LocalAIなどのローカルモデルを含むすべてのOpenAI互換エンドポイント、およびOpenAI互換クラウド。"
---

# AIプロバイダー — セットアップカタログ

cMindのAIレイヤーはプロバイダー非依存です（[AI機能](../features/ai.md)を参照）。プロバイダーを2つの方法で設定します：

1. **UI（オーナー）：** 設定 → AI → **プロバイダーを追加** → 種類を選択、ベースURL、モデル、キー（ローカルの場合はオプション）、機能トグル、**アクティブに設定** → **接続をテスト**。
2. **設定/環境（オペレーション）：** `App:Ai:Providers[]`と`App:Ai:ActiveProvider`をシード — 認証情報が存在しないときに初回起動時にストアにインポート。例（環境、プロバイダーインデックス`0`）：

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (キーレスローカルエンドポイントの場合は省略)
   ```

正確に1つのプロバイダーが一度にアクティブです。キーは暗号化して保存されます。ローカルエンドポイントは何も必要ありません。

## セキュリティ：httpとhttps

プレーンテキスト`http://`は**ローカルホストまたはプライベート（イントラネット）ホストのみ**で受け入れられます。ローカルLLMの場合（Ollama、LM Studio、vLLM、オンプレミスボックス）。パブリックインターネット上でルーティング可能なホスト**は必須**`https://`であり、APIキーはクリアテキストで送信されません。エアギャップ/オンプレミス：ベースURLを内部エンドポイント（ループバックまたはプライベートIP）に指定し、ランタイムが認証されていない場合はキーを空のままにします。

## 組み込みのローカルAI（ONNX、出荷済み）

cMindは、**デフォルトで有効**である**実際のプロセス内ローカルLLM**（Microsoft.ML.OnnxRuntimeGenAI）を出荷します。キーなし、外部サービスなし。初回起動時に、プロバイダーが設定されておらず`App:Branding:AllowBuiltInAi`が`true`の場合、それは自動的にシードおよびアクティベートされます。

- **設定：** `App:Ai:BuiltIn:Enabled`（デフォルト`true`）、`App:Ai:BuiltIn:ModelPath`（デフォルト`models/onnx`、アプリベースディレクトリに対して相対）、`App:Ai:BuiltIn:MaxTokens`（デフォルト`1024`）。
- **モデルファイル：** `ModelPath`をONNX GenAIモデルを含むディレクトリに指定します。`genai_config.json`、トークナイザー、`.onnx`の重み。CPU **Phi-3-mini**ビルドがうまくいきます。例：

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # その後、App:Ai:BuiltIn:ModelPathをそのフォルダに設定します（genai_config.jsonを含む）
  ```

  フォルダをデプロイメントイメージ/Helボリュームにバンドルするか、ランタイムにマウントします。ファイルが存在しない場合、組み込みは明確な「モデルがインストールされていません」メッセージに低下します。アプリはまだ実行されます。別のプロバイダーを設定するか、モデルをインストールしてください。
- **GPU：** CPU パッケージ/モデルをCUDA/DirectML ONNX GenAIビルドに置き換えます。コードパスは変わりません。

## ホワイトラベル：AIの制限

`App:Branding`の下で設定します（サーバー側で実行 — 禁止されたアップサートは`400`を返す）：

- `AllowBuiltInAi: false` — 出荷された組み込みモデルを完全に削除。
- `AllowLocalProviders: false` — ローカル/セルフホストエンドポイント（Ollama/LM Studio/vLLMおよび任意のループバック/プライベートOpenAI互換URL）を禁止。
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — これらの種類のみを許可（空 = すべて）。

## 将来の組み込みモデルの拡張

プロバイダーレイヤーはアダプタベース（`IAiProvider`は`AiProviderKind`でキー付け）であるため、将来の組み込みモデルランタイムはAI機能に触れずに追加されます。種類を追加し、1つのアダプタを実装し、登録します。ONNX組み込みは参照実装です。[AI機能 → 拡張](../features/ai.md#extending-future-built-in-models)を参照。

## クラウドプロバイダー

### Anthropic（Claude）

- キー：<https://console.anthropic.com/> → APIキー。
- ベースURL：`https://api.anthropic.com/` · モデル：例えば`claude-opus-4-8`。
- 機能：ウェブ検索とビジョンはデフォルトで有効。

### OpenAI

- キー：<https://platform.openai.com/api-keys>。
- ベースURL：`https://api.openai.com/v1/` · モデル：例えば`gpt-4o`。
- 種類：**OpenAiCompatible**。ビジョンモデルを使用している場合はダイアログでビジョンを有効にします。

### Azure OpenAI

- キー＋エンドポイント：Azureポータル → Azure OpenAIリソース。
- ベースURL：`https://<resource>.openai.azure.com/` · モデル：デプロイメント**名**。
- 種類：**AzureOpenAi**（`api-key`ヘッダ＋`api-version`クエリとデプロイメントパスを使用）。

### Google Gemini

- キー：<https://aistudio.google.com/app/apikey>。
- ベースURL：`https://generativelanguage.googleapis.com/` · モデル：例えば`gemini-2.0-flash`。
- 種類：**Gemini**。ウェブ検索グラウンディングとビジョンはデフォルトで有効。

### その他のOpenAI互換クラウド（OpenRouter、Groq、Together、Mistral、DeepSeek）

- 種類：**OpenAiCompatible**。ベースURL = プロバイダーのOpenAI互換エンドポイント、モデル = そのモデルid、ApiKey = プロバイダーキー。cMind変更は不要 — 1つのアダプタがすべてに対応します。

## ローカルモデル（キーなし）

すべてのローカルランタイムはOpenAI Chat Completions ワイアを公開するため、**種類：OpenAiCompatible**をランタイムのベースURLと提供モデル名で使用します。キーを空のままにします。

### Ollama

```
# https://ollama.comからインストールしてから：
ollama pull llama3.1:8b
```

- ベースURL：`http://localhost:11434/v1/` · モデル：プルされた名前（例`llama3.1:8b`、`qwen2.5-coder`）。
- APIキーなし。機能はデフォルトではテキストのみ。ビジョンモデルの場合のみビジョンを有効にします。

### LM Studio

- ローカルサーバーを開始します（Developer → サーバーを開始）。
- ベースURL：`http://localhost:1234/v1/` · モデル：ロードされたモデルid。APIキーなし。

### vLLM / llama.cpp `server` / LocalAI

- OpenAI互換エンドポイントを提供します（各が1つを出荷）。
- ベースURL：提供されたURL（例`http://localhost:8000/v1/`）· モデル：提供されたモデル名。認証を前に置かない限りキーなし。

## 検証

- ダイアログの**接続をテスト**は小さなpingコンプリーションを実行し、成功とレイテンシを報告します。ローカルエンドポイントを確認するのに理想的です。
- 自動化：アプリのE2Eスイートはデフォルトではプロセス内フェイクOpenAI互換サーバーに対して、または`AI_E2E_BASEURL`（オプション`AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`）が設定されている場合は実プロバイダーに対して、すべてのAI機能を駆動します。[AI機能 → テスト](../features/ai.md#testing-with-the-fake-local-llm)を参照。

## 切り替え/ローテーション

- **アクティブプロバイダーを切り替え：** 設定 → AI → 別のカード上の**アクティブに設定**（1つをアクティベートすると他のすべてが非アクティベート）。
- **キーをローテーション：** プロバイダーを編集し、新しいキーを指定します（保存されたキーを保持するには空のままにします）。
- **削除：** カードを削除します。アクティブなプロバイダーがない場合、AI機能は無効になり、アプリの残りは変わりなく実行されます。
