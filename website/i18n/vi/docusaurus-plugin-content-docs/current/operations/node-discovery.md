---
description: "Các nút CLI cTrader tham gia cụm bằng tự đăng ký + nhịp tim — không nhập thủ công. Cùng một mô hình như các tác nhân Consul/Nomad/kubeadm: tác nhân khởi động biết vị trí nút chính..."
---

# Node auto-discovery

Các nút CLI cTrader tham gia cụm bằng **tự đăng ký + nhịp tim** — không nhập thủ công. Cùng một mô hình như các tác nhân Consul/Nomad/kubeadm: tác nhân khởi động biết vị trí nút chính + bí mật cụm được chia sẻ, sau đó liên tục công bố chính nó.

> Xác minh từ đầu đến cuối trên Docker Compose và cụm `kind` Kubernetes: các tác nhân tự đăng ký, xuất hiện trong DB có thể tiếp cận, tự động đánh dấu không thể tiếp cận khi nhịp tim dừng quá TTL, trở lại trực tuyến khi tiếp tục.

## Nó hoạt động như thế nào

```
CtraderCliNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert CtraderCliNode by name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Đăng ký == nhịp tim.** Tác nhân re-POSTs trên `HeartbeatIntervalSeconds`. Cuộc gọi đầu tiên tạo nút (sự kiện `NodeRegistered`); các lệnh gọi sau làm mới tính sống. Nhịp tim được nối lại sau khi ngừng hoạt động lật nút trở lại có thể tiếp cận (`NodeCameOnline`).
- **Hòa giải tính sống.** `NodeHeartbeatMonitor` đánh dấu các nút có nhịp tim cuối cùng vượt quá `HeartbeatTtl` không thể tiếp cận. Bộ lập lịch (`IsActive`/`AcceptsRun`/`AcceptsBacktest` được gating trên khả năng tiếp cận) dừng đặt công việc cho đến khi chúng báo cáo lại.
- **Yêu sách instance bị mồ côi.** `NodeInstanceReclaimer` (background) chuyển bất kỳ instance không đầu cuối nào bị mắc kẹt trên nút không thể tiếp cận thành **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, sự kiện miền `InstanceFailed` → thông báo người dùng), vì vậy nút bị crash/được phân vùng không bao giờ có thể để instance bị mắc kẹt "Running" mãi mãi. Yêu sách chỉ kích hoạt sau khi nhịp tim cuối cùng của nút cũ hơn `HeartbeatTtl + InstanceReclaimGrace`, cung cấp cho blip ngắn một cơ hội phục hồi trước. Các **lần chạy được yêu sách không được tự động lên lịch lại**: một nút được phân vùng nhưng sống có thể vẫn đang thực thi thùng chứa và không có hàng rào cấp thùng chứa, vì vậy khởi chạy lại sẽ có nguy cơ thực thi kép — người dùng khởi động lại chạy được yêu sách một cách cố ý. Backtests tự thoát, vì vậy một backtest được yêu sách chỉ được chạy lại.
- **Nhận dạng là tên nút.** Main upserts bằng `NodeName`, vì vậy pod có IP/URL thay đổi khi khởi động lại giữ nhận dạng, re-registers `AdvertiseUrl` mới.
- **Chế độ cố định tại đăng ký đầu tiên.** Chế độ nút (`Run`/`Backtest`/`Mixed`) là loại tồn tại, không thể thay đổi trên nhịp tim; tái đăng ký với chế độ khác được tôn trọng cho tính sống nhưng thay đổi chế độ bị bỏ qua (ghi lại dưới dạng cảnh báo). Để thay đổi chế độ: xóa nút, để nó tái đăng ký.

## Cấu hình

Main (Web) — `App:Discovery`:

| Khóa | Mặc định | Ý nghĩa |
|-----|---------|---------|
| `Enabled` | `false` | Công tắc chính cho điểm cuối đăng ký + monitor. |
| `JoinToken` | — | Bí mật cụm được chia sẻ (≥ 32 ký tự) các tác nhân phải trình bày. |
| `HeartbeatTtl` | `00:01:30` | Ân huệ trước khi nút im lặng được đánh dấu không thể tiếp cận. |
| `InstanceReclaimGrace` | `00:01:00` | Lề bổ sung vượt quá `HeartbeatTtl` trước khi instance bị mắc kẹt trên nút không thể tiếp cận được yêu sách (thất bại). |
| `MonitorInterval` | `00:00:30` | Tần suất monitor và instance-reclaimer quét. |
| `HeartbeatInterval` | `00:00:30` | Giá trị được trả về cho các tác nhân dưới dạng nhịp điệu được đề xuất. |

Agent (CtraderCliNode) — `NodeAgent`:

| Khóa | Ý nghĩa |
|-----|---------|
| `MainUrl` | URL cơ bản của nút chính. Trống = chế độ đăng ký thủ công (vòng lặp no-op). |
| `AdvertiseUrl` | URL main sử dụng để đạt tới tác nhân **này**. |
| `NodeName` | Tên duy nhất; mặc định là tên máy nếu trống. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Gợi ý dung lượng được tôn trọng bởi bộ lập lịch. |
| `HeartbeatIntervalSeconds` | Tái đăng ký nhịp điệu. |
| `JwtSecret` | Phải bằng `JoinToken` của main — cả khóa ký HS256 JWT ký gửi và điều phối. |

## Mô hình bảo mật (v1)

Các nút tự động đăng ký chia sẻ **một bí mật cụm** (`JoinToken` == `JwtSecret` của mỗi tác nhân). Main ký từng yêu cầu điều phối dưới dạng JWT HS256 5 phút với bí mật đó; tác nhân xác nhận. Yêu cầu:

- Giữ `JoinToken` ≥ 32 ký tự và xoay nó (cập nhật `App:Discovery:JoinToken` của main và `NodeAgent:JwtSecret` của mỗi tác nhân cùng nhau).
- Kết thúc TLS phía trước chính và tác nhân trong sản xuất (proxy ngược / ingress).
- Tác nhân vẫn chỉ chạy hình ảnh phù hợp với `AllowedImagePrefix`.

**Hardening follow-up (không v1):** phát hành bí mật duy nhất cho mỗi nút tại đăng ký (kubeadm-style bootstrap → thông tin xác thực cho mỗi nút) vì vậy tác nhân bị xâm phạm duy nhất không thể giả mạo mã thông báo điều phối cho đối tác. Luồng đăng ký đã trả về phần nội dung phản hồi — nơi tự nhiên để gửi lại bí mật được bán kèm cho mỗi nút.

## Các nút thủ công vẫn hoạt động

`POST /api/nodes` (giao diện người dùng quản trị viên) tiếp tục đăng ký các nút được ghim với bí mật riêng cho mỗi nút. Khám phá là bổ sung.

Triển khai white-label có thể **ẩn các điều khiển thủ công** (hoặc bề mặt Nodes toàn bộ) và dựa vào khám phá tự động thuần túy: `App:Branding:NodesUi=Monitor` thả thêm/xóa thủ công, `Hidden` xóa nav, trang và API thủ công, và `App:Branding:RestrictNodesToOwner` tầng bề mặt ở owner-only. Điểm cuối tự đăng ký + nhịp tim ở đây không bị ảnh hưởng trong mọi chế độ. Xem [White-label → Nodes UI visibility](../features/white-label.md#nodes-ui-visibility).
