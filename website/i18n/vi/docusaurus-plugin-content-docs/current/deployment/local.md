---
title: Chạy nó cục bộ
description: Nhận cMind chạy trên máy của bạn trong vài phút với Docker Compose (hoặc .NET Aspire cho phát triển).
sidebar_position: 1
---

# Chạy cMind cục bộ 🖥️

Đây là cách nhanh nhất để thấy cMind thực tế — một thực thể đầy đủ trên máy của bạn. Chắc cà phê; bạn
có khả năng được ký vào trước khi nó mát.

:::tip[Những gì bạn sẽ có ở cuối]
Một ứng dụng web chạy tại **localhost:8080**, máy chủ MCP tại **localhost:8081**, cơ sở dữ liệu Postgres,
và một nút công nhân cục bộ sẵn sàng xây dựng và backtest cBots. Tất cả trên máy của bạn, tất cả của bạn.
:::

**Trước khi bạn bắt đầu, bạn cần một trong:**

- **Chỉ Docker** → sử dụng Tùy chọn A (không cần .NET SDK). Được khuyến nghị cho một cái nhìn đầu tiên.
- **.NET 10 SDK + Docker** → sử dụng Tùy chọn B nếu bạn muốn hack trên mã.

Cả hai đường dẫn đều đa nền tảng (Windows / macOS / Linux).

## Tùy chọn A — Docker Compose (không cần .NET SDK)

Prereq: Docker Desktop (hoặc Docker Engine + plugin soạn nhạc).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (ký vào với chủ sở hữu từ `.env`; bắt buộc thay đổi mật khẩu khi đăng nhập lần đầu).
- Máy chủ MCP: <http://localhost:8081/mcp>.
- Dữ liệu Postgres duy trì trong âm lượng `pgdata`; lược đồ di chuyển tự động khi khởi động.

Vùng chứa Web gắn ổ cắm Docker máy chủ (`/var/run/docker.sock`) vì vậy trình xây dựng trong trình duyệt và hạt giống **LocalNode** xây dựng + chạy các vùng chứa cTrader Console trên máy của bạn.

**Ghi chú đa nền tảng**
- Docker Desktop (Windows/macOS) tiếp xúc ổ cắm tại `/var/run/docker.sock` — mount soạn nhạc hoạt động như có.
- Linux: đảm bảo người dùng của bạn có thể truy cập ổ cắm, hoặc chạy soạn nhạc với đặc quyền đủ.
- Hình ảnh Web là `linux/amd64`; trên Apple Silicon Docker chạy nó dưới giả lập.

Dừng và xóa sạch:

```bash
docker compose down          # keep data
docker compose down -v       # also delete the database volume
```

## Tùy chọn B — .NET Aspire (cho phát triển)

Prereq: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire điều phối Postgres, Web, MCP, pgAdmin; dây các chuỗi kết nối + OTLP; mở bảng điều khiển. Đặt thông tin xác thực chủ sở hữu làm tham số Aspire (`OwnerEmail`, `OwnerPassword`).

Chạy chỉ ứng dụng web với Postgres hiện tại:

```bash
dotnet run --project src/Web
```

## Thêm các nút công nhân cục bộ

Hạt giống LocalNode đã chạy công việc trên máy của bạn. Để thực tập **tự động khám phá** cục bộ, hãy bắt đầu đại lý nút chỉ vào ứng dụng Web (xem [khám phá nút](../operations/node-discovery.md)) với `NodeAgent:MainUrl=http://host.docker.internal:8080` và `JoinToken` phù hợp.

## Khắc phục sự cố 🔧

Docker có ý kiến. Đây là những nghi phạm thường gặp:

| Triệu chứng | Nguyên nhân có khả năng & sửa chữa |
|---|---|
| `port is already allocated` trên 8080/8081 | Cái gì khác đang sử dụng cổng. Dừng nó, hoặc thay đổi ánh xạ trong `docker-compose.yml`. |
| Web bắt đầu nhưng xây dựng/backtests không thành công | Ổ cắm Docker không được gắn hoặc không thể truy cập. Trên Linux, hãy đảm bảo người dùng của bạn có thể tiếp cận `/var/run/docker.sock`. |
| `permission denied` trên ổ cắm (Linux) | Thêm người dùng của bạn vào nhóm `docker` (`sudo usermod -aG docker $USER`) và đăng nhập lại, hoặc chạy với đặc quyền đủ. |
| Lần chạy đầu tiên rất chậm | Bản dựng đầu tiên lấy hình ảnh và biên soạn — các lần chạy tiếp theo nhanh hơn nhiều. Trên Apple Silicon hình ảnh web `linux/amd64` chạy dưới mô phỏng. |
