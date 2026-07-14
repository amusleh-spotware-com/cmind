---
slug: /for-brokers
title: cMind cho các broker cTrader
description: Tại sao một broker cTrader nên chạy một cMind white-label cho các khách hàng của riêng mình — cung cấp cho các trader AI, giao dịch sao chép và những thách thức prop-firm dưới thương hiệu của bạn, hạn chế tài khoản của bạn để nhà môi giới, và giành được lợi thế so với các đối thủ.
keywords:
  - cTrader broker
  - white-label trading platform
  - broker technology
  - copy trading for brokers
  - AI trading tools
  - prop firm software
sidebar_position: 6
---

# cMind cho các broker cTrader 🏦

Bạn chạy một brokerage cTrader. Các khách hàng của bạn đã có thể giao dịch — nhưng vậy cũng mọi khách hàng của broker khác.
**cMind cho phép bạn trao cho các trader của bạn một nền tảng hoạt động giao dịch được hỗ trợ AI đầy đủ, được xây dựng dưới thương hiệu của bạn**,
vì vậy họ xây dựng, backtest, chạy, sao chép và giám sát chiến lược bên trong *your* hệ sinh thái
thay vì trôi dạo đến một công cụ của bên thứ ba. Đó là các khách hàng dính hơn, khối lượng lớn hơn và một lợi thế thực sự
so với các broker chỉ cung cấp một terminal.

:::tip[TL;DR]
Chạy một cMind white-label cho các khách hàng của bạn. Hạn chế tài khoản của bạn đến **your** brokerage, bật AI và
giao dịch sao chép, và ship nó dưới thương hiệu của bạn. → [White-label cho kinh doanh](./white-label-for-business.md)
:::

## Lợi thế bạn nhận được so với các broker khác

- **Phân biệt trên công cụ, không chỉ là sự lan truyền.** Cung cấp cho các khách hàng AI cBot tạo, backtesting trên một
  cụm được quản lý, sao chép giao dịch và những thách thức prop-firm — khả năng hầu hết các broker chỉ đơn giản là không
  cung cấp.
- **Giữ các khách hàng trong hệ sinh thái của bạn.** Khi các trader xây dựng và chạy chiến lược của họ bên trong nền tảng được xây dựng của bạn,
  họ ở lại. Giữ lại là toàn bộ trò chơi.
- **Dưới thương hiệu của bạn, trên tên miền của bạn.** Tên, logo, màu sắc, favicon, thậm chí ứng dụng điện thoại có thể cài đặt được —
  tất cả của bạn. Không ai nhìn thấy "cMind." → [Tính năng White-label](./features/white-label.md)

## Phục vụ chỉ các tài khoản của bạn (allowlist broker)

Chạy một white-label cho *your* khách hàng? Hạn chế những broker nào' tài khoản giao dịch người dùng có thể thêm để
triển khai của bạn chỉ phục vụ sách của bạn:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

Khi danh sách cho phép được đặt, cMind kiểm tra mọi tài khoản mà người dùng cố gắng thêm — cả qua cTrader Open
API và qua cID đăng nhập thủ công (được xác minh bằng cách đọc tên broker thực sự của tài khoản) — và từ chối bất kỳ
tài khoản nào không có trong danh sách của bạn. Để nó trống và mỗi broker được phép (mặc định). Xem
[Tài liệu tính năng White-label](./features/white-label.md#broker-allowlist) cho toàn bộ cơ chế.

## Gửi một ứng dụng Open API cho tất cả người dùng của bạn

Bỏ qua các phiền toái trên mỗi người dùng: cung cấp **một ứng dụng cTrader Open API** và mỗi khách hàng xác thực
tài khoản của họ thông qua nó — khách hàng không bao giờ đăng ký của riêng họ. Đăng ký một URL chuyển hướng duy nhất, bỏ
thông tin xác thực vào cấu hình hoặc cài đặt chủ sở hữu và chế độ chia sẻ bật cho tất cả mọi người. Đã đàm phán một
giới hạn tin nhắn cTrader cao hơn? Điều chỉnh **giới hạn tỷ lệ máy khách mỗi loại tin nhắn** (hoặc vô hiệu hóa tốc độ).
→ [Ứng dụng Open API được chia sẻ & giới hạn tỷ lệ](./features/open-api-shared-app.md)

## Những cách mới để kiếm tiền

- **AI, không có sự cọ xát nào cho các khách hàng.** Cung cấp khóa nhà cung cấp AI mặc định ở mức triển khai và
  mỗi khách hàng nhận được các tính năng AI ngay lập tức — không có đăng ký ở nơi khác. Đánh dấu nó, hoặc bó nó vào cao cấp
  tầng. Các khách hàng vẫn có thể mang khóa của riêng họ. → [Tính năng AI](./features/ai.md)
- **Những thách thức prop-firm.** Chạy các thách thức trader được tài trợ với theo dõi vốn trực tiếp và các quy tắc được thực thi,
  và tính phí cho các mục. → [Quy tắc Prop-firm](./features/prop-firm.md)
- **Kinh doanh giao dịch sao chép.** Phí hiệu suất và một thị trường nhà cung cấp biến giao dịch sao chép thành
  doanh thu. → [Phí hiệu suất](./features/copy-performance-fees.md) ·
  [Thị trường nhà cung cấp](./features/copy-provider-marketplace.md)
- **Tầng tính năng.** Quyết định tính năng nào mỗi phân khúc khách hàng thấy với
  [chuyển đổi tính năng](./features/feature-toggles.md).

## Được quy định, có thể kiểm toán, đa thuê bao

- **[Tuân thủ](./features/compliance.md)** nhật ký cung cấp cho bạn dòng kiểm toán mà cơ quan quản lý của bạn sẽ hỏi.
- **[Xác thực hai yếu tố](./features/two-factor-auth.md)** có thể bắt buộc cho mỗi triển khai.
- **Thương hiệu mỗi khách hàng** — chạy một thực thể được xây dựng riêng biệt cho mỗi phân khúc, được điều khiển từ plane kiểm soát của riêng bạn.
  → [Thương hiệu đa thuê bao](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Cách bắt đầu

1. Đọc [White-label cho kinh doanh](./white-label-for-business.md) cho sự rebrand 60 giây.
2. Đặt `App:Accounts:AllowedBrokers` thành nhà môi giới của bạn và chọn [tập tính năng](./features/feature-toggles.md) của bạn.
3. [Triển khai](./deployment/cloud.md) nó — Docker, Kubernetes, Azure hoặc AWS.

Không muốn chạy cơ sở hạ tầng yourself? Nhà cung cấp lưu trữ có thể hoạt động một cMind được quản lý cho bạn
— chỉ họ [Cho các nhà cung cấp cloud & VPS](./for-cloud-providers.md).

## Hình dạng lộ trình

cMind là mã nguồn mở. Những broker xây dựng trên nó nhận được lời nói quá khổ trong nơi nó đi — yêu cầu
các tích hợp và kiểm soát bạn cần, và đóng góp chúng trở lại qua
[Hướng dẫn Đóng góp](./contributing.md).
