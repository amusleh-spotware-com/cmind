---
description: "構築、実行、バックテスト cTrader cBots (C#と Python、両.NET) ブラウザ内 Monaco IDE から、公式 ghcr.io/spotware/ctrader-console イメージ上実行。"
---

# cBots 構築 & バックテスト

cTrader cBots (C# **と** Python、両.NET) ブラウザ内 Monaco IDE から構築、実行、バックテスト、公式 `ghcr.io/spotware/ctrader-console` イメージ上実行。

## 構築

- **ビルダー** ページ Monaco エディタホスト; `CBotBuilder` プロジェクト `dotnet build` **使い捨てコンテナ内** (`AppOptions.BuildImage`、作業ディレクトリバインドマウント `/work`)、信頼できないユーザー MSBuild ターゲット ホストアクセスなし。NuGet リストア キャッシュ共有ボリューム経由ビルド間。Web ホスト Docker ソケット アクセス必要。
- C# + Python スターター テンプレート `src/Nodes/Builder/Templates/` に存在。

## 実行 & バックテスト

- **インスタンス** = TPH 状態階層 (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`)。遷移エンティティ置換 (ID 変更)、コンテナ ID 実行。
- `NodeScheduler` 最も負荷が低い適格ノード選択; `ContainerDispatcherFactory` リモート ノード HTTP エージェント または ローカル Docker ディスパッチャへルーティング。
- 完了ポーラー 終了コンテナ 調整 (バックテスト コンテナ `--exit-on-stop` 経由 自己終了); レポート 存在 → 完了 (`ReportJson` 保存)、無し → 失敗。
- ライブ コンテナ ログ ブラウザへ SignalR ストリーミング; バックテスト エクイティ曲線 レポートから解析 + チャート化。

## cTrader コンソール CLI 注記

バックテスト `--data-mode` 必要 (デフォルト `m1`)、日付 `dd/MM/yyyy HH:mm`、`params.cbotset` JSON 位置引数; `run` `--data-dir` 拒否 (バックテストのみ)。`ContainerCommandHelpers` 参照。

## ノード & スケール

実行容量 ノード エージェント追加でスケール (自己登録 + ハートビート)。[ノード検出](../operations/node-discovery.md) と [スケーリング](../deployment/scaling.md) 参照。
