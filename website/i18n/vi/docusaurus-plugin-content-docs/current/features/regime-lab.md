---
description: "Regime Lab — gắn nhãn chuỗi lợi nhuận vào các regime biến động Calm / Normal / Turbulent và báo cáo hiệu suất theo regime, cộng với Hurst exponent (trend-persistence vs mean-reversion). Tất định."
---

# Regime Lab

Một tỷ số Sharpe đơn lẻ ẩn sự thật rằng hầu hết các lợi thế đều có điều kiện: tuyệt vời trong thị trường calm, trending
và chết trong turbulence (hoặc ngược lại). Regime Lab chia lịch sử chiến lược thành các regime biến động
và cho thấy nó hoạt động như thế nào trong mỗi regime — vì vậy bạn biết *khi nào* lợi thế của bạn thực sự hoạt động.

Mở **cBots → Regime Lab** (`/quant/regimes`).

## Nó làm gì

Cho một chuỗi lợi nhuận (hoặc đường cong equity, cũ nhất trước), nó:

- tính **biến động thực tế trailing** tại mỗi điểm và chia lịch sử thành các regime **Calm**,
  **Normal** và **Turbulent** bằng các tercile của biến động đó;
- báo cáo **hiệu suất theo regime** — số quan sát, lợi nhuận trung bình, biến động và Sharpe — vì vậy bạn có thể thấy
  lợi thế sống ở đâu;
- ước tính **Hurst exponent** qua phân tích rescaled-range (R/S): trên ~0.55 chuỗi là
  **trending / persistent**, dưới ~0.45 nó là **mean-reverting**, và quanh 0.5 nó gần như random walk.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // or { "equity": [...] }
```

## Tại sao nó đáng tin cậy

Mã miền tất định thuần (`Core.Regimes`) không phụ thuộc hạ tầng và không có lệnh gọi bên ngoài
— được unit-test cho việc phân tách regime (calm vs turbulent volatility) và cho hướng Hurst
(chuỗi anti-persistent ghi điểm dưới 0.5, một trend persistent ghi điểm trên). Cùng tín hiệu regime cung cấp năng lượng
cho vòng phản chiếu của các tác nhân tự trị, vì vậy một tác nhân có thể dựa vào các regime mà lợi thế của nó là thật.
