---
slug: /white-label-for-business
title: White-label cho kinh doanh
description: Ship cMind như sản phẩm được xây dựng của riêng bạn — cho các công ty prop-firm, broker và các kinh doanh giao dịch sao chép. Rebrand mọi bề mặt qua cấu hình, không có thay đổi mã.
sidebar_position: 4
---

# White-label cMind cho kinh doanh của bạn 🏢

Chạy một công ty prop-firm, một bàn làm việc broker hoặc một dịch vụ giao dịch sao chép? cMind được xây dựng từ ngày đầu tiên để
**bán lại như sản phẩm được xây dựng của riêng bạn**. Mọi bề mặt — tên, logo, favicon, màu sắc, thậm chí
ứng dụng điện thoại có thể cài đặt được — bends với thương hiệu của bạn. Khách hàng của bạn thấy *your* công ty. Không có thay đổi mã,
không rẽ nhánh, chỉ cấu hình.

:::tip TL;DR
Chỉ `App:Branding` tại tên, màu sắc và logo của bạn. Khởi động lại. Xong. Tham chiếu kỹ thuật đầy đủ sống
trong [Tài liệu tính năng White-label](./features/white-label.md).
:::

## Những gì bạn có thể rebrand

| Bề mặt | Thay đổi gì |
|---|---|
| **Tên sản phẩm** | Văn bản thanh ứng dụng + tiêu đề tab trình duyệt |
| **Logo & favicon** | Dấu của bạn ở khắp nơi, bao gồm cả tab trình duyệt |
| **Màu sắc** | Bảng màu đầy đủ — màu sắc chính, bề mặt, trạng thái — chảy qua toàn bộ UI *và* CSS của chính ứng dụng qua các token thiết kế |
| **Ứng dụng có thể cài đặt được (PWA)** | Tên thêm-vào-màn hình chính, biểu tượng và splash sử dụng thương hiệu của bạn |
| **Meta / SEO** | Mô tả và URL hỗ trợ là của bạn |
| **CSS tùy chỉnh** | Bơm văn bản của riêng bạn cho 5% cuối cùng |

Mọi thứ mặc định với danh tính cMind cổ phiếu, vì vậy bạn chỉ ghi đè những gì bạn quan tâm.

## Rebrand 60 giây

Đặt những cái này trên triển khai của bạn (cấu hình JSON hoặc biến môi trường):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Dạng biến môi trường: `App__Branding__ProductName=AcmeFX`. Màu sắc được xác thực khi khởi động —
một giá trị hex xấu không thành công khi khởi động với một thông báo rõ ràng thay vì hiển thị một trang bị hỏng. Tốt và
ồn ào, chính xác khi bạn muốn nó.

## Liên kết "Được hỗ trợ bởi cMind"

Theo **default**, bảng điều khiển hiển thị một liên kết nhỏ, lịch sự **"Được hỗ trợ bởi cMind"** mà
chỉ các khách trở lại trang web này. Nó bật mặc định vì chúng tôi tự hào về dự án và
nó giúp các trader khác tìm thấy nó — nhưng nó **call của bạn**.

- **Giữ nó** (mặc định): một liên kết tín dụng tinh tế trên bảng điều khiển. Không tốn kém bất cứ điều gì, giúp dự án.
- **Ẩn nó**: đặt `App__Branding__ShowSiteLink=false` và nó biến mất hoàn toàn — hoàn hảo cho một
  triển khai white-label đầy đủ nơi sản phẩm là không thể nhầm lẫn *yours*.

Xem [Tài liệu tính năng White-label](./features/white-label.md#powered-by-link) cho chính xác nơi nó
hiển thị.

## Multi-tenant, thương hiệu mỗi khách hàng

Vì thương hiệu chỉ là cấu hình triển khai, mỗi triển khai người thuê có thể mang danh tính riêng. Chạy một
thực thể riêng biệt cho mỗi khách hàng, hoặc lái thương hiệu từ plane kiểm soát của riêng bạn — ứng dụng đọc nó từ
`IOptionsMonitor`, vì vậy nó thậm chí có thể xây dựng lại chủ đề trực tiếp khi các tùy chọn thay đổi.

Cặp với:

- **[Chuyển đổi tính năng](./features/feature-toggles.md)** — quyết định tính năng nào mỗi người thuê nhìn thấy.
- **[Quy tắc Prop-firm](./features/prop-firm.md)** — thực thi các quy tắc thách thức của bạn với theo dõi vốn trực tiếp.
- **[Phí hiệu suất](./features/copy-performance-fees.md)** + **[marketplace nhà cung cấp](./features/copy-provider-marketplace.md)** — kiếm tiền từ giao dịch sao chép.
- **[Tuân thủ](./features/compliance.md)** — giữ dòng kiểm toán mà cơ quan quản lý của bạn sẽ hỏi.

## Tài sản & Hosting

Bỏ logo/favicon của bạn vào ứng dụng Web `wwwroot/branding/` (hoặc chỉ `LogoUrl`/`FaviconUrl`
ở bất kỳ URL tuyệt đối nào). Triển khai tuy nhiên phù hợp với bạn — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) hoặc
[AWS](./deployment/cloud-aws.md).

Sẵn sàng làm cho nó của bạn? Bắt đầu với [tham chiếu kỹ thuật white-label →](./features/white-label.md)
