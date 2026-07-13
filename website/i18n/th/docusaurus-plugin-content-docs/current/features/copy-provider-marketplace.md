---
description: "ไดเรกทอรีที่เข้าถึงได้ของ copy strategies Provider เผยแพร่ copy profile เป็น listing พร้อม verified-live badge (source account ของ strategy เทรดด้วยเงินจริง ไม่ใช่ demo)"
---

# Copy provider marketplace (Phase 4)

ไดเรกทอรีที่เข้าถึงได้ของ copy strategies Provider **publishes** copy profile เป็น listing พร้อม
**verified-live** badge (source account ของ strategy เทรดด้วยเงินจริง ไม่ใช่ demo) บวก performance fee
Followers เรียกดู marketplace จัดอันดับด้วย performance score ที่ project มาจาก execution-transparency data

## Model

- `CopyProviderListing` = aggregate: `UserId`, `ProfileId`, display name, description, performance fee,
  `VerifiedLive`, `Published` + `PublishedAt` หนึ่ง listing ต่อ profile (unique index)
- **Verified-live** derive ตอน publish time จาก profile source `TradingAccount.IsLive` —
  provider ไม่สามารถ self-assert ได้
- Performance stats **ไม่ได้เก็บบน listing** — read-model projection over `CopyExecution`
  transparency log (fill rate, avg latency, avg realized slippage) ดังนั้น marketplace
  สะท้อน live execution quality เสมอ

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` →
คะแนน 0–100: fill rate เป็นตัวหลัก (×60), low latency + low slippage เพิ่ม (×20 แต่ละอัน),
verified-live badge เพิ่ม small trust bonus Deterministic + monotonic ดังนั้น ordering stable

## API

- `POST /api/copy/profiles/{id}/publish` — publish/update profile listing
  (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live set จาก source account
- `DELETE /api/copy/profiles/{id}/publish` — unpublish
- `GET /api/copy/marketplace` — all published listings, ranked, แต่ละอันมี performance summary
  (executions, fill rate, avg latency, avg slippage, score) + verified-live badge

## Tests

- **Unit** (`CopyProviderListingTests`) — aggregate invariants: display name required; publish set
  timestamp; unpublish hide; update replace display fields + fee + badge
- **Integration** (`CopyMarketplaceTests`, real Postgres) — published listing persist พร้อม badge;
  หนึ่ง listing ต่อ profile (unique index); ranking score prefer verified/high-fill providers

Copy host ไม่ถูกแตะ (listings + read model เท่านั้น) ดังนั้น copy DST stress suite ไม่ได้รับผลกระทบ
