---
description: "Ứng dụng rebrand nhà bán lại — tên sản phẩm, logo, favicon, màu sắc, CSS tùy chỉnh — thông qua cấu hình triển khai, không thay đổi mã. Mỗi giá trị branding mặc định là nhận dạng stock..."
---

# White-label branding

Ứng dụng rebrand nhà bán lại — tên sản phẩm, logo, favicon, màu sắc, CSS tùy chỉnh — thông qua cấu hình triển khai, không thay đổi mã. Mỗi giá trị branding **mặc định cho nhận dạng stock**: triển khai không được cấu hình trông giống như trước đây; nhà bán lại chỉ ghi đè những gì cần.

## Mô hình

- `Core.Options.BrandingOptions` — được ràng buộc từ `App:Branding`. String-based (cạnh cấu hình); mỗi màu được xác thực khi theme được xây dựng.
- `Core.Branding.HexColor` — đối tượng giá trị cho màu CSS hex (`#RGB` / `#RRGGBB`), immutable, tự xác thực. Màu không hợp lệ ném `DomainException` (`domain.branding.color_invalid`) khi theme được xây dựng — triển khai sai cấu hình thất bại nhanh chóng khi khởi động, không kết xuất bảng màu bị hỏng.
- `Web.Components.Theme.Build(BrandingOptions)` — tạo ra chủ đề MudBlazor từ branding. Chỉ các mục bảng màu được đặt tên thương hiệu đến từ cấu hình; kiểu chữ, bố cục, tông màu bề mặt trung tính vẫn ở lại cố định vì vậy sản phẩm giữ giao diện gắn kết trên các nhà bán lại.
- `Web.Branding.IBrandingThemeProvider` — singleton, xây dựng theme một lần, xây dựng lại khi tùy chọn thay đổi. Được tiêm bởi `MainLayout`/`EmptyLayout` cho `MudThemeProvider`, bởi thanh ứng dụng cho tên sản phẩm/logo. `App.razor` đọc `IOptionsMonitor<AppOptions>` trực tiếp cho trang `<head>` (tiêu đề, mô tả, favicon, theme-colour, CSS tùy chỉnh).

## Cấu hình

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Hình thức biến môi trường: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Khóa | Hiệu ứng | Mặc định |
|-----|--------|---------|
| `ProductName` | Văn bản thanh ứng dụng + trang `<title>` | `cMind` |
| `LogoUrl` | Hình ảnh logo thanh ứng dụng; khi trống, văn bản tên sản phẩm hiển thị | *(trống)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | mô tả stock |
| `PrimaryColor` / `SecondaryColor` | nhấn, biểu tượng ngăn kéo, nút | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + bề mặt; `AppBarColor` lái `<meta theme-color>` + manifest PWA `theme_color`, `BackgroundColor` manifest `background_color` | bảng màu tối |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | màu trạng thái | stock |
| `CustomCss` | tiêm `<style>` trong `<head>` (triển khai-tin tưởng) | *(trống)* |
| `ShowSiteLink` | hiển thị liên kết tín dụng "Powered by cMind" trên bảng điều khiển | `true` |
| `RequireMfa` | yêu cầu mỗi người dùng thiết lập xác thực hai yếu tố trước khi sử dụng ứng dụng | `false` |
| `NodesUi` | bao nhiêu bề mặt Nodes được gửi: `Full` (danh sách + thêm/xóa thủ công), `Monitor` (danh sách chỉ đọc, không thêm/xóa), `Hidden` (không điều hướng, không trang, không API thủ công) | `Full` |
| `RestrictNodesToOwner` | khi `true`, chỉ chủ sở hữu mới có thể xem/quản lý nút; nếu không toàn bộ bề mặt nhân viên quản trị viên hoặc ở trên. Người dùng thông thường không bao giờ nhìn thấy nút dù sao | `false` |

Tài sản được tham chiếu bởi `LogoUrl`/`FaviconUrl` được phục vụ từ ứng dụng Web `wwwroot` (ví dụ gắn kèm thư mục `wwwroot/branding/`) hoặc bất kỳ URL tuyệt đối nào.

`App:Branding` được xác thực khi khởi động (`BrandingOptionsValidator`, chạy qua `ValidateOnStart`): mỗi màu phải là hex hợp lệ, `CustomCss` không được chứa `<`/`>` (không thể thoát khỏi thẻ `<style>`). Triển khai sai cấu hình không boot với thông báo rõ ràng, không kết xuất trang bị hỏng.

## Powered-by link

Bảng điều khiển kết xuất một liên kết tín dụng nhỏ **"Powered by cMind"** trỏ đến trang tài liệu của dự án. Nó được kiểm soát bởi `App:Branding:ShowSiteLink` và là **`true` theo mặc định** — một triển khai không được cấu hình hiển thị nó. Nhà bán lại chạy một phiên bản được rebrand hoàn toàn đặt `App__Branding__ShowSiteLink=false` để xóa nó hoàn toàn.

