---
description: "Phí hiệu suất của money-manager trên high-water-mark, mô hình copy-trading tiêu chuẩn (cTrader Copy, Darwinex, ZuluTrade profit-share): provider tính phần trăm của lợi nhuận *mới* trên đỉnh equity của mỗi người theo dõi."
---

# Phí hiệu suất copy (Giai đoạn 4)

**Phí hiệu suất của money-manager trên high-water-mark**, mô hình copy-trading tiêu chuẩn (cTrader Copy,
Darwinex, ZuluTrade profit-share): provider tính phần trăm của lợi nhuận *mới* trên đỉnh equity của mỗi người theo dõi — không bao giờ trên opening balance, và không bao giờ hai lần cho cùng một ground đã recovered. **Opt-in** qua
`App:Copy:FeesEnabled` (tắt theo mặc định).

## Mô hình (high-water-mark)

Mỗi destination (tài khoản người theo dõi), mỗi settlement:

1. **Settlement đầu tiên** gieo high-water-mark (HWM) tại equity hiện tại → không tính phí (người theo dõi không bao giờ bị tính phí trên số dư gửi của họ).
2. **Đỉnh mới** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, rồi `HWM ← equity`.
3. **Tại hoặc dưới đỉnh**: không tính phí, HWM không đổi — người theo dõi phải recover vượt qua đỉnh cũ trước, vì vậy họ không bao giờ bị tính phí hai lần cho cùng một khoản lợi nhuận.

Số học phí là một invariant miền trên `CopyDestination.SettleFee(equity)` — aggregate sở hữu nó; settlement service chỉ cung cấp equity được poll và ghi lại số tiền được trả về. `PerformanceFee` là một value object được giới hạn ở 50% vì vậy một misconfiguration không thể tính phí hết toàn bộ lợi nhuận của người theo dõi.

## Cách nó settlement

```
CopyFeeSettlementService (BackgroundService, chỉ khi FeesEnabled)
   │  mỗi App:Copy:FeeSettlementInterval
   ├─ load các profile đang chạy có destination được cấu hình phí
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader mở một session,
   │                                               tính balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← logic HWM trên aggregate
   └─ persist HWM đã advance + append CopyFeeAccrual (chỉ khi đỉnh mới)
```

- `ICopyEquityReader` là một abstraction Core; implementation thực (`OpenApiCopyEquityReader`) là infra piece duy nhất — vì vậy settlement + logic HWM được exercise trong tests với một fake reader, không cần broker thực.
- `CopyFeeAccrual` là một log append-only (HWM-before, equity, fee %, số tiền phí, settled-at) — một fact log cho báo cáo phí và billing, không phải aggregate.

## Cấu hình & API

| `App:Copy` setting | Mặc định | Hiệu ứng |
|--------------------|---------|---------|
| `FeesEnabled` | `false` | Chạy settlement service. |
| `FeeSettlementInterval` | `1h` | Tần suất equity được poll và phí được settlement. |

Mỗi destination: `PerformanceFeePercent` (0–50) được đặt trên destination (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — các fee accrual của profile + tổng đã tính.

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — invariant HWM: settlement đầu tiên gieo + không tính phí; đỉnh mới chỉ tính phí trên phần lợi nhuận trên đỉnh; tại/dưới đỉnh không tính phí và đỉnh không bao giờ lùi; sau drawdown chỉ recovery vượt đỉnh cũ mới bị tính phí; 0% không bao giờ tính; VO từ chối phần trăm ngoài phạm vi.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (không tính phí, mark đã gieo), 12k (tính 400, mark tiến), 11k (không tính phí, mark giữ); accrual persisted đúng owner/amount.

Copy host không thay đổi bởi phí (settlement là một DB job riêng), vì vậy copy DST stress suite không bị ảnh hưởng (23/23).
