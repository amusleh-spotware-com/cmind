---
title: アーキテクチャ決定記録
description: cMind 背後 非自明 設計 決定 — コンテキスト、決定、結果 — コード外 読み取る不可。
---

# アーキテクチャ決定記録

これら レコード 設計決定 あなた **コード外 推論不可** — トレードオフ、ロード取られない、なぜ。各短し: *コンテキスト → 決定 → 結果*。新規 構造 決定 → ADR ここ追加 (次数) 次エンジニア (人間または AI) 継承 推論、結果 のみ ではなく。

| # | 決定 |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | 厳密 DDD と純粋 `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | インスタンス状態 TPH; 遷移 エンティティ置換 |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI ノード HTTP + JWT、SSH/シェル なし |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` ウェブホスト でサンドボックスコンテナ で実行 |
| [0005](./0005-anthropic-raw-http.md) | AI クライアント 生HTTP 使用、Anthropic SDK ではなく |
| [0006](./0006-copy-profile-db-lease.md) | コピーホスティング 原子 DB リース コーディネート |
