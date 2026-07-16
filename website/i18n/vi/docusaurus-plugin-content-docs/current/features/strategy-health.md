---
description: "Strategy Health & Alpha Decay — phát hiện suy giảm xác định so sánh Sharpe gần đây của chiến lược với kỷ lục trước đó và xác định điểm dịch chuyển trung bình lớn nhất (CUSUM change-point), trả về phán quyết Healthy / Degrading / Decayed / Unknown."
---

# Strategy Health & Alpha Decay

Mọi lợi thế đều suy giảm — nghiên cứu rõ ràng cho thấy thời gian bán rã của một chiến lược quant đã sụt giảm từ nhiều năm xuống còn nhiều tháng, vì vậy *thích ứng vượt qua khám phá*. Màn hình Strategy Health cho bạn biết, từ chính lịch sử lợi nhuận của chiến lược, liệu lợi thế vẫn còn đó hay không.

Mở **cBots → Strategy Health** (`/quant/health`).

## What it does

Cho một chuỗi lợi nhuận (hoặc đường cong tài sản, từ cũ nhất đến mới nhất), nó:

- chia lịch sử thành một nửa **trước đó** và một nửa **gần đây** và so sánh tỷ lệ Sharpe của chúng;
- chạy quét **CUSUM change-point** để xác định quan sát nơi trung bình rõ ràng nhất đã thay đổi (một sự phá vỡ chế độ), được báo cáo chỉ khi độ lệch đáng chú ý về mặt thống kê;
- trả về một phán quyết:

| Phán quyết | Ý nghĩa |
|---|---|
| **Healthy** | Hiệu suất gần đây phù hợp với (hoặc tốt hơn) kỷ lục trước đó. |
| **Degrading** | Sharpe gần đây yếu hơn đáng kể so với kỷ lục trước đó — hãy theo dõi chặt chẽ. |
| **Decayed** | Lợi thế đã biến mất hiệu quả trong cửa sổ gần đây — hãy cân nhắc tạm dừng. |
| **Unknown** | Không đủ lịch sử để phán xét. |

- **Trực tiếp từ một lần chạy backtest — không sao chép dán.** Mỗi backtest hoàn thành đều đưa ra một biểu tượng **Kiểm tra sức khỏe chiến lược** trên hàng danh sách **Backtest** và trên chế độ xem chi tiết phiên bản của nó; một cú nhấp chuột chạy màn hình trên đường cong tài sản được lưu trữ của lần chạy đó và hiển thị phán quyết trong một hộp thoại. Biểu tượng bị vô hiệu hóa cho đến khi backtest hoàn thành và tạo ra báo cáo, vì vậy nó không bao giờ là một điều khiển không hoạt động. Ở dưới nền tảng, đây là `POST /api/quant/health/backtest/{instanceId}`, đọc đường cong tài sản của báo cáo được lưu trữ.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Why it is reliable

Nó là mã miền tinh khiết, xác định (`Core.Health`) không có phụ thuộc cơ sở hạ tầng và không có cuộc gọi bên ngoài — được kiểm tra đơn vị cho các trường hợp decayed, degrading, healthy và quá ngắn và để xác định vị trí điểm thay đổi. Nó là người bạn đi kèm thủ công để kiểm tra sức khỏe luôn bật hỗ trợ các tác nhân tự chủ: những thống kê tương tự lái xe bộ ngắt mạch vượt trội hóa một chiến lược trực tiếp mà lợi thế đang phai.
