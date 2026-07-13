---
title: 0006 — Lưu Trữ Sao Chép Được Điều Phối Bởi Một Khoá DB Nguyên Tử
description: Tại sao các hồ sơ sao chép được yêu cầu qua một khoá Postgres nguyên tử thay vì một bộ điều phối chuyên dụng, và cách điều đó ngăn chặn giao dịch kép.
---

# 0006 — Lưu Trữ Sao Chép Được Điều Phối Bởi Một Khoá DB Nguyên Tử

## Ngữ cảnh

Một hồ sơ sao chép đang chạy phải được lưu trữ bởi **chính xác một** nút — hai máy chủ trên cùng một hồ sơ có nghĩa
mỗi giao dịch nguồn được phản chiếu hai lần (tiền thực bị mất). Các nút đến và đi (cân bằng, sự cố, cuộn
cập nhật), và chúng tôi không muốn một dịch vụ bộ điều phối riêng biệt để chạy và giữ sống.

## Quyết định

Mỗi `CopyEngineSupervisor` yêu cầu hồ sơ với một **atomic DB lease** trên bảng `CopyProfiles`:

- **Yêu cầu** — một `ExecuteUpdate` nguyên tử (hoặc `FOR UPDATE SKIP LOCKED` khi cấp phép mỗi nút) lấy
  các hồ sơ chưa được gán *hoặc* mà khoá đã hết hạn. Tính nguyên tử có nghĩa là hai quản trị viên đua
  không bao giờ cả hai yêu cầu cùng một hàng.
- **Gia hạn** — một nút trực tiếp làm mới khoá của nó mỗi chu kỳ, vì vậy nó giữ yêu cầu của nó.
- **Khiếu nại** — khoá của một nút bị sập hết hạn, và một người sống sót nhặt hồ sơ lên trên chu kỳ tiếp theo của nó
  (tự chữa). Khi tắt một cách duyên lối, nút **giải phóng** khoá ngay lập tức để failover nhanh.
- **Watchdog** — một máy chủ mà tác vụ của nó đã thoát trong khi hồ sơ vẫn của chúng tôi được khởi động lại.
- Hòa giải được rung chuông để tránh một đàn voi sấm sét của `UPDATE`s tại quy mô.

## Hậu quả

- Không có bộ điều phối độc lập để triển khai hoặc giữ khỏe — Postgres là nguồn sự thật duy nhất.
- Giao dịch kép được ngăn chặn bằng tính nguyên tử cấp hàng, không phải bằng khóa cấp ứng dụng.
- Độ trễ failover được ràng buộc bởi TTL khoá (trừ phương pháp tắt duyên lối nhanh).
- Đây là con đường tiền; nó được bảo vệ bởi bộ kiểm tra căng thẳng xác định (DST) — không bao giờ làm yếu một kịch bản DST
  để làm cho nó vượt qua.
