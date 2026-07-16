---
description: "Phòng thí nghiệm Kiểm tra Tính toàn vẹn Backtest — thống kê độ tin cậy quỹ xác định (Xác suất Sharpe & Sharpe Giảm phát, t-stat) biến một backtest thô thành một phán quyết Vững chắc / Mong manh / Tối ưu hóa quá mức, điều chỉnh cho số lượng cấu hình bạn đã thử."
---

# Phòng Thí Nghiệm Kiểm Tra Tính Toàn Vẹn Backtest

Các nền tảng bán lẻ hiển thị cho bạn Sharpe hoặc lợi nhuận ròng của một backtest và dừng lại. Các tổ chức không bao giờ tin tưởng một backtest thô — họ hỏi liệu kết quả có tồn tại **sau khi điều chỉnh cho độ lệch chọn lọc và số lượng cấu hình bạn đã thử**. Phòng Thí Nghiệm Kiểm Tra Tính Toàn Vẹn Backtest đưa kiểm tra này vào cMind. Đó là **toán học xác định** (không AI, không có cuộc gọi bên ngoài), vì vậy phán quyết có thể tái tạo được và mọi số đều có thể giải thích được.

Mở nó tại **cBots → Integrity** (`/quant/integrity`).

## Nó tính toán gì

Cho một chuỗi lợi nhuận (hoặc một đường cong vốn/số dư) và số lượng bộ tham số bạn đã thử để đạt được nó, bộ phân tích báo cáo:

- **Tỷ lệ Sharpe** — trên mỗi kỳ và hàng năm (căn bậc hai của thời gian).
- **Tỷ lệ Sharpe Xác suất (PSR)** — mức độ tự tin rằng *Sharpe thực* vượt qua điểm chuẩn, tính đến độ dài kỷ lục, độ lệch và độ nhọn (Bailey & López de Prado, 2012). Một kỷ lục ngắn hoặc có đuôi dày sẽ hạ thấp nó.
- **Tỷ lệ Sharpe Giảm Phát (DSR)** — PSR được đo so với một **điểm chuẩn giảm phát**: Sharpe mà bạn sẽ mong đợi từ *tốt nhất của N lần thử ngẫu nhiên* theo giả thuyết không (Định lý Chiến lược Sai). Bạn càng thử nhiều cấu hình, thanh càng cao — đây là cách nó bắt được tối ưu hóa quá mức.
- **t-statistic** của lợi nhuận trung bình. Theo Harvey, Liu & Zhu, một lợi thế thực sự phải vượt qua **t ≥ 3.0**, không phải là 2.0 trong sách giáo khoa.
- **Độ lệch / Độ nhọn** của lợi nhuận, chúng cấp nguồn cho các điều chỉnh PSR/DSR.

## Phán quyết

| Phán quyết | Ý nghĩa | Quy tắc |
|---|---|---|
| **Vững chắc** | Lợi thế tồn tại qua các lần thử bạn đã chạy. | DSR ≥ 95% **và** PSR ≥ 95% **và** \|t\| ≥ 3.0 |
| **Mong manh** | Còn sống về mặt thống kê nhưng không thuyết phục — không tăng kích thước dựa trên điều này một mình. | giữa hai cái |
| **Tối ưu hóa quá mức** | Rất có khả năng là một tạo tác của độ lệch chọn lọc, không phải một lợi thế thực sự. | DSR < 90% |

Mọi kết quả đều mang lý do bằng tiếng Anh đơn giản để lý do "tại sao" không bao giờ bị ẩn.

## Xác suất Backtest Tối ưu hóa quá mức (trên các lần thử)

