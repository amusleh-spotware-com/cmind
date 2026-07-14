---
slug: /for-traders
title: cTrader トレーダー向け cMind
description: cTrader トレーダーが cMind を自己ホストするべき理由 — スタックとデータを所有し、1 つの AI 搭載コンソールでノートパソコン、VPS、電話上の cBot をオーサリング、バックテスト、実行、監視します。
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cTrader トレーダー向け cMind 📈

cTrader で既にトレードしています。コードエディタ、バックテスター、VPS、3 つのブラウザタブで既にやりくりしています。**cMind はすべてをあなたが自分で実行する 1 つの暗いキーボードフレンドリーコンソールに凝縮します** — そしてオープンソースなので、あなたのエッジ、戦略、認証情報についての何もあなたのボックスを離れることはありません。

:::tip[TL;DR]
ノートパソコン、安い VPS、またはホームサーバーに cMind を自己ホストしてください。1 つの場所で cBot をオーサリング、バックテスト、実行、監視し、AI コアが雑務を処理します。→ [5 分で実行する](./deployment/local.md)
:::

## ホストされたサービスではなく自己ホストする理由は何ですか？

- **スタックとデータを所有してください。** cBot、認証情報、トークン、エクイティ履歴は**あなた**のインフラストラクチャ上に存在します — 第三者なし、ロックインなし、「この製品を日没させています」というメール。
- **本当にあなたが変更する。** C# 14 / .NET 10、厳密な DDD、EF Core + PostgreSQL、MCP サーバー — すべてオープンソースでハッキング可能です。フォークして、拡張して、PR を送信してください。
- **機能ごとのペイウォールなし。** 任意のプロバイダーに独自の AI キーを持参してください。すべての AI 機能は有効です。

サーバーを自分で実行したくないですか？ホスティング会社はあなたのために管理 cMind を実行できます —
[クラウド & VPS プロバイダー向け](./for-cloud-providers.md)を参照してください。

## 1 つのコンソール、タブ切り替えなし

- **オーサリング**本物の Monaco IDE（VS Code エディタ）で、C# **と** Python テンプレート、サンドボックス化された `dotnet build` が一時的なコンテナに含まれています。→ [ビルドとバックテスト](./features/build-and-backtest.md)
- **バックテスト**ノード群を横切って、エクイティカーブがライブでストリームバックされるのを見てください。
- **実行**戦略をライブで実行し、1 つのダッシュボード から**監視**してください。→ [ダッシュボード](./features/dashboard.md)
- **コピー**マスターアカウントをブローカーと cTrader ID 間の多くのアカウントにコピーし、接続の切断とトークンのローテーションに耐える統調を行います。→ [コピートレード](./features/copy-trading.md)

## 小さな話ではなく雑務をするAI

独自の API キーを持参してください（サポートされている任意のプロバイダー — クラウドまたはローカルモデル）して、平文英語 → 自己修復ループ、パラメーター調整、バックテスト事後分析、暴れているボットを自動停止できるリスク保護を備えた本物のコンパイル cBot を取得してください。→ [AI コアに会う](./features/ai.md)

## 制度等級の工具、1 つのため

デスク が支払う同じ厳格さ、あなた自身のボックス上:

- [バックテスト整合性](./features/backtest-integrity.md) · [ポジションサイジング](./features/position-sizing.md)
- [戦略の健全性](./features/strategy-health.md) · [レジームラボ](./features/regime-lab.md)
- [実行 TCA](./features/execution-tca.md) · [トレーディングジャーナル](./features/trading-journal.md)
- [エージェントスタジオ](./features/agent-studio.md) · [逆張りポジショニング](./features/contrarian-positioning.md)

## あなたがいるところで実行

`docker compose up` でノートパソコンで始めて、準備ができたら安い VPS またはホームサーバーに卒業し、携帯電話からボットを確認してください — cMind はインストール可能なモバイルファースト[PWA](./features/pwa.md)です。→ [ローカルで実行する](./deployment/local.md)

AI クライアントがそれを駆動したいですか？ 組み込みの[MCP サーバー](./features/mcp.md)があります。

## より良くするのを手伝ってください

cMind はオープンソースで MIT ライセンスされています — ロードマップはコミュニティ形成です:

- 問題と機能リクエストをファイルし、重要な内容に投票してください。
- cBot テンプレート、AI プロバイダーアダプター、または UI 翻訳を追加してください。
- PR を送信してください — 3 つのテストティア（ユニット + 統合 + E2E）と厳密な DDD がバーを高く保ち、[貢献ガイド](./contributing.md)があなたを案内します。

準備はいいですか？ → [イントロを読む](./intro.md)その後[ローカルで実行する](./deployment/local.md)。
