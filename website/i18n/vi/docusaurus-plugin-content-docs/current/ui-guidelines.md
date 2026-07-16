---
description: "Quy tắc bắt buộc cho mọi phần giao diện người dùng mới hoặc thay đổi trong ứng dụng này (trang Blazor, hộp thoại, thành phần). Đây là nguồn sự thật được tham chiếu bởi `CLAUDE.md`..."
---

# Hướng dẫn thiết kế giao diện người dùng — BẮT BUỘC

Quy tắc bắt buộc cho **mọi** phần giao diện người dùng mới hoặc thay đổi trong ứng dụng này (trang Blazor, hộp thoại, thành phần).
Đây là nguồn sự thật được tham chiếu bởi `CLAUDE.md`. Nếu một quy tắc chặn bạn, hãy dừng lại và hỏi — đừng
gửi giao diện người dùng vi phạm nó. Dựa trên `plans/ui-overhaul.md`.

## 1. Mobile-first, luôn luôn

- **Thiết kế cho điện thoại 360–430px trước tiên**, sau đó nâng cao với các truy vấn phương tiện `min-width` / thuộc tính điểm dừng MudBlazor.
  Không bao giờ thiết kế desktop-first với ghi đè `max-width`.
- **Không cuộn ngang ở bất kỳ chiều rộng nào từ 320–1920px.** Nếu nội dung rộng hơn khung nhìn, đó là một lỗi.
- Mục tiêu chạm ≥ **44px** (`var(--app-touch-target)`). Các trường nhập văn bản ≥ 16px font (ngăn chặn iOS zoom-on-focus).
- Tôn trọng các notch: sử dụng `env(safe-area-inset-*)`; khung nhìn đã đặt `viewport-fit=cover`.
- Tuân thủ `prefers-reduced-motion` — không có thông tin thiết yếu được truyền đạt chỉ bằng hoạt ảnh.

## 2. Design tokens — không có giá trị được mã hóa cứng

