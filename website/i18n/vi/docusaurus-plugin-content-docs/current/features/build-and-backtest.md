---
description: "Build, run, backtest cTrader cBots (C# và Python, cả hai .NET) từ in-browser Monaco IDE, chạy trên official ghcr.io/spotware/ctrader-console image."
---

# Build & backtest cBots

Build, run, backtest cTrader cBots (C# **and** Python, cả hai .NET) từ in-browser Monaco
IDE, chạy trên official `ghcr.io/spotware/ctrader-console` image.

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project với
  `dotnet build` **in throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`), vì vậy untrusted user MSBuild targets không reach host. NuGet restore cached
  across builds via shared volume. Web host cần Docker socket access.
- C# + Python starter templates sống trong `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transition replace entity (id change),
  container id carried over.
- `NodeScheduler` pick least-loaded eligible node; `ContainerDispatcherFactory` route to
  remote node HTTP agent hoặc local Docker dispatcher.
- Completion pollers reconcile exited containers (backtest containers self-exit via
  `--exit-on-stop`); report present → completed (store `ReportJson`), missing → failed.
- Live container logs stream to browser over SignalR; backtest equity curves parsed from
  report + charted.

## cTrader Console CLI notes

Backtests cần `--data-mode` (mặc định `m1`), dates as `dd/MM/yyyy HH:mm`, và
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Xem
`ContainerCommandHelpers`.

## Nodes & scale

Execution capacity scale by adding node agents (self-register + heartbeat). Xem
[node discovery](../operations/node-discovery.md) và [scaling](../deployment/scaling.md).

## Chạy từ trình soạn thảo mã

Nhấp vào **Chạy** trong trình soạn thảo mã sẽ mở một hộp thoại thay vì kích hoạt một lần chạy mù, mã hóa cứng:

- **Tài khoản giao dịch** (bắt buộc) — tài khoản cTrader mà cBot kết nối tới.
- **Bộ tham số** (tùy chọn) — chọn một bộ hiện có, hoặc để trống để chạy với **giá trị tham số mặc định** của cBot. Nút **+** bên cạnh bộ chọn tạo một bộ tham số mới ngay tại chỗ (xem bên dưới) và chọn nó.
- **Ký hiệu / Khung thời gian** mặc định là `EURUSD` / `h1` và có thể thay đổi; **Hủy** hoặc **Chạy**.

Khi **Chạy**, trình soạn thảo lưu và biên dịch mã nguồn hiện tại, khởi động phiên bản trên tài khoản đã chọn với các tham số đã chọn, sau đó theo dõi nhật ký vùng chứa trực tiếp. (Luồng nhật ký chuyển tiếp cookie xác thực của người dùng đã đăng nhập tới hub SignalR `/hubs/logs`, nên nó kết nối thay vì thất bại với `Invalid negotiation response received`.)

## Bộ tham số

**Bộ tham số** là một tập hợp có tên, có thể tái sử dụng các ghi đè tham số cBot, được lưu dưới dạng đối tượng JSON phẳng ánh xạ mỗi tên tham số tới một giá trị vô hướng, ví dụ `{"Period": 14, "Label": "trend"}`. Khi chạy/backtest, nó được chuyển thành tệp cTrader `params.cbotset` (`{ "Parameters": { … } }`). Bạn có thể tạo/chỉnh sửa một bộ dưới dạng JSON thô từ hộp thoại **Bộ tham số** của cBot hoặc ngay tại chỗ từ hộp thoại Chạy.

JSON được **xác thực** khi lưu: nó phải là một đối tượng phẳng duy nhất mà tất cả các giá trị đều là vô hướng (chuỗi / số / bool). Gốc không phải đối tượng, một mảng, một đối tượng lồng nhau, một giá trị `null`, hoặc JSON không đúng định dạng sẽ bị từ chối (lỗi rõ ràng trong hộp thoại, `400 Bad Request` tại API). Một đối tượng rỗng `{}` được cho phép và có nghĩa là "không ghi đè".

## Điều khiển vòng đời phiên bản

Mỗi hàng phiên bản (và trang chi tiết của nó) có các điều khiển đúng theo trạng thái. Một phiên bản **đang hoạt động** hiển thị **Dừng**; một phiên bản **kết thúc** (Đã dừng / Hoàn tất / Thất bại) hiển thị **Bắt đầu (▶)** để khởi chạy lại với cùng cBot, tài khoản, ký hiệu, khung thời gian, bộ tham số và image (một lần chạy khởi động lại thành lần chạy, một backtest thành backtest). Nhấp Dừng sẽ hiển thị thông báo "Đang dừng…" và vô hiệu hóa biểu tượng cho đến khi hoàn tất; một lần chạy mới tạo sẽ xuất hiện ngay trong danh sách — không cần tải lại trang.

Nhật ký console được **lưu lại khi một phiên bản kết thúc** — cho cả lần chạy (khi dừng) lẫn **backtest** (khi hoàn tất) — nên nhật ký của lần chạy gần nhất vẫn xem được trên trang chi tiết và tải xuống được qua biểu tượng **Tải nhật ký**, ngay cả sau khi vùng chứa đã biến mất.
