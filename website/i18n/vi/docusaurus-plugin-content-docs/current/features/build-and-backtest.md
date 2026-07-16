---
description: "Xây dựng, chạy, backtest cBots cTrader (C# và Python, cả hai .NET) từ trình soạn thảo Monaco trong trình duyệt, chạy trên hình ảnh ghcr.io/spotware/ctrader-console chính thức."
---

# Xây dựng & backtest cBots

Xây dựng, chạy, backtest cBots cTrader (C# **và** Python, cả hai .NET) từ trình soạn thảo Monaco trong trình duyệt, chạy trên hình ảnh chính thức `ghcr.io/spotware/ctrader-console`.

## Xây dựng

- Trang **Builder** lưu trữ trình soạn thảo Monaco; `CBotBuilder` biên dịch dự án với `dotnet build` **trong container tạm thời** (`AppOptions.BuildImage`, thư mục làm việc được gắn tại `/work`), vì vậy các mục tiêu MSBuild của người dùng không đáng tin cậy không thể truy cập máy chủ. NuGet restore được lưu trong bộ nhớ đệm trên các bản dựng thông qua volume được chia sẻ. Web host cần quyền truy cập socket Docker.
- Các mẫu khởi động C# + Python nằm trong `src/Nodes/Builder/Templates/`.

## Chạy & backtest

- **Instances** = phân cấp trạng thái TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Quá trình chuyển đổi thay thế thực thể (thay đổi id), id container được mang theo.
- `NodeScheduler` chọn Node đủ điều kiện ít tải nhất; `ContainerDispatcherFactory` định tuyến đến agent HTTP Node từ xa hoặc bộ gửi Docker cục bộ.
- Các bộ khảo sát hoàn thành điều hòa các container đã thoát (các container backtest tự thoát qua `--exit-on-stop`); báo cáo hiện tại → hoàn thành (lưu `ReportJson`), mất → thất bại.
- Nhật ký container trực tiếp được phát trực tuyến đến trình duyệt qua SignalR; đường cong vốn backtest được phân tích từ báo cáo + biểu đồ.

## Dữ liệu thị trường backtest được lưu trong bộ nhớ đệm cho mỗi tài khoản

cTrader Console tải xuống dữ liệu tick/bar lịch sử vào `--data-dir` của nó. Thư mục đó là một **bộ nhớ đệm ổn định, liên tục được khóa trên tài khoản giao dịch** (số tài khoản của nó) — được gắn từ đĩa của Node tại đường dẫn container của nó (`/mnt/data`), một **gắn riêng biệt, không lồng nhau** từ thư mục làm việc cho mỗi thể hiện. Vì vậy, mỗi backtest trên cùng tài khoản **tái sử dụng** dữ liệu đã tải xuống thay vì tải xuống lại mỗi lần chạy. (Trước đó, thư mục dữ liệu nằm dưới thư mục làm việc cho mỗi thể hiện, có id thay đổi mỗi lần chạy, điều này buộc phải tải xuống mới mỗi lần backtest.) Thư mục làm việc tạm thời cho mỗi thể hiện vẫn chứa algo, params, mật khẩu và báo cáo; bộ nhớ đệm dữ liệu được chia sẻ được tính vào mức sử dụng dữ liệu backtest của một Node và bị xóa bởi hành động làm sạch Node.

## Cài đặt backtest

Hộp thoại **Backtest** hiển thị các cài đặt backtest cTrader Console có thể điều chỉnh của người dùng, do đó bạn không bao giờ phải chạm vào dòng lệnh:

- **Symbol / Timeframe** — timeframe là một **danh sách thả xuống của mỗi khoảng thời gian cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, và các khoảng thời gian Renko/Range/Heikin), trong cách viết chính tắc chính thức của bộ điều khiển, do đó bạn luôn chọn một `--period` hợp lệ.
- **From / To** — cửa sổ backtest (`--start` / `--end`).
- **Data mode** — một trong ba chế độ cTrader (`--data-mode`): **Tick data** (`tick`, chính xác), **m1 bars** (`m1`, nhanh), hoặc **Open prices only** (`open`, nhanh nhất).
- **Starting balance** — mặc định là `10000` (`--balance`). Một **số dư 0 không thực hiện bất kỳ giao dịch nào và khiến cTrader phát ra một báo cáo trống mà sau đó nó sẽ gặp sự cố** ("Message expected"), vì vậy luôn gửi một số dư khác 0.
- **Commission** — `--commission`.
- **Spread** — `--spread`, một **trường số trong pips không thể dưới 0**. Nó **ẩn ở chế độ Tick data**, nơi cTrader lấy spread từ chính dữ liệu tick (không gửi `--spread`).

