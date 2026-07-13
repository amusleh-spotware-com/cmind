---
description: "Ràng buộc cho mỗi phần UI mới hoặc thay đổi trong ứng dụng này (các trang Blazor, hộp thoại, thành phần). Đây là nguồn sự thật được tham chiếu bởi CLAUDE.md. Nếu một quy tắc..."
---

# Hướng Dẫn Thiết Kế UI — MANDATORY

Ràng buộc cho **mỗi** phần UI mới hoặc thay đổi trong ứng dụng này (các trang Blazor, hộp thoại, thành phần).
Đây là nguồn sự thật được tham chiếu bởi `CLAUDE.md`. Nếu một quy tắc chặn bạn, hãy dừng lại và hỏi — không phải
giao UI vi phạm nó. Bắt nguồn từ `plans/ui-overhaul.md`.

## 1. Điều khiển di động-trước, luôn luôn

- **Tác giả cho điện thoại 360–430px trước tiên**, sau đó nâng cao lên với `min-width` truy vấn phương tiện / MudBlazor
  các thuộc tính điểm ngắt. Không bao giờ desktop-đầu tiên với `max-width` ghi đè.
- **Không có cuộn ngang ở bất kỳ chiều rộng 320–1920px.** Nếu nội dung rộng hơn cửa sổ xem, đó là một lỗi.
- Chạm vào mục tiêu ≥ **44px** (`var(--app-touch-target)`). Các đầu vào văn bản ≥ 16px font (dừng iOS zoom-on-focus).
- Tôn trọng các khách: sử dụng `env(safe-area-inset-*)`; cửa sổ xem đã đặt `viewport-fit=cover`.
- Danh dự `prefers-reduced-motion` — không có thông tin cần thiết được truyền đạt chỉ bằng hoạt ảnh.

## 2. Các token thiết kế — không có giá trị mã hóa cứng

