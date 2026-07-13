---
description: "Đăng ký người dùng tự phục vụ được bảo vệ, được gating white-label — trang đăng ký trên ứng dụng và API cấp phát máy chủ-to-máy chủ, với các thuộc tính người dùng có thể cấu hình, gating phê duyệt quản trị viên hoặc xác minh email, và lính canh chống lạm dụng. Tắt theo mặc định."
---

# Đăng ký người dùng

Theo mặc định, **chủ sở hữu/quản trị viên thêm người dùng thủ công** (trang Người dùng → *Người dùng mới*). Đối với triển khai white-label cần onboard người dùng quy mô lớn — hoặc tích hợp ứng dụng với dịch vụ khác — cMind cũng gửi một **đường dẫn đăng ký tự phục vụ bảo vệ**. Nó **bị tắt theo mặc định**: triển khai stock không thay đổi và cả trang và API đều trả về 404 cho đến khi triển khai chọn tham gia.

Có hai điểm nhập chia sẻ một luồng miền:

1. **Trang trên ứng dụng** (`/register`) — trang đăng ký được đặt tên thương hiệu, ưu tiên di động trong cùng một shell như `/login`.
2. **API cấp phát** (`POST /api/provision`) — điểm cuối máy chủ-to-máy chủ để dịch vụ tích hợp tạo tài khoản, được xác thực bằng bí mật cấp phát cho mỗi triển khai.

## Những gì được ghi lại — giảm thiểu dữ liệu

cMind là giao dịch **tooling**: nó xây dựng/chạy/kiểm tra lại cBots và phản chiếu giao dịch trên thông tin xác thực Open API cTrader *của chính họ* của mỗi người dùng. Nó **không mở tài khoản giao dịch hoặc tiền được lưu ký của khách hàng**, vì vậy xác minh danh tính KYC/AML là **trách nhiệm của nhà môi giới**, không phải của nền tảng này. Biểu mẫu đăng ký do đó **chỉ ghi một email theo mặc định** — mức tối thiểu cần thiết để cung cấp dịch vụ (GDPR Art. 5(1)(c) giảm thiểu dữ liệu; cơ sở pháp lý = hợp đồng). cMind cố ý gửi **không** trường ID quốc gia / ngày sinh / địa chỉ.

Mọi thuộc tính khác **chọn tham gia cho mỗi triển khai** thông qua `App:Registration:Attributes`, mỗi cái độc lập `Off` / `Optional` / `Required`:

| Thuộc tính | Ghi chú |
|---|---|
| `FullName`, `DisplayName`, `Company` | Văn bản tự do, giới hạn chiều dài. |
| `Country` | ISO 3166-1 alpha-2, được xác thực chống lại một bộ mã cố định. |
| `Phone` | Định dạng E.164 (`+14155552671`). |
| `Locale` | Hình dạng BCP-47 (`en-US`), được chuẩn hóa. |
| `MarketingOptIn` | Riêng biệt, hộp kiểm **unticked** — không bao giờ được gói với sự đồng ý bắt buộc (CAN-SPAM). |
| `AgeConfirmation` | Một hộp kiểm chỉ; **không** ngày sinh được lưu trữ. |

Các thuộc tính sống trong đối tượng giá trị `UserProfile` sở hữu bởi tổng hợp `AppUser`, được xác thực khi xây dựng. **Xóa GDPR** (`AppUser.Anonymize()`) làm sạch hồ sơ và bất kỳ mã thông báo xác minh nào.

**Sự đồng ý.** Khi `RequireTermsAcceptance` bật, người dùng phải chấp nhận các tài liệu pháp lý được xuất bản (Điều khoản, Quyền riêng tư, Công bố Rủi ro). Sự chấp nhận được ghi lại thông qua tổng hợp `ConsentRecord` hiện có — phiên bản được dấu thời gian, với IP xuất phát — cùng một cửa hàng được sử dụng ở nơi khác cho MiFID/ESMA-grade record-keeping.

