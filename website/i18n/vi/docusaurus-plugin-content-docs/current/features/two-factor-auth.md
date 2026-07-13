---
description: "Xác thực hai yếu tố TOTP tùy chọn với đăng ký ứng dụng xác thực, mã sao lưu dùng một lần, và công tắc white-label để làm cho nó bắt buộc đối với tất cả người dùng."
---

# Xác thực hai yếu tố (2FA)

Tài khoản có thể được bảo vệ bằng **mật khẩu dùng một lần dựa trên thời gian (TOTP)** xác thực hai yếu tố trên đỉnh của mật khẩu. Nó **chọn tham gia** từ hồ sơ của người dùng theo mặc định, và triển khai white-label có thể làm cho nó **bắt buộc** cho tất cả mọi người. Bất kỳ ứng dụng xác thực RFC 6238 nào hoạt động — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — vì triển khai là tiêu chuẩn (SHA-1, 6 chữ số, bước 30 giây); không có thành phần máy chủ độc quyền liên quan.

## Nó hoạt động như thế nào

- **Miền.** MFA sống trên tổng hợp `AppUser` (Bối cảnh truy cập). Một người dùng được đăng ký thông qua các phương thức tiết lộ ý định — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — vì vậy các bất biến (một bí mật phải được xác nhận trước khi nó kích hoạt; một mã sao lưu được sử dụng một lần) được thực thi ở một nơi.
- **TOTP.** Tạo và xác minh nằm phía sau giao diện Core `ITotpAuthenticator`, được triển khai trong Infrastructure với thư viện **Otp.NET**. Xác minh chịu đựng ±1 bước thời gian của độ chệch đồng hồ.
- **Bí mật khi nghỉ.** Bí mật xác thực được lưu trữ **được mã hóa** thông qua `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — không bao giờ dưới dạng rõ ràng.
- **Mã sao lưu.** Mười mã khôi phục dùng một lần được cấp khi đăng ký, hiển thị **một lần**, và được lưu trữ chỉ dưới dạng hashes SHA-256 (`MfaBackupCodes`). Mỗi hoạt động chính xác một lần; một mã được sử dụng bị từ chối sau đó.

## Bật nó (hồ sơ)

Trên trang **Tài khoản** (`/account`) phần *Xác thực hai yếu tố* hiển thị trạng thái hiện tại:

1. **Bật xác thực hai yếu tố** mở một hộp thoại MudBlazor với **mã QR** (được hiển thị phía máy chủ dưới dạng SVG qua `Net.Codecrete.QrCodeGenerator`) cộng với khóa cài đặt thủ công.
2. Quét nó, nhập mã 6 chữ số để xác nhận — điều này xác minh bí mật đang chờ trước khi kích hoạt.
3. Hộp thoại sau đó hiển thị **mã sao lưu**; lưu chúng. 2FA hiện đang bật.

Cùng một phần cho phép người dùng được đăng ký **tạo lại mã sao lưu** hoặc **tắt** 2FA — cả hai đều yêu cầu mật khẩu tài khoản để xác nhận.

## Đăng nhập với 2FA

Đăng nhập là luồng **hai bước** sau khi 2FA được bật:

1. **Bước mật khẩu** (`POST /api/auth/login`). Khi thành công, cookie xác thực **không** được cấp chưa; thay vào đó, một cookie *đang chờ* được mã hóa có thời gian ngắn (5 phút) được đặt và người dùng được gửi đến `/login/2fa`.
2. **Bước thử thách** (`POST /api/auth/login/verify-2fa`). Người dùng nhập mã TOTP **hoặc** bất kỳ mã sao lưu chưa sử dụng nào. Khi thành công, cookie đang chờ bị loại bỏ và cookie xác thực thực được cấp.

Nỗ lực xác thực nhân tố thứ hai không thành công đếm đến **lockout** tài khoản hiện có (`AuthLockout`), và các điểm cuối xác thực bị giới hạn tốc độ.

## 2FA bắt buộc cho triển khai white-label

Nhà bán lại được quy định có thể yêu cầu 2FA cho **mỗi** tài khoản:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Khi `RequireMfa` bật và một người dùng không có 2FA đăng nhập, bước mật khẩu báo cáo `mfaSetupRequired` và `MfaEnforcementMiddleware` chuyển hướng điều hướng trang của họ đến `/account` cho đến khi họ hoàn thành đăng ký. Nó mặc định thành `false`, vì vậy một triển khai không được cấu hình giữ 2FA tùy chọn. Xem [White-label](white-label.md).

## Điểm cuối

| Phương thức & tuyến | Mục đích |
| --- | --- |
| `POST /api/auth/login` | Bước mật khẩu; trả về `mfaRequired` (thử thách) hoặc đăng nhập |
| `POST /api/auth/login/verify-2fa` | Bước nhân tố thứ hai (TOTP hoặc mã sao lưu) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, đang chờ, số lượng mã sao lưu còn lại |
| `POST /api/auth/mfa/setup` | Bắt đầu đăng ký — trả về bí mật, URI `otpauth://`, QR SVG |
| `POST /api/auth/mfa/confirm` | Xác nhận mã, kích hoạt, trả về mã sao lưu |
| `POST /api/auth/mfa/disable` | Tắt (được xác nhận mật khẩu) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Phát hành một bộ mới (được xác nhận mật khẩu) |

## Các bài kiểm tra

- **Đơn vị** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (vectơ RFC 6238), `AppUserMfaTests.cs` (bất biến đăng ký/chuyển tiếp/sử dụng một lần), `MfaBackupCodesTests.cs`.
- **Tích hợp** — `IntegrationTests/MfaPersistenceTests.cs` (đăng ký → xác nhận → tiêu thụ, xóa tầng) và `MfaFlowTests.cs` (đăng nhập HTTP hai bước đầy đủ với TOTP + mã sao lưu, và cổng đăng ký bắt buộc).
- **E2E** — `E2ETests/MfaFlowTests.cs`: bật từ hồ sơ (QR + xác nhận + mã sao lưu) và hoàn thành đăng nhập bị thử thách, trên cảnh quan desktop và di động.
