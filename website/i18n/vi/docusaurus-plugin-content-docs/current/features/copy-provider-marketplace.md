---
description: "Thư mục có thể duyệt các chiến lược copy. Provider publish copy profile như một listing với verified-live badge (tài khoản nguồn chiến lược giao dịch tiền thật, không phải demo) cộng với phí hiệu suất."
---

# Thị trường nhà cung cấp copy (Giai đoạn 4)

Thư mục có thể duyệt các chiến lược copy. Provider **publish** copy profile như một listing với badge **verified-live** (tài khoản nguồn chiến lược giao dịch tiền thật, không phải demo) cộng với phí hiệu suất. Người theo dõi duyệt thị trường, được xếp hạng bằng điểm hiệu suất được tính từ dữ liệu execution-transparency.

## Mô hình

- `CopyProviderListing` = aggregate: `UserId`, `ProfileId`, display name, description, performance fee, `VerifiedLive`, `Published` + `PublishedAt`. Một listing cho mỗi profile (unique index).
- **Verified-live** được xác định tại thời điểm publish từ `TradingAccount.IsLive` của tài khoản nguồn profile — provider không thể tự xác nhận.
- Các thống kê hiệu suất **không được lưu trên listing** — projection read-model qua transparency log `CopyExecution` (fill rate, avg latency, avg realized slippage), vì vậy marketplace luôn phản ánh chất lượng execution thực tế.

## Xếp hạng

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → điểm 0–100: fill rate chiếm ưu thế (×60), latency thấp + slippage thấp cộng thêm (×20 mỗi cái), badge verified-live cộng thêm trust bonus nhỏ. Tất định + monotonic, vì vậy thứ tự ổn định.

## API

- `POST /api/copy/profiles/{id}/publish` — publish/update listing profile (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live được đặt từ tài khoản nguồn.
- `DELETE /api/copy/profiles/{id}/publish` — unpublish.
- `GET /api/copy/marketplace` — tất cả các listing đã publish, được xếp hạng, mỗi cái với tóm tắt hiệu suất (số execution, fill rate, avg latency, avg slippage, điểm) + badge verified-live.

## Tests

- **Unit** (`CopyProviderListingTests`) — aggregate invariants: display name bắt buộc; publish đặt timestamp; unpublish ẩn; update thay thế các trường hiển thị + fee + badge.
- **Integration** (`CopyMarketplaceTests`, real Postgres) — listing đã publish vẫn tồn tại với badge; một listing cho mỗi profile (unique index); điểm xếp hạng ưu tiên verified/high-fill providers.

Copy host không thay đổi (listings + read model only), vì vậy copy DST stress suite không bị ảnh hưởng.
