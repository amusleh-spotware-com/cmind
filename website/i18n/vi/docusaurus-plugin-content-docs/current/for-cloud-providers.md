---
slug: /for-cloud-providers
title: cMind cho các nhà cung cấp cloud & VPS
description: Tại sao một nhà cung cấp cloud hoặc VPS nên cung cấp lưu trữ cMind được quản lý — một sản phẩm sẵn sàng, khác biệt cho các trader algo, broker và công ty prop-firm, với những cách rõ ràng để kiếm tiền tính toán, white-label bán lại và AI được quản lý.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# cMind cho các nhà cung cấp cloud & VPS 🖥️

Bạn đã thuê tính toán. cMind là một sản phẩm sẵn sàng, mã nguồn mở mà bạn có thể bao bọc tính toán đó
xung quanh: **cung cấp lưu trữ cMind được quản lý** và đạt được khối lượng công việc cao, dính, tiêu thụ công suất —
những trader algo, broker, công ty prop-firm và những cộng đồng giao dịch muốn nền tảng chạy
mà không trở thành nhóm ops của họ.

:::tip TL;DR
Chạy tầng không trạng thái + Postgres + một fleet nút; trao cho khách hàng một URL được xây dựng. Kiếm tiền từ
đăng ký, tính toán, white-label và AI. → [Triển khai đến cloud](./deployment/cloud.md)
:::

## Tại sao cung cấp cMind được quản lý

- **Không có chi phí xây dựng.** Nó là mã nguồn mở, được cấp phép MIT và đã được ghi chép, kiểm tra và chứa sẵn.
  Bạn đóng gói và hoạt động nó — bạn không xây dựng nó.
- **Một sản phẩm khác biệt cho một thị trường không rõ ràng có lợi.** Giao dịch Algo tiêu thụ công suất: backtests và
  các nút trực tiếp đốt CPU, đó là *billable usage* mà bạn đã bán.
- **Những khách hàng dính.** Những trader xây dựng và chạy chiến lược bên trong nền tảng không churn thường xuyên.
- **Biến một cảnh báo thành một upsell.** cMind được tự host theo thiết kế — cho các khách hàng "không muốn
  được nhóm ops," *you* là câu trả lời.

## Ai mua cMind được quản lý từ bạn

- **Những quant riêng lẻ & trader** muốn nó được host. → [Cho các trader](./for-traders.md)
- **Những broker cTrader** chạy một white-label cho các khách hàng của họ. → [Cho các broker](./for-brokers.md)
- **Những công ty prop-firm & kinh doanh giao dịch sao chép** cần cơ sở hạ tầng được xây dựng, có thể kiểm toán.

## Những "cMind được quản lý" có nghĩa là chạy

Bạn vận hành ba tầng; khách hàng nhận được một URL web được xây dựng:

| Tầng | Nó là gì | Nơi nó chạy |
|---|---|---|
| Không trạng thái (Web + MCP) | Ứng dụng + API + máy chủ MCP | Bất kỳ nền tảng vùng chứa, tự động chia tỷ lệ |
| Cơ sở dữ liệu | PostgreSQL | Postgres được quản lý (RDS / Flexible Server / của riêng bạn) |
| Nút fleet | Xây dựng & chạy các vùng chứa cTrader | **VMs hoặc Kubernetes — cần Docker được ưu tiên** |

:::warning Một điều phạm vi lên trước
Các đại lý nút xây dựng và chạy các vùng chứa cTrader, vì vậy họ cần **Docker được ưu tiên**. Điều đó loại trừ
container runtimes không có máy chủ (Azure Container Apps, AWS Fargate) *cho các đại lý* — chạy những cái trên
[Kubernetes](./deployment/kubernetes.md), một VM hoặc EC2. Tầng không trạng thái chạy ở bất kỳ nơi nào.
:::

Hướng dẫn triển khai thực tế, copy-paste làm cho điều này cụ thể: [tổng quan cloud](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## Cách bạn kiếm tiền từ nó

- **Đăng ký lưu trữ được quản lý.** Các kế hoạch Starter / Team / Business hàng tháng được xác định theo fleet nút và
  tương tranh backtest.
- **Metering & tính toán sử dụng.** Hóa đơn backtest-hours, live-node-hours và lưu trữ — tự nhiên được đo bằng
  fleet vùng chứa bạn đã chạy.
- **White-label reseller tầng.** Tính phí cao hơn cho một rebrand đầy đủ (logo, màu sắc, PWA,
  `ShowSiteLink=false`) và để bật các khả năng cao cấp qua
  [chuyển đổi tính năng](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **AI được quản lý.** Bó một khóa nhà cung cấp AI mặc định để mọi người dùng khách hàng nhận được AI không có thiết lập, và
  đánh dấu lên cách sử dụng — hoặc cung cấp bring-your-own-key. → [Tính năng AI](./features/ai.md)
- **Prop-firm & doanh thu chia sẻ giao dịch sao chép.** Những công ty lưu trữ chạy những thách thức và phí hiệu suất và
  lấy một nền tảng cắt. → [Prop-firm](./features/prop-firm.md) ·
  [Phí hiệu suất](./features/copy-performance-fees.md) ·
  [Thị trường nhà cung cấp](./features/copy-provider-marketplace.md)
- **Thiết lập, onboarding & SLA.** Đính kèm các dịch vụ chuyên nghiệp và hỗ trợ cao cấp.

## Các mẫu đa thuê bao

- **Triển khai-per-tenant (được khuyến nghị).** Một phiên bản được xây dựng cho mỗi khách hàng — cách ly mạnh,
  thương hiệu và cơ sở dữ liệu mỗi người thuê, một mã thông báo nút khác biệt cho mỗi người thuê. Thương hiệu được đọc từ
  `IOptionsMonitor`, vì vậy mỗi thực thể mang danh tính riêng của nó.
  → [Thương hiệu đa thuê bao](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Khám phá nút](./operations/node-discovery.md)
- **Plane kiểm soát được chia sẻ (nâng cao).** Lái nhiều thực thể từ lớp cung cấp của riêng bạn, trồng
  thương hiệu và tính năng cho mỗi người thuê theo chương trình.

## Metering sử dụng cho hóa đơn

Một chủ sở hữu/admin-only **`GET /api/usage`** endpoint trả về một tóm tắt chỉ đọc mà nhà cung cấp có thể bỏ phiếu và
hóa đơn trên — không có bất kỳ miền mới hoặc tính bền vững, nó dự báo trạng thái hiện tại:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
```