Thư mục dữ liệu (`--data-file` / `--data-dir`) được quản lý bởi chính ứng dụng (bộ nhớ đệm cho mỗi tài khoản, xem trên), không được hiển thị trong hộp thoại.

:::note cTrader gặp sự cố trên một backtest trống
Nếu một backtest không tạo ra **kết quả nào** — không có giao dịch, hoặc không có dữ liệu thị trường cho các ngày/ký hiệu được chọn — trình ghi báo cáo của chính cTrader Console sẽ ném `Message expected` và thoát mà không có báo cáo. Ứng dụng không thể sửa lỗi ngược dòng đó, nhưng nó phát hiện ra và đánh dấu thể hiện **Failed** với một lý do hành động ("không có kết quả backtest cho phạm vi được chọn…") thay vì một dấu vết ngăn xếp thô. Chọn một phạm vi ngày rộng hơn có dữ liệu thị trường có sẵn và thử lại.
:::

## Trang chi tiết thể hiện

Mở một thể hiện (`/instance/{id}`) hiển thị trạng thái trực tiếp, nhật ký của nó và — đối với một backtest — đường cong vốn. **Tiêu đề tab trình duyệt** phản ánh thể hiện cụ thể (**tên cBot · kind · symbol**, ví dụ: `TrendBot · Backtest · EURUSD`) vì vậy một tab chạy trực tiếp và một tab backtest có thể phân biệt được ngay lập tức. Một lần chạy và một backtest của cùng cBot được theo dõi như các **lineages** riêng biệt (một id lineage ổn định được mang theo các chuyển tiếp trạng thái), vì vậy trang tuân theo chính xác một thể hiện và không bao giờ trộn dữ liệu chạy với dữ liệu backtest.

## Các điều khiển vòng đời thể hiện

Mỗi hàng thể hiện (và trang chi tiết của nó) có các điều khiển đúng trạng thái. Một thể hiện **hoạt động** hiển thị **Stop**; một thể hiện **terminal** (Stopped / Completed / Failed) hiển thị **Start (▶)** để khởi chạy lại nó bằng cBot, tài khoản, symbol, timeframe, ParamSet và image tương tự (một lần chạy khởi động lại như một lần chạy, một backtest như một backtest). Nhấp vào Stop hiển thị thông báo "Stopping…" và vô hiệu hóa biểu tượng cho đến khi nó giải quyết, và một lần chạy mới được tạo xuất hiện trong danh sách ngay lập tức — không cần tải lại trang.

Nhật ký bảng điều khiển được **lưu khi một thể hiện chấm dứt** — đối với một lần chạy (trên Stop) và đối với một **backtest** (khi hoàn thành) — vì vậy nhật ký chạy cuối cùng vẫn có thể xem được trên trang chi tiết và, thông qua thanh công cụ nhật ký, **sao chép vào bộ nhớ tạm** (biểu tượng Sao chép nhật ký) hoặc **tải xuống** (biểu tượng Tải xuống nhật ký) ngay cả sau khi container biến mất. Cả hai đều hoạt động trên nhật ký bảng điều khiển đầy đủ của thể hiện, không chỉ phần đuôi trên màn hình.

Một backtest **hoàn thành** cũng lưu giữ **báo cáo cTrader** của nó ở cả hai định dạng — **JSON** thô (cái giống như đường cong vốn và phân tích AI đọc) và báo cáo **HTML** đầy đủ. Cả hai đều có thể tải xuống từ hàng backtest **và** trang chi tiết thông qua các biểu tượng chuyên dụng. Chỉ **báo cáo chạy cuối cùng** được giữ lại, và các biểu tượng là **vô hiệu hóa** cho bất kỳ backtest nào không được bắt đầu, đang chạy hoặc thất bại (và không bao giờ được hiển thị cho một thể hiện chạy) — chỉ một backtest hoàn thành mới có báo cáo để tải xuống.

Một `.algo` **được tải lên** chưa bao giờ được xây dựng ở đây, do đó cột **Last Build** của nó trên trang cBots bị bỏ trống (nó chỉ hiển thị thời gian xây dựng cho các cBots bạn xây dựng trong trình duyệt).

## Chỉnh sửa & chạy lại một thể hiện đã dừng

