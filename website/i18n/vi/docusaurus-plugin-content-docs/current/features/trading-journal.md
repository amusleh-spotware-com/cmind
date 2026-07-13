---
description: "Nhật ký Giao dịch & Coach — phân tích các lần chạy và backtest của chính bạn để tìm rò rỉ hành vi (over-concentration, thất bại lặp đi lặp lại, thiên kiến thua lỗ) và huấn luyện bạn về chiến lược bạn đã có. Tất định, với narrative AI tùy chọn."
---

# Nhật ký Giao dịch & Coach

Danh mục AI-cho-giao dịch hữu ích mới nhất không phải là dự đoán thị trường — mà là phân tích
*bản thân bạn*. Nhật ký Giao dịch chuyển lịch sử các lần chạy và backtest của bạn thành phản hồi thành thật để
bạn có thể cải thiện chiến lược mình đã có.

Mở **AI → Trading Journal** (`/journal`).

## Nó tiết lộ gì

Từ các instance (lần chạy và backtest) của bạn, nó tính toán, tất định:

- **Số lần thắng / thua / thất bại và tỷ lệ thắng** trên tất cả các backtest;
- **Nhận định hành vi** — những rò rỉ âm thầm làm hao tổn các trader bán lẻ:
  - **Over-concentration** — phần lớn hoạt động của bạn tập trung vào một symbol;
  - **Repeated failures** — tỷ lệ cao các lần chạy không build được hoặc cấu hình sai;
  - **Losing bias** — nhiều backtest thua hơn thắng (với lời nhắc chạy Integrity Lab và
    kiểm tra lợi thế có thật không);
  - một báo cáo sức khỏe tốt khi không có vấn đề nào ở trên.

```http
GET /api/journal
```

## Tại sao nó đáng tin cậy

Phân tích hành vi là mã miền tất định thuần (`Core.Journal`) không phụ thuộc hạ tầng — được unit-test cho over-concentration, thất bại lặp lại, thiên kiến thua lỗ, trường hợp cân bằng và tài khoản trống. Các sự thật đến trước; AI coach (Portfolio Digest) là lớp narrative tùy chọn bên trên, được gating bằng Anthropic API key, vì vậy nhật ký hoạt động đầy đủ mà không cần AI được cấu hình.
