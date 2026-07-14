---
description: "Mọi thời gian hiển thị đều theo múi giờ của riêng bạn — được phát hiện từ trình duyệt trong lần truy cập đầu và có thể thay đổi trong Cài đặt. Lưu trữ và API vẫn theo UTC."
---

# Múi giờ

Mọi thời gian ứng dụng hiển thị đều được kết xuất theo múi giờ của riêng bạn, không phải máy chủ. Lựa chọn của bạn được lưu vào hồ sơ và đi theo bạn trên các thiết bị.

Trong lần truy cập đầu, ứng dụng tự động áp dụng múi giờ của trình duyệt. Bạn có thể đổi bất cứ lúc nào tại Cài đặt → Múi giờ; mặc định triển khai là tùy chọn white-label App:Branding:DefaultTimeZone (mặc định UTC). Thời gian luôn được lưu và trả về từ API theo UTC — chỉ phần hiển thị được chuyển đổi.

- Thứ tự phân giải: múi giờ hồ sơ, rồi cookie, rồi mặc định triển khai, rồi UTC.
- Việc phát hiện chạy một lần và không bao giờ ghi đè múi giờ bạn đã chọn.
- Định dạng theo ngôn ngữ của bạn; nhãn tương đối như «2 phút trước» không bị ảnh hưởng.
