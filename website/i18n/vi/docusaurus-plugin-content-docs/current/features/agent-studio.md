---
description: "Agent Studio — tạo trading agents theo persona, no-code với character và archetype quản lý tài khoản hướng tới mục tiêu của bạn dưới Autonomy & Safety Kernel (risk envelope, circuit breaker, kill switch, versioned disclaimer consent)."
---

# Agent Studio

Agent Studio cho phép bạn tạo một **trading agent với một character** — không cần code — và giao cho nó quản lý
các tài khoản hướng tới các mục tiêu có thể đo lường. Một agent giống như một cBot theo personality: bạn chọn archetype
và attitude, đặt guardrails, và nó chạy dưới **Autonomy & Safety Kernel**.

Mở **AI → Agent Studio** (`/agent-studio`).

## Tạo một agent

**New agent** dialog collects, no-code:

- **Name** và **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion hoặc Breakout/Momentum. Mỗi preset fix một cadence và posture hợp lý.
- **Attitude** — các thanh trượt aggressiveness, patience và trend-following.
- **Managed account(s)** — **ít nhất một là bắt buộc để tạo agent** (một agent không có tài khoản không bao giờ có thể start, vì vậy *Create* vẫn disabled cho đến khi bạn chọn một). Nếu bạn chưa liên kết tài khoản giao dịch nào, dialog sẽ nói như vậy và hướng bạn liên kết một trước tiên.
- **Autonomy level** — **Advisory** (chỉ propose) hoặc **Approval-gated** (chỉ act sau khi bạn
  per-action approval). **Full Auto** (không per-trade approval) thêm yêu cầu một **risk envelope**
  và chấp nhận risk disclaimer trước khi nó có thể arm.

Persona compiles **deterministically** thành system prompt của agent (không có LLM author nó), vì vậy cùng một cấu hình luôn tạo ra cùng một instructions — reproducible và auditable.

## The roster

Mỗi agent hiển thị trong một control-room table: **agent nào, type của nó, có bao nhiêu tài khoản nó quản lý, các
goals của nó, run status, và last action**, với các điều khiển **Start / Stop / Kill**. Kill switch halts một
running agent ngay lập tức.

## Safety is a domain invariant, not a setting

Mọi thứ chạm đến tiền đều đi qua **Autonomy & Safety Kernel**:

- **Risk envelope** — các giới hạn per-order cứng (max daily loss, open exposure, position size, leverage,
  consecutive losses, orders/hour, allowed symbols). Mỗi order được validate đối với nó trước khi dispatch;
  một breach bị từ chối, không clamp. Required trước khi một agent có thể đạt Full Auto.
- **Circuit breaker** — deterministically halts new risk on a loss streak, a daily-loss breach, a **hard
  performance-goal breach**, hoặc **AI-provider unavailability** (một down hoặc hallucinating model không bao giờ mở
  fresh positions).
- **Versioned disclaimer consent** — một one-time, versioned acceptance được yêu cầu để arm Full Auto
  (legally-required consent, không phải per-trade approval); bumping disclaimer forces re-consent.
- **Kill switch** — một idempotent emergency halt trên mọi running agent.

## Goals

Cho một agent **measurable objectives** — ví dụ *keep max drawdown below 4%*, *profit factor at least
1.5*, *win rate ≥ 55%*. Mỗi target là **Hard** (một guardrail — breach trips circuit breaker) hoặc
**Soft** (chỉ steer reasoning), evaluated as On-track / At-risk / Breached.

## The decision pipeline

Sau khi started, một agent chạy một **24/7 supervised loop** (`AgentRuntimeService`). Mỗi tick, cho mọi
managed account, nó: đọc **deterministic account state** (ground truth, không bao giờ là model's memory);
hỏi decision engine cho một move; pass nó qua **safety gate** (`AgentDecisionProcessor`) —
autonomy level → circuit breaker → risk envelope; writes an append-only **`AgentDecisionRecord`**; và
halts hoặc executes as the gate directs. Loop là **fault-isolated** (failure của một agent không bao giờ touch
another hoặc host) và **safe by default**: nó inert trừ khi AI được cấu hình *and*
`App:Ai:AgentRuntimeEnabled` được set, và nó không bao giờ mở fresh risk trong khi AI provider unavailable.

- **Approval gate** — một **Approval-gated** agent's proposed order được record là **Pending** và không làm gì
  cho đến khi owner approve nó (`POST /api/agent-studio/{id}/decisions/{seq}/approve` hoặc
  `/reject`); **Full Auto** clears through envelope không có per-trade approval; **Advisory** chỉ propose.
- **Audit ledger** — mọi decision có thể replay: reasoning (XAI), evidence nó cite, gate verdict,
  order intent và có execute hay không, tại `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — một on-demand multi-agent debate: Alpha/Sentiment/Technical/Risk analysts mỗi người cho một
  view và một Reviewer synthesises một proposal (`POST /api/agent-studio/{id}/debate`).
- **Memory** — agent nhớ mỗi decision và recalls recent memory vào next prompt của nó cho continuity
  (`GET /api/agent-studio/{id}/memory`).

Mỗi roster row's **Details** mở agent's decision feed (với Approve/Reject on pending orders),
memory của nó, và một Run-debate tab.

## Scope

Shipped: full agent lifecycle, deterministic safety gate, 24/7 runtime, human-in-the-loop
approval gate, audit ledger, và **live cTrader Open API integration** — account-state store
(reads real balance, positions và open exposure in lots) và order executor (places real market
orders, lots→volume via symbol lot size), cả hai resolve mỗi managed account's OAuth credentials và
degrade safely khi một account không được link. **Yêu cầu Anthropic API key** cho model generate orders
(cho đến lúc đó engine holds); still to come are multi-agent debate roles và layered
memory/reflection. Runtime off trừ khi `App:Ai:AgentRuntimeEnabled` được set, vì vậy live trading chỉ
xảy ra trên một explicit, fully-consented opt-in.

## Managed accounts and editing

Khi tạo một agent, bạn chọn tài khoản giao dịch mà nó quản lý — **ít nhất một là bắt buộc khi tạo** (nút *Create* bị disabled cho đến khi cái nào được chọn, và endpoint create reject một selection rỗng). Mọi agent có thể được **edited** sau này (name, temperament, autonomy, và managed accounts) từ biểu tượng bút chì trên hàng roster của nó. Lifecycle controls (details, edit, start, stop, kill) là icon buttons, mỗi cái disabled trong các trạng thái mà hành động không áp dụng.
