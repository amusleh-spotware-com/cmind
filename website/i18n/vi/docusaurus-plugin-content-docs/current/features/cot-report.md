# Commitment of Traders (COT)

cMind cung cấp một báo cáo **Commitment of Traders** được tích hợp sẵn — bảng phân tích hàng tuần của CFTC
về những người dài và ngắn trong thị trường tương lai của Mỹ (các công ty phòng chống rủi ro thương mại,
các nhà đầu cơ lớn, quỹ), với các biểu đồ lịch sử tương tác, chỉ số **COT** được chuẩn hóa, API REST
xác thực cho cBots và các công cụ MCP cho các khách hàng AI. Dữ liệu đến trực tiếp từ **các tập dữ liệu
công khai Socrata của CFTC** — không cần khóa API, không có tổng hợp. Giống như lịch kinh tế, nó là một
mô-đun riêng biệt có thể được tắt hoàn toàn mà không ảnh hưởng đến lõi giao dịch.

## Nó mang lại những gì

- **Cả ba họ báo cáo, chỉ tương lai và kết hợp tương lai + tùy chọn:**
  - **Legacy** — Non-Commercial (các nhà đầu cơ lớn), Commercial (các công ty phòng chống rủi ro), Non-Reportable.
  - **Disaggregated** — Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.
  - **Traders in Financial Futures (TFF)** — Dealer, Asset Manager, Leveraged Funds, Other Reportables.
