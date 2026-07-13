---
title: 0002 — Trạng thái Thực thể là TPH; Một Quá trình Chuyển Thay thế Thực thể
description: Tại sao id của thực thể thay đổi khi nó di chuyển qua vòng đời của nó, và tại sao id vùng chứa là khóa ổn định.
---

# 0002 — Trạng thái Thực thể là TPH; Một Quá trình Chuyển Thay thế Thực thể

## Ngữ cảnh

Một thực thể run/backtest di chuyển qua các trạng thái (chờ đợi → lên lịch → bắt đầu → chạy → cuối cùng).
Chúng tôi mô phỏng trạng thái với EF Core **Table-Per-Hierarchy (TPH)**: mỗi trạng thái là một loại phụ
(`StartingRunInstance`, `RunningRunInstance`, …). Cột TPH phân loại của EF **không thể thay đổi** trên
một hàng hiện tại.

## Quyết định

Một quá trình chuyển trạng thái **thay thế thực thể** bằng một thực thể loại phụ mới thay vì đột biến một trạng thái
trường. Vì hàng được thay thế, **instance id thay đổi** qua bắt đầu → chạy → cuối cùng.
**id vùng chứa ổn định** và được mang qua các quá trình chuyển; đại lý nút HTTP được khóa bằng
id vùng chứa để trạng thái/báo cáo/dừng/nhật ký.

## Hậu quả

- Mỗi trạng thái là một loại riêng biệt chỉ với các trường và phương thức hợp lệ trong trạng thái đó — bất hợp pháp
  các quá trình chuyển và truy cập trường vô nghĩa là lỗi biên soạn, không phải kiểm tra thời gian chạy.
- Người gọi phải **không** cache instance id qua một quá trình chuyển; sử dụng id vùng chứa làm ổn định
  xử lý cho bất cứ điều gì kéo dài các trạng thái.
- Logic quá trình chuyển sống trong `InstanceTransitions`; sự thay đổi id là cố ý, không phải một lỗi.
