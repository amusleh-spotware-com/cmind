---
description: "Xây dựng, chạy, backtest các cBot cTrader (C# và Python, cả hai .NET) từ trình soạn thảo Monaco trong trình duyệt, chạy trên hình ảnh ghcr.io/spotware/ctrader-console chính thức."
---

# Xây dựng và backtest cBot

Xây dựng, chạy, backtest các cBot cTrader (C# **và** Python, cả hai .NET) từ trình soạn thảo
Monaco trong trình duyệt, chạy trên hình ảnh chính thức `ghcr.io/spotware/ctrader-console`.

## Xây dựng

- Trang **Builder** lưu trữ trình soạn thảo Monaco; `CBotBuilder` biên dịch dự án với
  `dotnet build` **trong vùng chứa dùng một lần** (`AppOptions.BuildImage`, thư mục làm việc được gắn
  tại `/work`), vì vậy các mục tiêu MSBuild của người dùng không đáng tin cậy không thể truy cập máy chủ. Khôi phục NuGet được lưu vào bộ nhớ đệm
  trên các bản dựng thông qua khối chia sẻ. Máy chủ Web cần truy cập vào ổ cắm Docker.
- Các mẫu khởi động C# + Python nằm trong `src/Nodes/Builder/Templates/`.

## Chạy và backtest

- **Instances** = Hệ thống phân cấp trạng thái TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Chuyển đổi thay thế thực thể (thay đổi id),
  id vùng chứa được mang theo.
- `NodeScheduler` chọn nút hợp lệ được tải nhẹ nhất; `ContainerDispatcherFactory` định tuyến đến
  tác nhân HTTP nút từ xa hoặc bộ điều phối Docker cục bộ.
- Các máy đo hoàn thành đồng bộ hóa các vùng chứa đã thoát (các vùng chứa backtest tự thoát thông qua
  `--exit-on-stop`); báo cáo hiện diện → hoàn thành (lưu trữ `ReportJson`), không có → thất bại.
- Nhật ký vùng chứa trực tiếp được phát trực tuyến đến trình duyệt qua SignalR; các đường cong quyền lực backtest được phân tích từ
  báo cáo + biểu đồ.

## Dữ liệu thị trường backtest được lưu vào bộ nhớ đệm cho mỗi tài khoản

cTrader Console tải xuống dữ liệu tick/thanh lịch sử vào `--data-dir` của nó. Thư mục đó là một
**bộ nhớ đệm ổn định, liên tục được khóa trên tài khoản giao dịch** (số tài khoản của nó) — được gắn
từ đĩa của nút tại đường dẫn vùng chứa của chính nó (`/mnt/data`), một **gắn riêng biệt, không lồng nhau** từ thư mục công việc cho mỗi phiên bản. Vì vậy, mỗi backtest trên cùng một tài khoản **sử dụng lại** dữ liệu đã được tải xuống
thay vì tải xuống lại mỗi lần chạy. (Trước đây, thư mục dữ liệu nằm dưới thư mục công việc cho mỗi phiên bản, id của nó thay đổi mỗi lần chạy, điều này buộc phải tải xuống mới mỗi backtest.) Thư mục công việc các phiên bản thoáng ngoài vẫn chứa thuật toán, tham số, mật khẩu
và báo cáo; bộ nhớ đệm dữ liệu chia sẻ được tính trong mức sử dụng dữ liệu backtest của nút và được xóa bởi
hành động làm sạch nút.

## Cài đặt backtest

Hộp thoại **Backtest** hiển thị mọi cài đặt mà CLI backtest cTrader Console chấp nhận, vì vậy bạn không bao giờ
phải chạm vào dòng lệnh:

- **From / To** — cửa sổ backtest (`--start` / `--end`).
- **Data mode** — `m1` (thanh 1 phút) hoặc `tick` (`--data-mode`).
- **Starting balance** — mặc định là `10000` (`--balance`). Một **số dư 0 không thực hiện giao dịch và khiến
  cTrader phát hành một báo cáo trống mà nó sau đó gặp sự cố** ("Message expected"), do đó một số dư khác không là
  luôn được gửi.
- **Commission** và **Spread** (`--commission` / `--spread`, spread tính bằng pips).
- **Advanced options** — một hộp `name=value` dạng tự do mỗi dòng cho bất kỳ tùy chọn backtest nào khác cTrader
  hỗ trợ (ví dụ: `applyCommissionAutomatically=true`); mỗi dòng trở thành một đối số CLI `--name value`.

## Trang chi tiết phiên bản

Mở một phiên bản (`/instance/{id}`) sẽ hiển thị trạng thái trực tiếp, nhật ký của nó và — cho một backtest — đường cong quyền lực
của nó. **Tiêu đề tab trình duyệt** phản ánh phiên bản cụ thể (**tên cBot · loại · ký hiệu**, ví dụ:
`TrendBot · Backtest · EURUSD`) vì vậy một tab chạy trực tiếp và một tab backtest là có thể phân biệt được thoạt nhìn.
Một lần chạy và một backtest của cùng một cBot được theo dõi là các **dòng dõi** khác nhau (một id dòng dõi ổn định được mang
qua các chuyển đổi trạng thái), vì vậy trang này theo dõi chính xác một phiên bản và không bao giờ trộn dữ liệu của một lần chạy với
dữ liệu của một backtest.

## Các kiểm soát vòng đời phiên bản

Mỗi hàng phiên bản (và trang chi tiết của nó) có các kiểm soát đúng theo trạng thái. Một phiên bản **hoạt động** hiển thị
**Stop**; một phiên bản **cuối cùng** (Stopped / Completed / Failed) hiển thị **Start (▶)** để khởi chạy lại nó với
cùng một cBot, tài khoản, ký hiệu, khung thời gian, bộ tham số và hình ảnh (một lần chạy bắt đầu lại như một lần chạy, một backtest như một backtest). Nhấp vào Stop sẽ hiển thị thông báo "Stopping…" và vô hiệu hóa biểu tượng cho đến khi nó được giải quyết, và một lần chạy mới được tạo sẽ xuất hiện trong danh sách ngay lập tức — không cần tải lại trang.

Các nhật ký bảng điều khiển được **lưu trữ khi một phiên bản kết thúc** — cho một lần chạy (khi Stop) và cho một
**backtest** (khi hoàn thành) như nhau — vì vậy các nhật ký lần chạy cuối cùng vẫn có thể xem được trên trang chi tiết và,
thông qua thanh công cụ nhật ký, **sao chép vào bộ nhớ đệm** (biểu tượng Sao chép nhật ký) hoặc **tải xuống** (biểu tượng Tải xuống nhật ký)
ngay cả khi vùng chứa đã biến mất. Cả hai tác động lên nhật ký bảng điều khiển đầy đủ của phiên bản, không chỉ phần cuối trên màn hình.

Một `.algo` được **tải lên** không bao giờ được xây dựng ở đây, vì vậy cột **Last Build** của nó trên trang cBots bị bỏ trống
(nó chỉ hiển thị thời gian xây dựng cho các cBot bạn xây dựng trong trình duyệt).

## Chỉnh sửa và chạy lại một phiên bản đã dừng

Một phiên bản đã **dừng** (chạy hoặc backtest) có kiểm soát **Chỉnh sửa** — một biểu tượng trên hàng của nó trong danh sách **và**
bên cạnh Start/Stop trên trang chi tiết của nó — mở một hộp thoại **được điền sẵn** với cấu hình hiện tại của nó.
Bạn có thể thay đổi **tài khoản giao dịch, ký hiệu, khung thời gian, bộ tham số và thẻ hình ảnh** (và, cho một backtest, **cửa sổ và tất cả cài đặt backtest** ở trên), sau đó **Save & start** sẽ khởi chạy lại nó với
cài đặt mới (thay thế phiên bản đã dừng). Kiểm soát là **vô hiệu hóa khi phiên bản hoạt động** — chỉ một phiên bản đã dừng mới có thể được chỉnh sửa.

## Chạy từ trình soạn thảo mã

Nhấp vào **Run** trong trình soạn thảo mã sẽ mở một hộp thoại thay vì kích hoạt một lần chạy mù, được mã hóa cứng:

- **Trading account** (bắt buộc) — tài khoản cTrader mà cBot kết nối đến.
- **Parameter set** (tùy chọn) — chọn một bộ hiện có, hoặc để trống để chạy với **các giá trị tham số mặc định** của cBot.
  Một nút **+** bên cạnh bộ chọn tạo một bộ tham số mới theo dòng (xem bên dưới) và chọn nó.
- **Symbol / Timeframe** mặc định là `EURUSD` / `h1` và có thể được thay đổi; **Cancel** hoặc **Run**.

Khi **Run**, trình soạn thảo lưu + xây dựng nguồn hiện tại, bắt đầu phiên bản trên tài khoản được chọn
với các tham số được chọn, sau đó theo dõi các nhật ký vùng chứa trực tiếp. (Luồng nhật ký chuyển tiếp cookie xác thực của người dùng đã đăng nhập đến trung tâm SignalR `/hubs/logs`, vì vậy nó kết nối thay vì không thành công với
`Invalid negotiation response received`.)

## Bộ tham số

Một **bộ tham số** là một bộ có tên, có thể sử dụng lại gồm các ghi đè tham số cBot được lưu trữ dưới dạng một
đối tượng JSON phẳng ánh xạ từng tên tham số đến một giá trị vô hướng, ví dụ: `{"Period": 14, "Label": "trend"}`. Tại
thời gian chạy/backtest, nó được chuyển đổi thành tệp `params.cbotset` cTrader
(`{ "Parameters": { … } }`). Bạn có thể tạo/chỉnh sửa một bộ dưới dạng JSON thô từ hộp thoại **Parameter
sets** của cBot hoặc theo dòng từ hộp thoại Run.

Mỗi bộ tham số **thuộc về một cBot**: hộp thoại New Parameter Set liệt kê tất cả các cBot của bạn và bạn
**phải chọn một** — việc tạo bị chặn cho đến khi một cBot được chọn. **Tên** của bộ **là duy nhất cho mỗi cBot**:
tạo hoặc đổi tên một bộ thành một tên mà một bộ khác của cùng một cBot đã sử dụng bị từ chối (một lỗi rõ ràng
trong hộp thoại, `409 Conflict` tại API). Cùng một tên có thể được sử dụng lại trên một **cBot khác**.

JSON được **xác thực** khi lưu: nó phải là một đối tượng phẳng duy nhất có các giá trị đều là vô hướng
(string / number / bool). Một gốc không phải là đối tượng, một mảng, một đối tượng lồng nhau, một giá trị `null`, hoặc JSON không được định dạng
bị từ chối (một lỗi rõ ràng trong hộp thoại, `400 Bad Request` tại API). Một đối tượng trống `{}`
được phép và có nghĩa là "không có ghi đè".

## Ghi chú CLI cTrader Console

Các backtest cần `--data-mode` (mặc định `m1`), ngày tháng là `dd/MM/yyyy HH:mm`, và
đối số positional JSON `params.cbotset`; `run` từ chối `--data-dir` (chỉ backtest). Xem
`ContainerCommandHelpers`.

## Các nút và quy mô

Công suất thực thi quy mô bằng cách thêm tác nhân nút (tự đăng ký + nhịp tim). Xem
[node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).

## Cần có tài khoản giao dịch

Chạy hoặc backtest một cBot cần một tài khoản giao dịch cTrader để kết nối đến. Cho đến khi bạn thêm một tài khoản dưới
**Trading accounts**, các nút **Run New cBot** / **Backtest New cBot** bị vô hiệu hóa (với một tooltip) và trang sẽ hiển thị một dấu nhắc liên kết đến thiết lập tài khoản — bạn không còn gặp phải một lỗi `stream connect failed` thô
từ một bot không có tài khoản.
