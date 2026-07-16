---
description: "Xây dựng, chạy, backtest cBots cTrader (C# và Python, cả hai .NET) từ Monaco IDE trong trình duyệt, chạy trên hình ảnh chính thức ghcr.io/spotware/ctrader-console."
---

# Xây dựng & backtest cBots

Xây dựng, chạy, backtest cBots cTrader (C# **và** Python, cả hai .NET) từ Monaco IDE trong
trình duyệt, chạy trên hình ảnh chính thức `ghcr.io/spotware/ctrader-console`.

## Xây dựng

- Trang **Builder** lưu trữ trình chỉnh sửa Monaco; `CBotBuilder` biên dịch dự án với `dotnet build`
  **trong vùng chứa tạm thời** (`AppOptions.BuildImage`, thư mục làm việc được gắn kết tại `/work`),
  do đó các mục tiêu MSBuild của người dùng không đáng tin cậy không đạt đến máy chủ. Khôi phục NuGet
  được lưu trong bộ nhớ cache trên các bản dựng thông qua một tập hợp được chia sẻ. Máy chủ Web cần
  quyền truy cập vào ổ cắm Docker.
- Mẫu bắt đầu C# + Python nằm trong `src/Nodes/Builder/Templates/`.

## Chạy & backtest

- **Instances** = phân cấp trạng thái TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Quá trình chuyển đổi thay thế thực thể (thay đổi id),
  id vùng chứa được mang theo.
- `NodeScheduler` chọn nút đủ điều kiện có tải nhất ít; `ContainerDispatcherFactory` định tuyến đến
  đại lý HTTP nút từ xa hoặc bộ điều phối Docker cục bộ.
- Các bộ khảo sát hoàn thành sẽ điều hòa các vùng chứa đã thoát (các vùng chứa backtest tự thoát
  thông qua `--exit-on-stop`); báo cáo hiện diện → hoàn thành (lưu trữ `ReportJson`), mất → không
  thành công.
- Nhật ký vùng chứa trực tiếp truyền phát đến trình duyệt qua SignalR; đường cong vốn backtest được
  phân tích cú pháp từ báo cáo + được vẽ biểu đồ.

## Dữ liệu thị trường Backtest được lưu trong bộ nhớ cache trên mỗi tài khoản

cTrader Console tải xuống dữ liệu tick/bar lịch sử vào `--data-dir` của nó. Thư mục đó là **bộ nhớ
cache ổn định, liên tục được khóa trên tài khoản giao dịch** (số tài khoản của nó) — được gắn kết từ
đĩa của nó tại đường dẫn vùng chứa của nó (`/mnt/data`), **một kết nối riêng biệt, không lồng nhau**
từ thư mục làm việc cho mỗi phiên bản. Vì vậy, mọi backtest trên cùng một tài khoản **sẽ sử dụng lại**
dữ liệu đã tải xuống thay vì tải xuống lại nó ở mỗi lần chạy. (Trước đó, thư mục dữ liệu nằm trong
thư mục làm việc cho mỗi phiên bản, có id thay đổi ở mỗi lần chạy, điều này buộc phải tải xuống lại
ở mỗi lần backtest.) Thư mục làm việc tạm thời cho mỗi phiên bản vẫn chứa algo, params, mật khẩu
và báo cáo; bộ nhớ cache dữ liệu được chia sẻ được tính trong việc sử dụng dữ liệu backtest của nó
và được xóa bởi hành động làm sạch nút.

## Cài đặt Backtest

Hộp thoại **Backtest** hiển thị cài đặt backtest cTrader Console có thể điều chỉnh bởi người dùng,
do đó bạn không bao giờ phải chạm vào dòng lệnh:

- **Symbol / Timeframe** — timeframe là **danh sách thả xuống của mỗi khoảng thời gian cTrader**
  (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` và các khoảng thời gian
  Renko/Range/Heikin), trong cách viết chữ hoa chính tắc của bảng điều khiển, do đó bạn luôn chọn
  một `--period` hợp lệ.
- **From / To** — cửa sổ backtest (`--start` / `--end`).
- **Data mode** — một trong ba chế độ cTrader (`--data-mode`): **Tick data** (`tick`, chính xác),
  **m1 bars** (`m1`, nhanh), hoặc **Open prices only** (`open`, nhanh nhất).
- **Starting balance** — mặc định là `10000` (`--balance`). Một **số dư bằng 0 không đặt các giao dịch
  và khiến cTrader phát ra một báo cáo trống sau đó nó sẽ sập** ("Message expected"), do đó, số dư
  khác không luôn được gửi.
- **Commission** và **Spread** — `--commission` / `--spread` (spread trong pips).

Thư mục dữ liệu (`--data-file` / `--data-dir`) được quản lý bởi chính ứng dụng (bộ nhớ cache cho
mỗi tài khoản, xem trên), không được hiển thị trong hộp thoại.

## Trang chi tiết phiên bản

Mở một phiên bản (`/instance/{id}`) để hiển thị trạng thái trực tiếp, nhật ký của nó và — đối với
một backtest — đường cong vốn của nó. **Tiêu đề tab trình duyệt** phản ánh phiên bản cụ thể
(**tên cBot · loại · biểu tượng**, ví dụ: `TrendBot · Backtest · EURUSD`) do đó một tab chạy trực tiếp
và một tab backtest có thể phân biệt được trong thoáng chốc. Một chạy và một backtest của cùng một cBot
được theo dõi như các **dòng dõi** riêng biệt (một id dòng dõi ổn định được mang theo các chuyển đổi
trạng thái), do đó trang theo dõi chính xác một phiên bản và không bao giờ trộn dữ liệu chạy với một
backtest.

## Các điều khiển vòng đời của phiên bản

Mỗi hàng phiên bản (và trang chi tiết của nó) có các điều khiển đúng trạng thái. Một phiên bản
**hoạt động** hiển thị **Stop**; một phiên bản **terminal** (Stopped / Completed / Failed) hiển thị
**Start (▶)** để khởi chạy lại nó với cùng một cBot, tài khoản, biểu tượng, timeframe, bộ tham số
và hình ảnh (một lần chạy khởi động lại dưới dạng chạy, một backtest dưới dạng backtest). Nhấp vào
Stop sẽ hiển thị thông báo "Stopping…" và tắt biểu tượng cho đến khi nó giải quyết, và một lần chạy
mới được tạo sẽ xuất hiện trong danh sách ngay lập tức — không cần tải lại trang.

Nhật ký bảng điều khiển được **tồn tại khi một phiên bản kết thúc** — cho một lần chạy (trên Stop)
và cho một **backtest** (khi hoàn thành) như nhau — do đó, nhật ký của lần chạy cuối cùng vẫn có thể
xem được trên trang chi tiết và, thông qua thanh công cụ nhật ký, **sao chép vào bộ nhớ tạm**
(biểu tượng Sao chép nhật ký) hoặc **tải xuống** (biểu tượng Tải xuống nhật ký) thậm chí sau khi vùng
chứa biến mất. Cả hai đều hoạt động trên toàn bộ nhật ký bảng điều khiển của phiên bản, không chỉ
là phần đuôi trên màn hình.

Một `.algo` được **tải lên** chưa bao giờ được xây dựng ở đây, vì vậy cột **Last Build** của nó trên
trang cBots được để trống (nó chỉ hiển thị một thời gian xây dựng cho các cBot bạn xây dựng trong
trình duyệt).

## Chỉnh sửa & chạy lại một phiên bản đã dừng

Một phiên bản **đã dừng** (chạy hoặc backtest) có một điều khiển **Edit** — một biểu tượng trên hàng
của nó trong danh sách **và** bên cạnh Start/Stop trên trang chi tiết của nó — mở một hộp thoại
**được điền sẵn** với cấu hình hiện tại của nó. Bạn có thể thay đổi **tài khoản giao dịch, biểu tượng,
timeframe, bộ tham số và thẻ hình ảnh** (và, đối với một backtest, **cửa sổ và tất cả cài đặt backtest**
ở trên), sau đó **Save & start** khởi chạy lại nó với cài đặt mới (thay thế phiên bản đã dừng). Điều
khiển này **bị tắt khi phiên bản hoạt động** — chỉ một phiên bản đã dừng mới có thể được chỉnh sửa.

## Chạy từ trình chỉnh sửa mã

Nhấp vào **Run** trong trình chỉnh sửa mã sẽ mở một hộp thoại thay vì kích hoạt một lần chạy mù,
được mã hóa cứng:

- **Trading account** (bắt buộc) — tài khoản cTrader mà cBot kết nối với.
- **Parameter set** (tùy chọn) — chọn một bộ hiện có, hoặc để nó trống để chạy với **các giá trị
  tham số mặc định của cBot**. Nút **+** bên cạnh bộ chọn tạo một bộ tham số mới inline (xem bên
  dưới) và chọn nó.
- **Symbol / Timeframe** mặc định là `EURUSD` / `h1` và có thể được thay đổi; **Cancel** hoặc **Run**.

Khi **Run**, trình chỉnh sửa lưu + xây dựng nguồn hiện tại, khởi động phiên bản trên tài khoản được
chọn với các tham số đã chọn, sau đó theo dõi nhật ký vùng chứa trực tiếp. (Luồng nhật ký chuyển
tiếp cookie xác thực của người dùng đã đăng nhập tới trung tâm SignalR `/hubs/logs`, do đó nó kết nối
thay vì không thành công với `Invalid negotiation response received`.)

## Bộ tham số

Một **parameter set** là một bộ ghi đè tham số cBot có tên, có thể sử dụng lại được lưu trữ dưới
dạng một đối tượng JSON phẳng ánh xạ từng tên tham số thành một giá trị vô hướng, ví dụ:
`{"Period": 14, "Label": "trend"}`. Tại thời điểm chạy/backtest, nó được chuyển thành tệp
`params.cbotset` của cTrader (`{ "Parameters": { … } }`). Bạn có thể tạo/chỉnh sửa một bộ dưới
dạng JSON thô từ hộp thoại **Parameter sets** của cBot hoặc inline từ hộp thoại Run.

Mỗi bộ tham số **thuộc về một cBot**: hộp thoại Bộ tham số mới liệt kê tất cả các cBot của bạn và
bạn **phải chọn một** — việc tạo bị chặn cho đến khi chọn một cBot. **Tên của một bộ là duy nhất cho
mỗi cBot**: việc tạo hoặc đổi tên một bộ thành một tên mà một bộ khác của cùng một cBot đã sử dụng
sẽ bị từ chối (một lỗi rõ ràng trong hộp thoại, `409 Conflict` tại API). Tên tương tự có thể được
sử dụng lại trên một **cBot khác**.

JSON được **xác thực** khi lưu: nó phải là một đối tượng phẳng duy nhất có các giá trị là tất cả
các vô hướng (chuỗi / số / bool). Một gốc không phải đối tượng, một mảng, một đối tượng lồng nhau,
một giá trị `null`, hoặc JSON không được định dạng sẽ bị từ chối (một lỗi rõ ràng trong hộp thoại,
`400 Bad Request` tại API). Một đối tượng trống `{}` được cho phép và có nghĩa là "không có ghi đè".

## Ghi chú CLI cTrader Console

Backtests cần `--data-mode` (mặc định `m1`), ngày làm `dd/MM/yyyy HH:mm`, và đối số vị trí
`params.cbotset` JSON; `run` từ chối `--data-dir` (chỉ backtest). Xem `ContainerCommandHelpers`.

## Nút & tỷ lệ

Dung lượng thực thi tỷ lệ bằng cách thêm các đại lý nút (tự đăng ký + heartbeat). Xem
[node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).

## Yêu cầu tài khoản giao dịch

Chạy hoặc backtest một cBot cần một tài khoản giao dịch cTrader để kết nối. Cho đến khi bạn thêm
một trong phần **Trading accounts**, các nút **Run New cBot** / **Backtest New cBot** bị vô hiệu
hóa (có chú thích công cụ) và trang hiển thị lời nhắc liên kết đến thiết lập tài khoản — bạn
không còn gặp lỗi `stream connect failed` từ một bot không có tài khoản.
