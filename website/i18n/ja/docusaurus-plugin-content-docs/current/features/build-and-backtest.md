---
description: "ブラウザ内蔵のMonaco IDEからcTraderのcBot（C#とPython、どちらも.NET）をビルド、実行、バックテストし、公式のghcr.io/spotware/ctrader-consoleイメージ上で実行します。"
---

# cBotのビルドとバックテスト

ブラウザ内蔵のMonaco IDEからcTraderのcBot（C# **かつ** Python、どちらも.NET）をビルド、実行、
バックテストし、公式の`ghcr.io/spotware/ctrader-console`イメージ上で実行します。

## ビルド

- **Builder**ページはMonacoエディタをホストします。`CBotBuilder`は**使い捨てコンテナ**内で
  `dotnet build`を用いてプロジェクトをコンパイルします（`AppOptions.BuildImage`、作業ディレクトリは
  `/work`にバインドマウント）。そのため信頼できないユーザーのMSBuildターゲットはホストに到達できません。
  NuGetのリストアは共有ボリュームを介してビルド間でキャッシュされます。Webホストには
  Dockerソケットへのアクセスが必要です。
- C# + Pythonのスターターテンプレートは`src/Nodes/Builder/Templates/`にあります。

## 実行とバックテスト

- **Instances** = TPH状態階層（`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`）。遷移はエンティティを置き換え（idが変わる）、
  コンテナidは引き継がれます。
- `NodeScheduler`は最も負荷の低い適格なノードを選択します。`ContainerDispatcherFactory`は
  リモートノードのHTTPエージェントまたはローカルのDockerディスパッチャーにルーティングします。
- 完了ポーラーは終了したコンテナを照合します（バックテストコンテナは`--exit-on-stop`で自己終了）。
  レポートあり → 完了（`ReportJson`を保存）、なし → 失敗。
- ライブのコンテナログはSignalR経由でブラウザにストリーミングされます。バックテストの
  エクイティカーブはレポートから解析されてチャート化されます。

## cTrader Console CLIに関する注意

バックテストには`--data-mode`（デフォルト`m1`）、`dd/MM/yyyy HH:mm`形式の日付、そして
`params.cbotset`のJSON位置引数が必要です。`run`は`--data-dir`を拒否します（バックテスト専用）。
`ContainerCommandHelpers`を参照してください。

## ノードとスケール

実行キャパシティはノードエージェントを追加する（自己登録 + ハートビート）ことでスケールします。
[ノードディスカバリー](../operations/node-discovery.md)と[スケーリング](../deployment/scaling.md)を参照してください。
## トレーディングアカウントが必要です

cBotの実行やバックテストには、接続先のcTraderトレーディングアカウントが必要です。
**Trading accounts**の下でアカウントを追加するまで、**Run New cBot** / **Backtest New cBot**
ボタンは（ツールチップ付きで）無効になり、ページにはアカウント設定へのリンクを含むプロンプトが
表示されます。アカウントのないボットから生の`stream connect failed`エラーに遭遇することは
もうありません。
