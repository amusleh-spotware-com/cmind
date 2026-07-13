---
title: 0005 — Máy Khách AI Sử Dụng HTTP Thô, Không Phải Anthropic SDK
description: Tại sao IAiClient gọi Anthropic API qua một HttpClient được gõ thay vì SDK chính thức, và tại sao AI hoàn toàn được gated trên một khóa.
---

# 0005 — Máy Khách AI Sử Dụng HTTP Thô, Không Phải Anthropic SDK

## Ngữ cảnh

Mọi tính năng AI (tạo chiến lược, tự sửa chữa, lệnh bảo vệ rủi ro, autopsy) gọi Anthropic
API. Một phụ thuộc SDK thêm một bề mặt bắc cầu mà chúng tôi không kiểm soát, ghép chu kỳ phát hành của chúng tôi với
của họ, và ẩn chính xác hợp đồng dây mà chúng tôi cần lý do về độ phục hồi và chi phí.

## Quyết định

`IAiClient` gọi Anthropic qua **HTTP thô** thông qua một `HttpClient` được gõ — cố ý **không**
SDK. `AiFeatureService` là đơn vị điều phối được chia sẻ bởi các điểm cuối Web, MCP `AiTools` và
`AiRiskGuard`. Toàn bộ bề mặt được **gated on `AppOptions.Ai.ApiKey`**: không có khóa, mỗi tính năng
trả về `AiResult.Fail` và ứng dụng chạy không thay đổi.

## Hậu quả

- Không cần khóa cho xây dựng, kiểm tra hoặc E2E — CI và dev cục bộ chạy ứng dụng đầy đủ mà không AI.
- Chúng tôi sở hữu yêu cầu/hình dạng phản hồi, chính sách thử lại/hết thời gian chờ và kế toán token một cách rõ ràng.
- Các tính năng Anthropic mới phải được kết nối bằng tay; chúng tôi giao dịch sự tiện lợi cho kiểm soát và một cái nhỏ hơn
  bề mặt phụ thuộc. Xem tham chiếu `claude-api` cho ID mô hình và tham số hiện tại.