- **Một danh mục thị trường được lựa chọn** — Các cặp FX chính, vàng/bạc/đồng, dầu thô & khí tự nhiên, Treasuries,
  chỉ số vốn chủ sở hữu, tiền điện tử và các hạt nhân/mềm chính — mỗi cái được ánh xạ tới mã hợp đồng CFTC ổn định
  của nó và, nơi không rõ ràng, tới một ký hiệu giao dịch (ví dụ: Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **Chỉ số COT (0–100)** — nơi vị trí ròng nhà đầu cơ hiện tại nằm trong phạm vi lịch sử của nó
  (mặc định khoảng 3 năm nhìn lại). Đọc gần các cực biên báo hiệu vị trí đông đúc thường
  trước một sự đảo ngược; báo cáo gắn nhãn một **cực dài** (≥80) hoặc **cực ngắn** (≤20).
- **Tính chính xác tại một thời điểm.** Báo cáo hàng tuần được đo vào thứ Ba nhưng chỉ trở nên công khai
  vào thứ Sáu tiếp theo; mỗi lần đọc tôn trọng thời điểm phát hành đó, vì vậy một tín hiệu vị trí kiểm tra ngược
  không bao giờ thấy báo cáo trước khi nó được công bố (không nhìn trước).

## Sử dụng trang

Mở **Commitment of Traders** từ điều hướng bên trái. Chọn một **thị trường**, một **loại báo cáo** (Legacy /
Disaggregated / Financial) và bật tắt **Futures + options** để chuyển đổi giữa chỉ tương lai và biến thể kết hợp.
Trang hiển thị:

- **Vị trí ròng theo thời gian** — biểu đồ dòng tương tác của vị trí ròng của mỗi loại nhà giao dịch
  (dài − ngắn) trên toàn cửa sổ lịch sử.
- **Chỉ số COT** — biểu đồ dòng của chỉ số 0–100, với mức đọc mới nhất và nhãn cực của nó.
- **Ảnh chụp nhanh mới nhất** — bảng dài / ngắn / ròng / % lãi suất mở trên mỗi loại nhà giao dịch, cộng
  với tổng lãi suất mở và ngày báo cáo.

Mỗi biểu đồ đi kèm với **phóng to / thu nhỏ** (và đặt lại) các nút thanh công cụ, và bạn có thể kéo trên trục thời gian để phóng to. **Xuất CSV** tải xuống toàn bộ lịch sử hàng tuần của thị trường và loại báo cáo được chọn dưới dạng tệp sẵn sàng bảng tính. Sử dụng **So sánh các thị trường** để chồng chéo nhiều thị trường trên một biểu đồ — các biểu đồ so sánh vẽ vị trí ròng nhà đầu cơ và chỉ số COT của mỗi thị trường được chọn cạnh nhau, vì vậy bạn có thể đọc định vị chéo thị trường một cách sáng suốt.

## Dòng dữ liệu được thực hiện như thế nào

Cơ sở dữ liệu là bộ nhớ cache. Một công nhân nhập dữ liệu hàng tuần kéo sáu tập dữ liệu CFTC cho các thị trường theo dõi, upserts danh mục thị trường và nối thêm mỗi báo cáo mới **theo cách lạc quan** (chạy lại không bao giờ sao chép một ảnh chụp nhanh). Ngoài ra, dữ liệu được **tải theo yêu cầu**: lần đầu tiên một thị trường được yêu cầu nó sẽ được tìm nạp từ nguồn CFTC và lưu trữ, và mọi yêu cầu tiếp theo được phục vụ trực tiếp từ cơ sở dữ liệu. Bộ nhớ cache **làm mới khi các báo cáo hàng tuần mới được phát hành** — khi báo cáo được lưu trữ mới nhất cũ hơn một tuần, yêu cầu tiếp theo sẽ trong suốt kéo và nối thêm dữ liệu mới nhất (giới hạn để nguồn không bao giờ bị tấn công). Lần tải đầu tiên điền sẵn vài năm lịch sử; một sự cố nguồn giảm xuống phục vụ dữ liệu được lưu trong bộ nhớ cache tốt nhất. Mọi thứ chạy từ hộp không có khóa; một mã thông báo ứng dụng Socrata tùy chọn chỉ nâng giới hạn tốc độ.

## Cấu hình

Tất cả các khóa nằm dưới `App:Cot` (xem [tính năng toggle](./feature-toggles.md) và
[cài đặt chủ sở hữu white-label](./white-label-owner-settings.md)):

| Khóa | Mặc định | Mục đích |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Cho dù công nhân nhập dữ liệu hàng tuần có chạy hay không. |
| `PollInterval` | `6h` | Công nhân bỏ phiếu cho các tập dữ liệu CFTC bao lâu. |
| `BackfillYears` | `5` | Năm lịch sử kéo trên lần chạy đầu tiên. |
| `ReconcileLookbackWeeks` | `4` | Những tuần gần đây được đồng bộ hóa lại mỗi chu kỳ để bắt các sửa đổi. |
| `SocrataAppToken` | — | Mã thông báo tùy chọn tăng giới hạn tốc độ ẩn danh. |
| `CotIndexLookbackWeeks` | `156` | Báo cáo hàng tuần được sử dụng làm phạm vi chỉ số COT (~3 năm). |

## Gating

Khả năng hiển thị là một cổng hai tầng, giống hệt lịch kinh tế: cổng hard white-label
`App:Branding:EnableCot` (mức xây dựng) **và** tính năng toggle chạy lúc `App:Features:Cot`. Khi tắt liên kết điều hướng,
trang, API REST và các công cụ MCP đều biến mất (API trả về `404`). Vì nguồn dữ liệu không cần khóa, không có cổng khóa
nguồn dữ liệu — được bật có nghĩa là hiển thị.

## Dành cho các nhà phát triển

- Miền: `Core.Cot` — các tổng hợp `CotMarket` và `CotReport`, đối tượng giá trị `CotPositions`, dịch vụ
  miền `CotIndexCalculator` và các cổng `ICotReports` / `ICotSource`.
- Cơ sở hạ tầng: `Infrastructure.Cot` — trình phân tích anti-corruption `CftcSocrataSource`, cổng tốc độ,
  dịch vụ ghi chỉ-thêm, phía đọc và công nhân nhập dữ liệu hàng tuần (lược đồ EF `cot`).
- Truy cập cBot & AI: [API cBot COT](./cot-cbot-api.md) (REST, `market:read` JWT) và các công cụ MCP
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
