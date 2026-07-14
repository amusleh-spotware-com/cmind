---
slug: /contributing
title: 貢献
description: cMind に貢献 — 人間 または AI 支援 PR ようこそ。初 貢献 10 分。
sidebar_position: 5
---

# cMind への貢献 🛠️

ここに いてくれてありがとう。cMind 良くなる すべての時 誰か issue を開く、報告 正確 cTrader 動作、これらの正確 ドキュメント タイプの typo を修正、または PR を船。**あなたは .NET ウィザード である必要はありません** — テスター、トレーダー、ドキュメント フィッサー 値打ちある アグリゲート 書く人 同様。

:::tip[正規ガイド リポで生きる]
このページ 親切 オンランプ。完全、常 最新 プロセス — 基礎ルール、コーディング慣例、レビュー フロー — **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**。
:::

## あなたの初 貢献 ~10 分

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 警告、または CI 丁寧に拒否
dotnet test           # unit + integration + E2E
```

何か修正 見つけた? ブランチ、変更、テスト追加、PR を開く。それが 全ループ。

## ヘルプの方法 (それら すべて コードではない)

| 貢献 | 努力 | どこ |
|---|---|---|
| 🐛 バグレポート 再現可能 | 10 分 | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 機能提案 | 10 分 | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 これらドキュメント 改善 | 15 分 | `website/docs/` 編集 + PR |
| 🧪 欠落テスト 追加 | 30 分 | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 報告 正確 cTrader 動作 | 10 分 | [Discussion を開く](https://github.com/amusleh-spotware-com/cmind/discussions) |

## ハウスルール (短版)

cMind 移動 **実マネー**、そのため いくつか ノンネゴシアブル — + 正直に、作る コードベース 喜び:

- **厳密 ドメイン駆動設計。** ビジネスロジック ライブ 集約 + 値オブジェクト、決してエンドポイント + UI。(友好的 プレイブック リポ。)
- **3 テストティア、すべて変更。** Unit + integration + E2E、*含む* 失敗パス (切断接続、拒否オーダー、デッドノード)。グリーンテスト 入場料。
- **ゼロ警告。** `TreatWarningsAsErrors=true`。モダン C# 14。
- **なし シークレット、なし マジック文字列、決して `DateTime.UtcNow`** (代わりに`TimeProvider`注入)。
- **ドキュメント 同じコミット。** 動作変更 → ドキュメント更新。はい、このサイト含む。

完全詳細、各ルール背後 *なぜ*、[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) + [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md)。

## AI で貢献 🤖

我々 本当に歓迎 **AI 支援 PR** — このプロジェクト 作成 人間 + エージェント 両者 作業される。あなたが ドライブしているなら Claude、Copilot、または 類似: ポイント [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md)、ネストされ読む `CLAUDE.md` ファイル、+ ホールド 同じバー (テスト、ゼロ警告、DDD)。良い AI PR 良い人間PR と区別不可 — 同じレビュー、同じようこそ。

## エクセレント 互いに

我々 [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md) を持つ。要点: 親切、良い信仰 を仮定、記憶 人 (または人エージェント) 他の終わり。早期に質問 — それ 強み、煩い ではなく。

ようこそ。我々 見れなくて 我慢できない あなたが構築 何を。🎉