Một thể hiện **đã dừng** (chạy hoặc backtest) có một điều khiển **Edit** — một biểu tượng trên hàng của nó trong danh sách **và** bên cạnh Start/Stop trên trang chi tiết của nó — mở một hộp thoại **điền sẵn** với cấu hình hiện tại của nó. Bạn có thể thay đổi **tài khoản giao dịch, symbol, timeframe, ParamSet và image tag** (và, đối với một backtest, **cửa sổ và tất cả cài đặt backtest** ở trên), sau đó **Save & start** khởi chạy lại nó với các cài đặt mới (thay thế thể hiện đã dừng). Điều khiển là **vô hiệu hóa khi thể hiện hoạt động** — chỉ một thể hiện đã dừng có thể được chỉnh sửa.

## Chạy từ trình soạn thảo mã

Nhấp vào **Run** trong trình soạn thảo mã mở một hộp thoại thay vì kích hoạt một lần chạy mù, được mã hóa cứng:

- **Trading account** (bắt buộc) — tài khoản cTrader mà cBot kết nối.
- **Parameter set** (tùy chọn) — chọn một bộ hiện có, hoặc để trống để chạy với **giá trị tham số mặc định** của cBot. Nút **+** bên cạnh bộ chọn tạo một ParamSet mới nội tuyến (xem bên dưới) và chọn nó.
- **Symbol / Timeframe** mặc định là `EURUSD` / `h1` và có thể thay đổi; **Cancel** hoặc **Run**.

Trên **Run** trình soạn thảo lưu + xây dựng nguồn hiện tại, khởi động thể hiện trên tài khoản được chọn với các tham số được chọn, sau đó xem nhật ký container trực tiếp. (Luồng nhật ký chuyển tiếp cookie xác thực của người dùng đã ký vào hub SignalR `/hubs/logs`, vì vậy nó kết nối thay vì bị lỗi với `Invalid negotiation response received`.)

## Bộ tham số

Một **parameter set** là một bộ ghi đè tham số cBot có tên, có thể tái sử dụng được lưu dưới dạng một đối tượng JSON phẳng ánh xạ mỗi tên tham số với một giá trị vô hướng, ví dụ: `{"Period": 14, "Label": "trend"}`. Tại thời gian chạy/backtest, nó được chuyển thành tệp cTrader `params.cbotset` (`{ "Parameters": { … } }`). Bạn có thể tạo/chỉnh sửa một bộ dưới dạng JSON thô từ hộp thoại **Parameter sets** của cBot hoặc nội tuyến từ hộp thoại Run.

Mỗi ParamSet **thuộc về một cBot**: hộp thoại New Parameter Set liệt kê tất cả cBots của bạn và bạn **phải chọn một** — việc tạo bị chặn cho đến khi cBot được chọn. **Tên của một bộ là duy nhất cho mỗi cBot**: tạo hoặc đổi tên một bộ thành tên mà một bộ khác của cBot tương tự đã sử dụng sẽ bị từ chối (lỗi rõ ràng trong hộp thoại, `409 Conflict` tại API). Tên tương tự có thể được tái sử dụng trên một **cBot khác**.

JSON được **xác thực** khi lưu: nó phải là một đối tượng phẳng duy nhất có tất cả các giá trị đều là vô hướng (chuỗi / số / bool). Một gốc không phải đối tượng, một mảng, một đối tượng lồng nhau, một giá trị `null`, hoặc JSON bị biến dạng sẽ bị từ chối (lỗi rõ ràng trong hộp thoại, `400 Bad Request` tại API). Một đối tượng trống `{}` được phép và có nghĩa là "không có ghi đè".

## Ghi chú CLI cTrader Console

Backtests cần `--data-mode` (mặc định `m1`), ngày dưới dạng `dd/MM/yyyy HH:mm`, và đối số vị trí JSON `params.cbotset`; `run` từ chối `--data-dir` (chỉ backtest). Xem `ContainerCommandHelpers`.

## Nodes & quy mô

Công suất thực thi mở rộng bằng cách thêm agent Nodes (tự đăng ký + heartbeat). Xem [node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).
## Cần một tài khoản giao dịch

Chạy hoặc backtesting một cBot cần một tài khoản giao dịch cTrader để kết nối. Cho đến khi bạn thêm một tài khoản trong **Trading accounts**, các nút **Run New cBot** / **Backtest New cBot** bị vô hiệu hóa (có tooltip) và trang hiển thị một lời nhắc liên kết đến thiết lập tài khoản — bạn không còn gặp lỗi `stream connect failed` thô từ một bot không có tài khoản.