Cung cấp một *số lượng* lần thử là tốt; cung cấp **chuỗi ngoài mẫu thực tế của mọi cấu hình bạn đã thử** là tốt hơn. Dán chúng vào **lưới thử nghiệm** tùy chọn (một chuỗi trên mỗi dòng) và cMind chạy **Xác thực Chéo Đối xứng Kết hợp** (Bailey, Borwein, López de Prado & Zhu, 2015): nó chia các quan sát thành các nhóm, và cho mọi cách chọn một nửa là trong mẫu, nó chọn cấu hình tốt nhất trong mẫu và kiểm tra xem người chiến thắng đó có đổ bộ ở nửa dưới **ngoài mẫu** không. **Xác suất Backtest Tối ưu hóa quá mức (PBO)** là phần của các lần chia nơi người chiến thắng không thành công trong việc khái quát. Một PBO gần 0 có nghĩa là cấu hình tốt nhất thực sự là tốt nhất; một PBO 0,5 hoặc hơn có nghĩa là quá trình lựa chọn của bạn đang chọn nhiễu — phán quyết trở thành **Tối ưu hóa quá mức** bất kể người chiến thắng trông tốt như thế nào.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Khi trình tối ưu hóa cTrader Console gốc được áp dụng, cMind sẽ cấp toàn bộ bề mặt thử nghiệm của nó ở đây tự động.

## Lần thử — số con số quan trọng

`Trials` là **có bao nhiêu bộ tham số bạn đã kiểm tra** trước khi chọn cái này. Kiểm tra một chiến lược và kiểm tra mười nghìn và giữ lại cái tốt nhất là những điều hoàn toàn khác nhau: điều thứ hai sản xuất một Sharpe trong mẫu cao một cách tình cờ. Cung cấp số lần thử trung thực là toàn bộ điểm — nó tăng giảm phát và có thể di chuyển một backtest "tuyệt vời" thành **Tối ưu hóa quá mức**. Khi trình tối ưu hóa cTrader Console gốc được áp dụng, cMind cung cấp cho nó kích thước lưới quét thực tế một cách tự động.

## Đầu vào

- **Lợi nhuận định kỳ** — một số trên mỗi kỳ (ví dụ: `0.01` = +1%). Ít nhất hai. Trường xác thực khi bạn nhập: nó đếm các số hợp lệ, gắn cờ bất kỳ mã nào không phải là số, và chỉ cho phép **Phân tích** sau khi ít nhất có hai giá trị sạch (lưới thử nghiệm cho phép **Đánh giá tối ưu hóa quá mức** sau khi hai chuỗi bốn số trở lên mỗi chuỗi đã sẵn sàng).
- **Đường cong vốn / số dư** — cMind rút ra các lợi nhuận đơn giản liên tiếp cho bạn.
- **Thẳng từ một lần chạy backtest — không sao chép dán.** Mỗi backtest hoàn thành hiển thị một **Kiểm tra tính toàn vẹn backtest** icon khiên trên hàng danh sách **Backtest** và trên chế độ xem chi tiết thực thể của nó; một cú nhấp chuột chạy Phòng Thí Nghiệm trên đường cong vốn được lưu trữ của lần chạy đó và hiển thị phán quyết trong hộp thoại. Biểu tượng bị vô hiệu hóa cho đến khi backtest hoàn thành và tạo ra một báo cáo, vì vậy nó không bao giờ là một điều khiển chết. Dưới lớp vỏ này là `POST /api/quant/integrity/backtest/{instanceId}`, đọc đường cong vốn của báo cáo được lưu trữ.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Trả về phán quyết, tất cả các số liệu và lý do. `POST /api/quant/integrity/backtest/{id}` chạy cùng một phân tích trên một backtest hoàn thành mà bạn sở hữu.

## Tại sao nó đáng tin cậy

Các thống kê là các hàm thuần túy trong hạt nhân miền (`Core.Quant`) với sự phụ thuộc cơ sở hạ tầng bằng không — chúng không thể bị đưa xuống bởi một lỗi mạng, và chúng được ghim bởi các bài kiểm tra đơn vị vectơ vàng so với các công thức được xuất bản. CDF bình thường/nghịch đảo là các xấp xỉ dạng đóng (Abramowitz-Stegun / Acklam), vì vậy các đầu vào giống nhau luôn mang lại cùng một phán quyết.
