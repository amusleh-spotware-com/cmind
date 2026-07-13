---
title: 0001 — DDD Nghiêm ngặt với một Core thuần
description: Tại sao logic miền sống trên các tổng thể trong dự án Core không có phụ thuộc cơ sở hạ tầng.
---

# 0001 — DDD Nghiêm ngặt với một `Core` thuần

## Ngữ cảnh

Ứng dụng này di chuyển tiền thực. Các quy tắc kinh doanh phân tán trên các điểm cuối, dịch vụ nền và các thành phần Razor
thối nát thành hành vi không thể kiểm tra được, không nhất quán — chính xác nơi một lỗi chi phí một người dùng vốn.

## Quyết định

Logic miền sống **trên tổng thể, đối tượng giá trị và dịch vụ miền** trong `src/Core`, mà
biên soạn với **không có phụ thuộc cơ sở hạ tầng** (không EF, HttpClient, Docker hoặc ASP.NET). Các điểm cuối,
công cụ MCP, thành phần và `BackgroundService`s **orchestrate** — họ không bao giờ quyết định. Quy tắc:

- Không có bộ định giá công khai; thay đổi trạng thái thông qua các phương thức tiết lộ ý định bảo vệ bất biến.
- Các tổng thể tham chiếu lẫn nhau bằng **ID mạnh**, không phải tính chất điều hướng.
- Một `SaveChanges` đột biến **one** tổng thể; các luồng giữa các tổng thể sử dụng sự kiện miền.
- Các nguyên thủy vượt qua ranh giới miền được bao bọc trong các đối tượng giá trị.
- Những vi phạm bất biến ném một Core `DomainException`, không phải ngoại lệ framework.

## Hậu quả

- Các quy tắc miền có thể kiểm tra đơn vị mà không cần cơ sở dữ liệu hoặc web host.
- `Core` purity được thực thi bằng máy bởi `ArchitectureGuardTests` và sẽ không thành công khi được phá vỡ.
- Có nhiều lễ nghi (đối tượng giá trị, ID mạnh, sự kiện miền) hơn một mô hình thiếu máu — đây là
  chi phí cố ý để giữ cho các quy tắc di chuyển tiền chính xác và ở một nơi.
