---
title: Bản ghi Quyết định Kiến trúc
description: Những quyết định thiết kế không rõ ràng đằng sau cMind — ngữ cảnh, quyết định và hậu quả — mà bạn không thể đọc từ mã.
---

# Bản ghi Quyết định Kiến trúc

Những cái này ghi lại những quyết định thiết kế bạn **can't infer from the code** — những thoả hiệp, con đường không lấy
và tại sao. Mỗi cái là ngắn: *Context → Decision → Consequences*. Quyết định cấu trúc mới → thêm một
ADR ở đây (số tiếp theo) để kỹ sư tiếp theo (con người hoặc AI) kế thừa lý do, không chỉ là
kết quả.

| # | Quyết định |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | DDD nghiêm ngặt với một `Core` thuần |
| [0002](./0002-tph-instance-replaces-entity.md) | Trạng thái thực thể là TPH; một quá trình chuyển thay thế thực thể |
| [0003](./0003-external-nodes-http-jwt.md) | Các nút CLI cTrader là HTTP + JWT, không SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` chạy trên web host trong một vùng chứa sandbox |
| [0005](./0005-anthropic-raw-http.md) | Máy khách AI sử dụng HTTP thô, không phải Anthropic SDK |
| [0006](./0006-copy-profile-db-lease.md) | Lưu trữ sao chép được điều phối bởi một khoá DB nguyên tử |