## Gating Modes

Tài khoản tự đăng ký không thể đăng nhập cho đến khi nó xóa cổng của nó (`App:Registration:Mode`):

- **`AdminApproval`** (mặc định) — tài khoản được xếp hàng; chủ sở hữu/quản trị viên phê duyệt nó trên trang **Người dùng** (phần *Chờ phê duyệt*). Không cần cơ sở hạ tầng mail.
- **`EmailVerification`** — một liên kết xác minh dùng một lần, hết hạn được gửi qua email; tài khoản kích hoạt khi liên kết được mở. Yêu cầu vận chuyển email (`App:Email`). **Nếu không có vận chuyển nào được cấu hình, chế độ này sẽ tự động giảm xuống `AdminApproval`** khi khởi động, vì vậy bật đăng ký không bao giờ âm thầm phá vỡ.
- **`Open`** — tài khoản hoạt động ngay lập tức (chỉ được tin tưởng/phát triển).

Người dùng tự đăng ký luôn được tạo là **`User`** (hoặc `Viewer` nếu được cấu hình) — miền **hard-refuses** để bán chủ sở hữu/Quản trị viên thông qua tự đăng ký.

## Bảo mật & chống lạm dụng

- **Chống liệt kê.** Email trùng lặp mang lại **cùng một** `202 Accepted` trung lập như đăng ký mới và không tạo gì — ứng dụng không bao giờ tiết lộ liệu một địa chỉ đã có tài khoản hay chưa.
- **Giới hạn tốc độ.** Các điểm cuối công cộng bị điều chỉnh mỗi IP (khó hơn so với giới hạn auth).
- **Chính sách mật khẩu.** Chiều dài tối thiểu được thực thi; mật khẩu được hashed (Argon2 qua `IPasswordHasher`); mã thông báo xác minh được lưu trữ chỉ dưới dạng hashes SHA-256 và được sử dụng một lần + hết hạn.
- **Vệ sinh email.** Danh sách cho phép tùy chọn các miền email và khối danh sách các nhà cung cấp có thể loại bỏ.
- **CAPTCHA (tùy chọn).** reCAPTCHA / hCaptcha / Turnstile thông qua hợp đồng xác minh được chia sẻ của họ.
- **Cổng đăng nhập.** Tài khoản đang chờ xử lý bị từ chối khi đăng nhập với phản hồi trung lập.

## API Provisioning (tích hợp)

Với `App:Registration:Api:Enabled` và một `Secret` được đặt, dịch vụ khác có thể tạo người dùng:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Bí mật được so sánh trong thời gian không đổi. Các tài khoản được cấp phát được tạo **hoạt động** (hoặc được mời với `MustChangePassword`) tùy thuộc vào `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Bật nó

Đăng ký yêu cầu **cả** cờ tính năng và công tắc chính:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // hoặc EmailVerification / Open
    "DefaultRole": "User",             // không bao giờ Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // trống = bất kỳ
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Phần `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`) cấu hình vận chuyển được sử dụng bởi chế độ `EmailVerification`; để `Host` chưa được đặt để chạy không có mail (người gửi no-op). Xem [bật tắt tính năng](./feature-toggles.md) và [white-label](./white-label.md) về cách triển khai bật các tính năng và đặt lại tên thương hiệu. Khi đăng ký được bật, trang đăng nhập hiển thị liên kết **Tạo tài khoản**.

## Được kiểm tra

Đơn vị (xác thực hồ sơ, bảo vệ vai trò `SelfRegister`, chuyển đổi kích hoạt, mã thông báo dùng một lần, xóa), tích hợp (bị tắt theo mặc định 404, luồng phê duyệt, hạ cấp xác minh email, chống liệt kê, lính canh lạm dụng, thuộc tính bắt buộc, cấp phát + bí mật xấu), và E2E (đăng nhập tắt theo mặc định không có liên kết đăng ký; trang `/register` hiển thị trạng thái đóng của nó được đặt tên thương hiệu).