Liên kết được phát hành bởi thành phần bảng điều khiển và đọc cờ thông qua `IBrandingThemeProvider` / `BrandingOptions`, vì vậy bật nó là thay đổi chỉ cấu hình (không xây dựng lại). Xem [White-label cho doanh nghiệp](../white-label-for-business.md#the-powered-by-cmind-link) để có bản tóm tắt hướng tới doanh nghiệp.

## Danh sách cho phép nhà môi giới

Triển khai white-label có thể hạn chế các nhà môi giới có tài khoản giao dịch mà người dùng của nó có thể thêm — vì vậy nhà môi giới chạy cMind cho chính các khách hàng của nó chỉ phục vụ cuốn sách của nó. Được cấu hình dưới `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Hình thức biến môi trường: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Hành vi:**

- **Danh sách trống (mặc định) ⇒ không hạn chế.** Mọi nhà môi giới đều được phép và **không xác minh chạy** — triển khai stock hoàn toàn không thay đổi.
- **Non-empty ⇒ restricted.** cMind kiểm tra mọi tài khoản mà người dùng cố gắng thêm so với danh sách (không phân biệt chữ hoa chữ thường):
  - **Liên kết Open API (OAuth)** — tên nhà môi giới được báo cáo có thẩm quyền bởi cTrader Open API, vì vậy tài khoản không được phép chỉ bị **bỏ qua** (các tài khoản được phép trong cấp ghi vẫn được liên kết); trang ủy quyền cho người dùng biết những nhà môi giới nào đã bị bỏ qua.
  - **CID thủ công (tên người dùng / mật khẩu)** — nhà môi giới được gõ bởi người dùng **không** được tin tưởng. cMind **xác minh** nhà môi giới thực của tài khoản bằng cách chạy cBot broker-probe được gửi kèm qua cTrader CLI (đọc `Account.BrokerName`) và duy trì tên được xác minh. Nhà môi giới không được phép bị từ chối bằng thông báo; lỗi xác minh (thông tin xác thực xấu, không nút, hết thời gian chờ) được bề mặt quá, và tài khoản không được thêm.

**Mô hình:**

- `Core.Options.AccountsOptions` — được ràng buộc từ `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — đối tượng giá trị (được xén, bằng nhau không phân biệt chữ hoa chữ thường).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; trống = cho phép tất cả. Được thực thi như một bất biến bên trong `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — chạy thùng chứa probe trên máy chủ web (có ổ cắm Docker), đuôi nhật ký, và phân tích nhà môi giới qua `Core.Accounts.BrokerProbeOutput`. Chỉ được gọi khi danh sách cho phép bị hạn chế.

**Broker-probe cBot:** một `broker-probe.algo` được xây dựng trước được gửi kèm với ứng dụng Web (`src/Web/BrokerProbe/`, được sao chép vào đầu ra là `broker-probe/broker-probe.algo`), vì vậy `App:Accounts:BrokerProbeAlgoPath` mặc định giải quyết ngay từ hộp — đường dẫn tương đối được giải quyết so với thư mục cơ sở ứng dụng, đường dẫn tuyệt đối được sử dụng như được cung cấp. Nguồn sống trong `tools/broker-probe/`. Khi algo bị vắng mặt, xác minh CID thủ công thất bại khép — tài khoản theo danh sách cho phép bị hạn chế vẫn có thể được liên kết qua đường dẫn Open API, không cần probe.

## Danh sách cho phép nhà môi giới — kiểm tra

- **Đơn vị** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` đối tượng giá trị, `BrokerProbeOutput` trình phân tích cú pháp, và bất biến danh sách cho phép `CTraderIdAccount`.
- **Tích hợp** — `IntegrationTests/BrokerAllowlistTests.cs`: điểm cuối CID thủ công với bộ xác minh giả (không hạn chế / được xác minh / không được phép / xác minh-thất bại) + bộ liên kết Open API bỏ qua tài khoản không được phép. `BrokerVerifierLiveTests.cs` chạy probe **thực** khi được cung cấp cID creds + algo (bỏ qua sạch sẽ nếu không).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: triển khai bị hạn chế từ chối thêm thủ công qua giao diện người dùng thực và hiển thị thông báo "không thể xác minh" (không có hàng tài khoản được thêm).

## Thể hiện Nodes UI

Nút là cơ sở hạ tầng hầu hết các đối tượng thuê không bao giờ quản lý bằng tay — tác nhân CLI cTrader [tự đăng ký và nhịp tim](../operations/node-discovery.md), vì vậy triển khai white-label có thể ẩn các điều khiển thủ công hoặc bề mặt Nodes hoàn toàn, và vẫn chạy một cụm lành mạnh thông qua khám phá tự động. Hai khóa branding chỉ cấu hình này:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Hình thức biến môi trường: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — ba chế độ:**

- **`Full` (mặc định)** — sản phẩm stock: danh sách nút cộng với **Nút mới** thủ công và điều khiển **Xóa**. `POST`/`DELETE /api/nodes` hoạt động.
- **`Monitor`** — bề mặt chỉ đọc: danh sách và thống kê trực tiếp vẫn ở, nhưng thêm thủ công và xóa bị xóa. Nút chỉ khi nào xuất hiện thông qua khám phá tự động. `POST`/`DELETE /api/nodes` trả về **404**.
- **`Hidden`** — liên kết Nút điều hướng và trang hoàn toàn biến mất và tuyến trang chuyển hướng đến bảng điều khiển; API thêm/xóa thủ công tắt. Cụm chỉ khám phá tự động.

**`RestrictNodesToOwner`** tầng ai có thể xem và quản lý nút. Mặc định `false` giữ bề mặt nhân viên **admin-or-above** tiêu chuẩn (`AdminOrAbove`); đặt `true` để làm cho nó **chỉ chủ sở hữu** (`Owner`). Dù sao **người dùng thông thường không bao giờ nhìn thấy nút** — điều này chỉ chọn giữa chỉ chủ sở hữu và bề mặt nhân viên rộng hơn.

Nút **khám phá tự động không bị ảnh hưởng bởi cả hai khóa**: điểm cuối `POST /api/nodes/register` đăng ký tự động + nhịp tim ẩn danh luôn hoạt động, vì vậy triển khai `Hidden`/`Monitor` vẫn phát triển cụm của nó tự động.

**Mô hình:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — nguồn sự thật duy nhất sáng tác chế độ + hạn chế chủ sở hữu: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Điều hướng (`NavMenu.razor`), trang (`Pages/Nodes.razor`) và điểm cuối (`NodeEndpoints`) đều đọc nó vì vậy giao diện người dùng và API không bao giờ có thể không đồng ý.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — được ràng buộc từ `App:Branding`.

## Thể hiện Nodes UI — kiểm tra

- **Đơn vị** — `UnitTests/Nodes/NodesUiAccessTests.cs`: độ hiển thị trang, độ phân giải quản lý thủ công và yêu cầu-policy trên mỗi chế độ + branding mặc định.
- **Tích hợp** — `IntegrationTests/NodeUiGatingTests.cs`: trên HTTP + Postgres thực — `Full` cho phép thêm thủ công, `Monitor`/`Hidden` 404 thêm và xóa, và `RestrictNodesToOwner` cấm quản trị viên trong khi chủ sở hữu vẫn đọc danh sách.
- **E2E** — `E2ETests/NodesUiTests.cs` (mặc định `Full`: liên kết điều hướng + trang + nút Nút mới kết xuất) và `E2ETests/NodesHiddenTests.cs` (`Hidden`: liên kết điều hướng biến mất, `/nodes` chuyển hướng).

## Mã thông báo thiết kế (Biến CSS)

Branding cũng đạt đến stylesheet **riêng của nó** + các thành phần tùy chỉnh của ứng dụng, không chỉ MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` phát ra bảng màu được đặt tên thương hiệu dưới dạng thuộc tính tùy chỉnh CSS trên `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), tiêm trong `App.razor` ngay sau `site.css`. `site.css` và mỗi thành phần đọc `var(--app-*)` — **không màu cứng** — vì vậy bảng màu của nhà bán lại chảy ở mọi nơi (đăng nhập anh hùng, điều hướng dưới cùng, mẹo trợ giúp, trang ngoại tuyến) miễn phí. Tông màu bề mặt trung tính mặc định trong `site.css :root`; `CustomCss` (tiêm cuối cùng) có thể ghi đè bất kỳ mã thông báo nào. Xem [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA được đặt tên thương hiệu

Ứng dụng có thể cài đặt cũng được đặt tên thương hiệu — điểm cuối manifest (`/manifest.webmanifest`) được xây dựng từ `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → chủ đề/nền). Xem [pwa.md](pwa.md).

## Các bài kiểm tra

- **Đơn vị** — `UnitTests/Branding/HexColorTests.cs`: xác thực hex hợp lệ/không hợp lệ.
- **Tích hợp** — `IntegrationTests/ThemeBuildTests.cs`: màu ánh xạ vào bảng màu, màu không hợp lệ ném; `IntegrationTests/BrandingHttpTests.cs`: `ProductName`/description/theme-colour tùy chỉnh kết xuất trong trang phục vụ `<head>` (WebApplicationFactory + Postgres), giữ mặc định tên stock.
- **E2E** — `E2ETests/BrandingTests.cs`: tên sản phẩm được đặt tên thương hiệu kết xuất trong thanh ứng dụng trong trình duyệt thực.
