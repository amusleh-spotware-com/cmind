---
description: "Sức khỏe Chiến lược & Suy thoái Alpha — phát hiện suy thoái tất định so sánh Sharpe gần đây của chiến lược với bản ghi trước đó và xác định điểm dịch chuyển trung bình lớn nhất (CUSUM change-point), trả về phán quyết Khỏe mạnh / Đang suy giảm / Đã suy thoái."
---

# Sức khỏe Chiến lược & Suy thoái Alpha

Mọi lợi thế đều suy thoái — nghiên cứu đã chỉ rõ rằng thời gian bán rã của một chiến lược định lượng đã thu hẹp từ nhiều năm xuống còn nhiều tháng, vì vậy *thích ứng thắng khám phá*. Màn hình Sức khỏe Chiến lược cho bạn biết, từ chính lịch sử lợi nhuận của chiến lược, liệu lợi thế còn tồn tại hay không.

Mở **cBots → Strategy Health** (`/quant/health`).

## Nó làm gì

Cho một chuỗi lợi nhuận (hoặc đường cong equity, cũ nhất trước), nó:

- chia lịch sử thành **nửa trước** và **nửa gần đây** và so sánh tỷ số Sharpe của chúng;
- chạy quét **CUSUM change-point** để xác định quan sát mà mean dịch chuyển rõ ràng nhất (một regime break), chỉ báo cáo khi độ lệch có ý nghĩa thống kê;
- trả về một phán quyết:

| Phán quyết | Ý nghĩa |
|---|---|
| **Khỏe mạnh** | Hiệu suất gần đây tương xứng (hoặc tốt hơn) với bản ghi trước đó. |
| **Đang suy giảm** | Sharpe gần đây yếu hơn đáng kể so với bản ghi trước đó — theo dõi sát. |
| **Đã suy thoái** | Lợi thế đã thực sự biến mất trong cửa sổ gần đây — cân nhắc tạm dừng. |
| **Không xác định** | Không đủ lịch sử để đánh giá. |

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Tại sao nó đáng tin cậy

Đây là mã miền tất định thuần (`Core.Health`) không phụ thuộc hạ tầng và không có lệnh gọi bên ngoài — được unit-test cho các trường hợp suy thoái, suy giảm, khỏe mạnh và quá ngắn cùng với việc xác định vị trí change-point. Nó là phần bổ sung thủ công cho các kiểm tra sức khỏe luôn bật hỗ trợ các tác nhân tự trị: cùng các thống kê đó cung cấp năng lượng cho circuit breaker giảm rủi ro cho chiến lược đang chạy mà lợi thế đang phai dần.
