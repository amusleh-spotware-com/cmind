---
slug: /for-traders
title: cMind cho các trader cTrader
description: Tại sao một trader cTrader nên tự host cMind — sở hữu stack và dữ liệu của bạn, tác giả, backtest, chạy và giám sát cBots trong một bảng điều khiển được hỗ trợ AI, trên máy tính xách tay, VPS hoặc điện thoại của bạn.
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind cho các trader cTrader 📈

Bạn đã giao dịch trên cTrader. Bạn đã lúc nào đó juggle một trình biên tập mã, một backtester, một VPS và ba
tab trình duyệt. **cMind sụp đổ tất cả điều đó thành một bảng điều khiển tối thân thiện với bàn phím mà bạn chạy
yourself** — và nó là mã nguồn mở, vì vậy không có gì về lợi thế của bạn, chiến lược của bạn hoặc thông tin xác thực của bạn
bao giờ rời khỏi hộp của bạn.

:::tip[TL;DR]
Tự host cMind trên máy tính xách tay, VPS rẻ hoặc máy chủ nhà. Tác giả, backtest, chạy và giám sát cBots
trong một nơi, với lõi AI thực hiện công việc. → [Chạy nó trong 5 phút](./deployment/local.md)
:::

## Tại sao tự host thay vì một dịch vụ được host?

- **Sở hữu stack và dữ liệu của bạn.** cBots, thông tin xác thực, token và lịch sử vốn của bạn sống trên
  **your** cơ sở hạ tầng — không có bên thứ ba, không có khóa, không có email "chúng tôi đang mặt trời lặn sản phẩm này".
- **Nó thực sự của bạn để thay đổi.** C# 14 / .NET 10, DDD nghiêm ngặt, EF Core + PostgreSQL, một MCP
  máy chủ — tất cả mã nguồn mở và có thể hack. Rẽ nhánh nó, mở rộng nó, gửi một PR.
- **Không có tường phí mỗi tính năng.** Mang API key của riêng bạn cho bất kỳ nhà cung cấp; mọi tính năng AI là trên.

Thích không chạy các máy chủ yourself? Công ty lưu trữ có thể chạy một cMind được quản lý cho bạn —
xem [Cho các nhà cung cấp cloud & VPS](./for-cloud-providers.md).

## Một bảng điều khiển, không juggling tab

- **Tác giả** trong một Monaco IDE thực (trình soạn thảo VS Code), với C# **và** các template Python và
  `dotnet build` được cách ly trong các vùng chứa có thể loại bỏ. → [Xây dựng & Backtest](./features/build-and-backtest.md)
- **Backtest** trên một fleet các nút và xem đường cong vốn phát trực tiếp.
- **Chạy** chiến lược trực tiếp và **giám sát** chúng từ một bảng điều khiển. → [Bảng điều khiển](./features/dashboard.md)
- **Sao chép** tài khoản chính vào nhiều tài khoản trên các broker và ID cTrader, với hòa giải
  nó sống sót những kết nối bỏ rơi và các token quay. → [Sao chép giao dịch](./features/copy-trading.md)

## AI làm công việc, không nói chuyện nhỏ

Mang API key của riêng bạn (bất kỳ nhà cung cấp được hỗ trợ — cloud hoặc mô hình cục bộ) và nhận từ tiếng Anh đơn giản → a
cBot biên soạn thực tế với một vòng lặp tự sửa chữa, điều chỉnh tham số, autopsy backtest và một lệnh bảo vệ
có thể tự động dừng một bot cư xử sai. → [Gặp lõi AI](./features/ai.md)

## Công cụ cấp độ tổ chức, cho một

Sự kỷ luật tương tự một bàn làm việc trả tiền, trên hộp riêng của bạn:

- [Tính toàn vẹn Backtest](./features/backtest-integrity.md) · [Định kích thước vị trí](./features/position-sizing.md)
- [Sức khỏe chiến lược](./features/strategy-health.md) · [Phòng thí nghiệm Regime](./features/regime-lab.md)
- [Thực thi TCA](./features/execution-tca.md) · [Nhật ký giao dịch](./features/trading-journal.md)
- [Studi Đại lý](./features/agent-studio.md) · [Định vị Contrarian](./features/contrarian-positioning.md)

## Chạy nơi bạn làm

Bắt đầu trên máy tính xách tay của bạn với `docker compose up`, tốt nghiệp thành VPS rẻ hoặc máy chủ nhà khi bạn
sẵn sàng, và kiểm tra các bot của bạn từ điện thoại của bạn — cMind là một công ty cài đặt được, điều khiển di động đầu tiên
[PWA](./features/pwa.md). → [Chạy nó cục bộ](./deployment/local.md)

Muốn máy khách AI của bạn để điều khiển nó? Có một máy chủ [MCP](./features/mcp.md) tích hợp.

## Giúp làm cho nó tốt hơn

cMind là mã nguồn mở và được cấp phép MIT — lộ trình được định hình bởi cộng đồng:

- Lập các vấn đề và yêu cầu tính năng, và bình chọn những gì quan trọng.
- Thêm các template cBot, các bộ điều hợp nhà cung cấp AI hoặc các bản dịch UI.
- Gửi PRs — ba lớp thử nghiệm (unit + tích hợp + E2E) và DDD nghiêm ngặt giữ thanh cao, và
  [Hướng dẫn Đóng góp](./contributing.md) hướng dẫn bạn qua nó.

Sẵn sàng? → [Đọc giới thiệu](./intro.md) sau đó [chạy nó cục bộ](./deployment/local.md).