- Tất cả màu sắc/bán kính/khoảng cách đến từ **design tokens**: chủ đề MudBlazor (`Web/Components/Theme.cs`) +
  các thuộc tính tùy chỉnh CSS phát ra bởi `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Không bao giờ mã hóa cứng một màu hex, bán kính hoặc chuỗi thương hiệu trong thành phần hoặc quy tắc CSS.** Đọc một token.
  Các token chảy từ white-label `BrandingOptions`, vì vậy bảng màu của một nhà bán lại phải đến giao diện người dùng của bạn miễn phí.
- Giá trị ảnh hưởng đến thương hiệu mới → thêm một token + trường branding; đừng nhúng nó.

## 3. Bố cục đáp ứng & dữ liệu

- **Bảng thu gọn thành các thẻ trên điện thoại.** Mỗi `MudTable` đặt `Breakpoint="Breakpoint.Sm"` và mỗi
  `MudTd` có một `DataLabel`. Không có bảng rộng thô trên mobile. (Mẫu: `Components/Pages/Nodes.razor`.)
- Lưới: `MudItem xs="12" sm="6" md="4"` — toàn chiều rộng trên điện thoại, nhiều cột hướng lên.
- Biểu mẫu cột đơn trên mobile; các mục tiêu chạm lớn; `inputmode`/`autocomplete` trên các đầu vào; inputmode số/thập phân
  cho tiền/phần trăm.
- **Các điều khiển thích hợp cho đầu vào có cấu trúc — không bao giờ là một hộp văn bản thô để nhập số hoặc danh sách.** Thu thập số,
  tiền, phần trăm, ngày, enums và bất kỳ dữ liệu đa giá trị nào với điều khiển phù hợp (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, danh sách hàng có thể chỉnh sửa với các trường được nhập, hoặc bảng), mỗi trường
  được xác thực riêng lẻ. Một `MudTextField` văn bản tự do duy nhất mà người dùng phải nhập một blob được phân tách bằng dấu phẩy/khoảng trắng/dòng mới vào — mà bạn sau đó phân tích cú pháp — là **cấm**: nó dễ xảy ra lỗi, không được xác thực, và có hại trên điện thoại. **Không ai muốn nhập một blob.** Đầu vào đa giá trị là danh sách có thể chỉnh sửa của các hàng được nhập (thêm /
  xóa), hoặc được tải từ dữ liệu miền hiện có (ví dụ: chạy kiểm tra trực tiếp từ một bài kiểm tra hoàn thành
  thay vì nhập lại các số của nó). `MudTextField` đơn thuần chỉ dành cho văn bản tự do thực sự — tên, ghi chú,
  tìm kiếm, mô tả.
- Cung cấp **tải, trống và lỗi** trạng thái trên mọi danh sách/chi tiết — kích thước cho mobile.
- **Điều hướng dưới cùng** mobile (`Components/Layout/BottomNav.razor`) là điều hướng điện thoại chính; ngăn kéo nhóm là menu đầy đủ. Thêm các điểm đến lưu lượng cao ở đó; giữ nó ≤5 mục.

## 4. Hộp thoại (tạo/chỉnh sửa)

- Tất cả các hành động thêm/tạo/chỉnh sửa/mới sử dụng một **hộp thoại MudBlazor** (`IDialogService.ShowAsync<TDialog>`), không bao giờ
  một biểu mẫu trang nội tuyến. Các hộp thoại sống trong `Web/Components/Dialogs/`, để lộ `[Parameter]`s, trả về một `public sealed record …Result(...)` lồng nhau.
- Các hành động hàng danh sách (bắt đầu/dừng/xóa) vẫn nội tuyến như các nút biểu tượng.
- Trên điện thoại, các hộp thoại sẽ **toàn màn hình / toàn chiều rộng** và nhạy cảm với bàn phím.

## 5. Trợ giúp nội tuyến — mỗi điều khiển

- Mỗi tùy chọn, lựa chọn, công tắc hoặc hành động không rõ ràng được lấy một **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — di chuột trên desktop, **nhấn trên mobile**. Lấy văn bản từ `docs/` để
  hướng dẫn luôn đồng bộ với hành vi; cập nhật cả hai trong cùng một lần commit.

## 6. White-label

- Tên sản phẩm, logo, mô tả, hỗ trợ/công ty, màu sắc, favicon đều đến từ `BrandingOptions`.
  Tham chiếu chúng (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), không bao giờ là "cMind" hoặc một
  màu thương hiệu. Tệp kê khai PWA, biểu tượng, theme-color và hình ảnh chính đăng nhập đều được dán nhãn thương hiệu.

## 7. PWA

- Ứng dụng có thể cài đặt được. Giữ cho điểm cuối tệp kê khai (`/manifest.webmanifest`) được dán nhãn thương hiệu, biểu tượng hiện diện
  (192/512/maskable + apple-touch), trình làm việc dịch vụ chỉ là vỏ ứng dụng (không bao giờ chạm vào Blazor
  circuit/`_framework`/hubs), và trang ngoại tuyến hoạt động. Tuyến tĩnh mới → giữ `scope` của tệp kê khai.
- Blazor Server cần một mạch SignalR trực tiếp → **có thể cài đặt + vỏ ứng dụng**, không phải ngoại tuyến đầy đủ. Đừng
  hứa tương tác ngoại tuyến.

## 8. Khả năng tiếp cận

- Nhãn trên các đầu vào, `aria-*` trên các điều khiển tùy chỉnh, tiêu điểm hiển thị, thứ tự tiêu điểm hợp lý. Vì chủ đề là
  có thể dán nhãn thương hiệu, hãy xác minh **độ tương phản** với chủ đề hoạt động, không phải bảng màu cố định.

## 9. E2E — không UI nào được gửi không kiểm tra (chặn)

Mỗi thay đổi giao diện người dùng được gửi Playwright E2E trong `tests/E2ETests`, được điều khiển như một người dùng thực, **trên emulation thiết bị mobile**
cộng với desktop:

- Tuyến mới → thêm nó vào `PageSmokeTests` **và** `MobileLayoutTests` (hiển thị, điều hướng dưới cùng, không có UI lỗi).
- Chuyển đổi bảng/trang → thêm tuyến của nó vào bộ **không tràn** di động.
- Luồng mới → một hành trình di động thực tế (vòng tạo/chỉnh sửa/lưu) **và** một đường dẫn không vui vẻ
  (đầu vào không hợp lệ, danh sách trống, quyền truy cập được từ chối theo vai trò).
- Mẹo trợ giúp mới → khẳng định nó mở khi nhấn (`HelpTipTests` pattern).
- Sử dụng `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (emulation thiết bị).
- `dotnet test` xanh trước "xong". WebKit được mô phỏng ≠ Safari di động — gating thiết bị thực tế là một bước phát hành riêng.

## 10. Định nghĩa về hoàn thành (UI)

- [ ] Mobile-first; không cuộn ngang 320–1920px; các mục tiêu chạm ≥44px.
- [ ] Chỉ design tokens — không có màu được mã hóa cứng/radii/chuỗi thương hiệu.
- [ ] Bảng → thẻ trên điện thoại (`DataLabel` + `Breakpoint.Sm`); trạng thái tải/trống/lỗi hiện diện.
- [ ] Đầu vào có cấu trúc sử dụng các điều khiển được xác thực thích hợp (số/ngày/chọn/danh sách hàng có thể chỉnh sửa) — không hộp văn bản thô mà người dùng nhập
  một blob giá trị được phân tách vào.
- [ ] Tạo/chỉnh sửa qua hộp thoại; toàn màn hình trên mobile.
- [ ] Mỗi điều khiển có một `HelpTip` được lấy từ tài liệu.
- [ ] White-label + PWA được tôn trọng.
- [ ] E2E di động + desktop được thêm (khói, không tràn, hành trình, đường dẫn không vui vẻ); `dotnet test` xanh.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` sạch sẽ trên các tệp được chạm vào.
