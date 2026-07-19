# API cBot COT

Dữ liệu Commitment of Traders được tiết lộ cho cBots và các khách hàng bên ngoài thông qua API REST được xác thực,
vì vậy một chiến lược có thể kéo vị trí (vị trí ròng, % lãi suất mở, chỉ số COT) làm đầu vào tín hiệu.
Nó tái sử dụng **cùng máy móc JWT và phạm vi `market:read`** như API thị trường độ mạnh tiền tệ — một mã thông báo, một lược đồ.

## Xác thực

1. Trong ứng dụng, cấp một khách hàng API dữ liệu thị trường (chủ sở hữu) và cấp cho nó phạm vi **`market:read`**.
2. Trao đổi id/bí mật khách hàng cho một mã thông báo người mang ngắn hạn:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Phản hồi mang `token`, `expiresAt` và `scopes` được cấp.
3. Gửi mã thông báo trên mỗi cuộc gọi COT:

   ```http
   Authorization: Bearer <token>
   ```

Một mã thông báo bị thiếu/không hợp lệ trả về `401`; một mã thông báo không có `market:read` trả về `403`.

## Điểm cuối

Đường dẫn cơ sở `/api/market/v1/cot`. Tất cả các phản hồi là JSON.

| Phương pháp & đường dẫn | Mục đích |
|---------------|---------|
| `GET /markets` | Danh mục thị trường hợp đồng được theo dõi. Tùy chọn `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) và từ khóa `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Ảnh chụp nhanh hàng tuần mới nhất cho một thị trường. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Lịch sử hàng tuần trong một cửa sổ. |

Tham số:

- `code` — mã thị trường hợp đồng CFTC (ví dụ: `099741` cho Euro FX; lấy nó từ `/markets`).
- `kind` — `Legacy` (mặc định), `Disaggregated` hoặc `Tff`.
- `combined` — `true` cho tương lai + tùy chọn, `false` (mặc định) chỉ dành cho tương lai.
- `asOf` (ISO-8601, tùy chọn) — neo điểm trong thời gian: chỉ báo cáo công khai tại thời điểm đó mới được trả về,
  vì vậy một kiểm tra ngược không thấy nhìn trước.

### Ví dụ

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## Công cụ MCP

Cùng một mô hình đọc có sẵn cho các khách hàng AI như các công cụ MCP: `CotMarkets`, `CotLatest`, `CotHistory`
và `CotHealth` — mỗi cái đúng theo thời điểm thông qua tùy chọn `asOf`. Xem
[tính năng Commitment of Traders](./cot-report.md) để có bức tranh đầy đủ.

## Gating

API ở phía sau cổng hai tầng giống như trang: `App:Branding:EnableCot` và `App:Features:Cot`.
Khi tắt mỗi tuyến đường dưới `/api/market/v1/cot` trả về `404`.
