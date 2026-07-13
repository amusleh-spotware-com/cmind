---
title: 0004 — CBotBuilder Chạy Trên Web Host Trong Một Vùng Chứa Sandbox
description: Tại sao các bản dựng cBot không đáng tin cậy xảy ra trên web host bên trong một vùng chứa SDK có thể loại bỏ thay vì trên một nút.
---

# 0004 — `CBotBuilder` Chạy Trên Web Host Trong Một Vùng Chứa Sandbox

## Ngữ cảnh

Xây dựng cBot của một người dùng có nghĩa là chạy **MSBuild không đáng tin cậy** — mã tùy ý tại thời gian xây dựng (mục tiêu,
trình tạo nguồn, các tập lệnh khôi phục). Nó cần ổ cắm Docker để quay lên một vùng chứa SDK. Các nút
chạy các vùng chứa giao dịch và không nên giữ các đặc quyền xây dựng.

## Quyết định

`CBotBuilder` chạy **trên web host** (mà đã có ổ cắm Docker), bên trong một **SDK có thể loại bỏ
vùng chứa** với:

- một thư mục `/work` được gắn kết (chỉ các đầu vào/đầu ra xây dựng, không phải hệ thống tệp máy chủ);
- một âm lượng `app-nuget-cache` được chia sẻ cho hiệu suất khôi phục;
- không truy cập mạng máy chủ ngoài những gì khôi phục cần.

Vì vậy MSBuild không đáng tin cậy không thể tiếp cận hệ thống tệp hoặc mạng máy chủ. Chạy/backtest các vùng chứa, bởi
ngược lại, chạy trên các nút được chọn bởi `NodeScheduler`.

## Hậu quả

- Đặc quyền xây dựng (ổ cắm Docker) được giới hạn ở web host; các nút chỉ chạy hình ảnh giao dịch được phép.
- Mỗi bản dựng được cô lập trong một vùng chứa có thể loại bỏ — một bản dựng độc hại không thể duy trì hoặc thoát.
- Web host phải có ổ cắm Docker có sẵn; đây là một yêu cầu triển khai, không phải là tùy chọn.
