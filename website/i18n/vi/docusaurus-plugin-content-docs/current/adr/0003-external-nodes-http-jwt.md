---
title: 0003 — Các Nút CLI cTrader là HTTP + JWT, không SSH/Shell
description: Tại sao các đại lý nút từ xa chỉ tiếp xúc một API HTTP với JWT có tuổi thọ ngắn và không bao giờ là shell.
---

# 0003 — Các Nút CLI cTrader là HTTP + JWT, không SSH/Shell

## Ngữ cảnh

Các vùng chứa backtest/run thực thi trên các máy chủ từ xa. Cách tiếp cận rõ ràng — SSH in và chạy docker — cấp
ứng dụng chính thực thi mã từ xa tùy ý và thông tin xác thực có tuổi thọ dài trên mỗi nút. Đó là một
bán kính phát nổ lớn cho một hệ thống chạy cBots không đáng tin cậy của người dùng.

## Quyết định

Mỗi máy chủ từ xa chạy một **HTTP agent** độc lập `CtraderCliNode` với **không SSH và không shell**. Các
ứng dụng chính gọi đại lý qua HTTP; mỗi yêu cầu mang một **HS256 JWT** có tuổi thọ ngắn (5 phút,
`iss=app-main` / `aud=app-node`) được ký bằng bí mật của nút đó. Đại lý:

- chỉ chạy các hình ảnh phù hợp với `AllowedImagePrefix` (với ranh giới đường dẫn để `ghcr.io/spotware` không thể
  khớp `ghcr.io/spotware-evil/...`);
- execs docker qua `ArgumentList` — không bao giờ một chuỗi shell;
- là **không trạng thái**, tìm các vùng chứa bằng nhãn `app.instance`;
- tự đăng ký và nhịp tim để `POST /api/nodes/register`; ứng dụng chính upsert `CtraderCliNode`
  **theo tên**, vì vậy một nút sống sót những thay đổi IP.

## Hậu quả

- Một mã thông báo yêu cầu bị rò rỉ hết hạn trong vài phút; không có thông tin xác thực shell đứng để ăn cắp.
- Khả năng của đại lý bị ràng buộc để "chạy một hình ảnh được phép" — nó không thể được biến thành chung
  shell từ xa.
- Danh tính nút dựa trên tên, vì vậy cấp phát lại một nút với IP mới không mồ côi lịch sử của nó.