- Tất cả màu/bán kính/khoảng cách đến từ **các token thiết kế**: MudBlazor theme (`Web/Components/Theme.cs`) +
  các thuộc tính tùy chỉnh CSS được phát hành bởi `Web/Branding/BrandingCss.cs` (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Không bao giờ mã hóa một màu hex, bán kính hoặc chuỗi thương hiệu trong một thành phần hoặc quy tắc CSS.** Đọc một token.
  Các token chảy từ white-label `BrandingOptions`, vì vậy bảng màu của người bán lại phải tiếp cận UI của bạn miễn phí.
- Giá trị ảnh hưởng đến thương hiệu mới → thêm một token + trường thương hiệu; không phải nó nội tuyến.

## 3. Bố cục & dữ liệu đáp ứng

- **Bảng sụp xuống để thẻ trên điện thoại.** Mỗi `MudTable` đặt `Breakpoint="Breakpoint.Sm"` và mỗi
  `MudTd` có một `DataLabel`. Không có bảng rộng thô trên di động. (Mẫu: `Components/Pages/Nodes.razor`.)
- Grids: `MudItem xs="12" sm="6" md="4"` — toàn chiều rộng trên điện thoại, nhiều cột phía trên.
- Biểu mẫu một cột trên di động; mục tiêu tap lớn; `inputmode`/`autocomplete` trên đầu vào; số/thập phân
  inputmode cho tiền/phần trăm.
- Cung cấp **tải, trống và lỗi** trạng thái trên mỗi danh sách/chi tiết — được định kích thước cho di động.
- **Điều hướng dưới cùng** di động (`Components/Layout/BottomNav.razor`) là điều hướng điện thoại chính; các
  ngăn kéo được nhóm là menu đầy đủ. Thêm các điểm đến giao thông cao; giữ nó ≤5 mục.

## 4. Hộp thoại (tạo/chỉnh sửa)

- Tất cả hành động thêm/tạo/chỉnh sửa/mới sử dụng **hộp thoại MudBlazor** (`IDialogService.ShowAsync<TDialog>`), không bao giờ
  một hình thức trang nội tuyến. Hộp thoại sống trong `Web/Components/Dialogs/`, tiếp xúc `[Parameter]`s, trả về một lồng
  `public sealed record …Result(...)`. Hành động hàng danh sách (bắt đầu/dừng/xóa) ở lại nội tuyến như các nút biểu tượng.
- Trên điện thoại, các hộp thoại sẽ **toàn màn hình / toàn chiều rộng** và nhận thức bàn phím.

## 5. Trợ giúp nội tuyến — mọi kiểm soát

- Mỗi tùy chọn, chọn, chuyển đổi hoặc hành động không rõ ràng nhận được **`<HelpTip Text="…" />`**
  (`Components/HelpTip.razor`) — di chuột trên máy tính để bàn, **tap trên di động**. Nguồn văn bản từ `docs/` vì vậy
  hướng dẫn luôn đồng bộ với hành vi; cập nhật cả hai trong cùng một commit.

## 6. White-label

- Tên sản phẩm, logo, mô tả, hỗ trợ/công ty, màu sắc, favicon đều đến từ `BrandingOptions`.
  Tham chiếu chúng (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), không bao giờ văn bản "cMind" hoặc một
  màu thương hiệu. Tệp kê khai PWA, biểu tượng, theme-color và login hero đều được xây dựng.

## 7. PWA

- Ứng dụng này có thể cài đặt được. Giữ điểm cuối tệp kê khai (`/manifest.webmanifest`) được xây dựng, biểu tượng hiện tại
  (192/512/maskable + apple-touch), công nhân dịch vụ app-shell-only (không bao giờ chạm vào Blazor
  circuit/`_framework`/hubs) và trang ngoại tuyến làm việc. Tuyến tĩnh mới → giữ kê khai `scope`.
- Blazor Server cần một mạch SignalR trực tiếp → **cài đặt được + app-shell**, không phải ngoại tuyến đầy đủ. Không
  hứa hẹn tương tác ngoại tuyến.

## 8. Khả năng truy cập

- Nhãn trên đầu vào, `aria-*` trên các kiểm soát tùy chỉnh, tiêu điểm vidible, thứ tự tiêu điểm hợp lý. Vì chủ đề là
  white-labelable, xác minh **contrast** chống lại chủ đề hoạt động, không phải một bảng màu cố định.

## 9. E2E — không giao diện UI không kiểm tra (chặn)

Mỗi thay đổi hướng đến người dùng giao Playwright E2E trong `tests/E2ETests`, được điều khiển như một người dùng thực, **trên di động
mô phỏng thiết bị** cộng với máy tính để bàn:

- Tuyến mới → thêm nó vào `PageSmokeTests` **và** `MobileLayoutTests` (hiển thị, điều hướng dưới cùng, không lỗi UI).
- Chuyển đổi một bảng/trang → thêm tuyến của nó vào mobile **no-overflow** set.
- Quy trình mới → một hành trình di động thực tế (tạo/chỉnh sửa/lưu vòng) **và** một con đường không hạnh phúc
  (đầu vào không hợp lệ, danh sách trống, quyền từ chối mỗi vai trò).
- Mẹo trợ giúp mới → khẳng định nó mở trên tap (`HelpTipTests` pattern).
- Sử dụng `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` (mô phỏng thiết bị).
- `dotnet test` xanh trước "xong". WebKit được mô phỏng ≠ Mobile Safari — gating thiết bị thực là một bước phát hành riêng biệt.

## 10. Định nghĩa hoàn thành (UI)

- [ ] Điều khiển di động-trước; không cuộn ngang 320–1920px; chạm mục tiêu ≥44px.
- [ ] Chỉ các token thiết kế — không có màu/radii/brand strings mã hóa cứng.
- [ ] Bảng → thẻ trên điện thoại (`DataLabel` + `Breakpoint.Sm`); tải/trống/lỗi trạng thái hiện tại.
- [ ] Tạo/chỉnh sửa qua hộp thoại; toàn màn hình trên di động.
- [ ] Mỗi kiểm soát có một `HelpTip` được lấy từ tài liệu.
- [ ] White-label + PWA được tôn trọng.
- [ ] Di động + desktop E2E được thêm (khói, no-overflow, hành trình, đường không hạnh phúc); `dotnet test` xanh.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` sạch trên các tệp cảm ứng.
