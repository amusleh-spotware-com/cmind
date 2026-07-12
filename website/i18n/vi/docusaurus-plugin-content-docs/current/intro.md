---
slug: /intro
title: Chào mừng đến với cMind
description: Giới thiệu thân thiện về cMind — nền tảng vận hành giao dịch mã nguồn mở, tự lưu trữ cho cTrader.
sidebar_position: 1
---

# Chào mừng đến với cMind 👋

Vậy là bạn muốn xây bot giao dịch, backtest chúng mà không làm chảy laptop, chạy chúng trên vài máy,
sao chép giao dịch sang cả chục tài khoản, và để AI trông chừng rủi ro trong khi bạn ngủ. **Bạn đang ở
đúng nơi rồi.**

cMind là một **nền tảng vận hành giao dịch mã nguồn mở, tự lưu trữ cho cTrader**. Hãy hình dung nó như
toàn bộ bàn giao dịch của bạn — soạn thảo, thực thi, một đội máy tính, sao chép giao dịch và một lõi AI
— gói gọn trong một ứng dụng điềm tĩnh, tối màu, thân thiện với di động mà bạn sở hữu từ đầu đến cuối.

:::tip Trong một câu
Xây dựng → backtest → chạy → sao chép các chiến lược cTrader của bạn ở quy mô lớn, với AI tích hợp, trên
máy chủ của riêng bạn, dưới thương hiệu của riêng bạn.
:::

## Nó thực sự làm được gì?

| Bạn muốn… | cMind làm điều đó | Đọc thêm |
|---|---|---|
| Viết một cBot trong trình duyệt | Monaco IDE + mẫu C#/Python, build trong sandbox | [Xây dựng & backtest](./features/build-and-backtest.md) |
| Backtest trên nhiều máy | Đội node tự phục hồi chọn máy rảnh nhất | [Mở rộng quy mô](./deployment/scaling.md) |
| Sao chép một tài khoản sang nhiều | Sao chép mạnh mẽ có đồng bộ lại, không giao dịch trùng | [Sao chép giao dịch](./features/copy-trading.md) |
| Để AI làm việc nặng | Tạo chiến lược, tự sửa lỗi, hộ vệ rủi ro, phân tích sau sự cố | [Lõi AI](./features/ai.md) |
| Tuân thủ luật prop firm | Theo dõi vốn chủ sở hữu trực tiếp + mô phỏng luật thử thách | [Prop firm](./features/prop-firm.md) |
| Phát hành như sản phẩm *của bạn* | White-label đầy đủ: tên, màu sắc, logo, favicon | [White-label](./features/white-label.md) |
| Chạy nó trên điện thoại | PWA cài đặt được, ưu tiên di động | [PWA](./features/pwa.md) |
| Điều khiển từ một client AI | Máy chủ MCP tích hợp (HTTP + SSE) | [MCP](./features/mcp.md) |

## Lộ trình 5 phút ⏱️

Nếu bạn có Docker và năm phút, bạn có thể vọc một instance cMind thật ngay bây giờ:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Sau đó mở **<http://localhost:8080>**, đăng nhập, và bạn đã sẵn sàng. Hướng dẫn đầy đủ (kèm khắc phục sự
cố cho khi Docker chắc chắn sẽ có ý kiến riêng) nằm ở **[Chạy cục bộ](./deployment/local.md)**.

## Mới ở đây? Đi theo con đường gạch vàng 🟡

1. **[Dành cho ai?](./audience.md)** — hãy chắc rằng bạn là kiểu rắc rối của chúng tôi.
2. **[Chạy cục bộ](./deployment/local.md)** — dựng một instance thật.
3. **[Tính năng](./features/README.md)** — chuyến tham quan đầy đủ những gì bên trong.
4. **[Triển khai thực sự](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Biến nó thành của bạn](./white-label-for-business.md)** — áp dụng white-label cho doanh nghiệp của bạn.
6. **[Đóng góp](./contributing.md)** — PR (con người *và* có AI hỗ trợ) rất được hoan nghênh.

## Đôi lời nhanh về tiền bạc 💸

cMind vận hành **vốn thật**. Chúng tôi xem điều đó nghiêm túc — mọi thay đổi đều được giao kèm kiểm thử
đơn vị, tích hợp và đầu-cuối, bao gồm các đường lỗi (mất kết nối, lệnh bị từ chối, node chết). Bạn cũng
nên xem nó nghiêm túc: **hãy thử trên tài khoản demo trước**, và đọc
[ghi chú tuân thủ](./features/compliance.md) trước khi hướng nó vào bất cứ thứ gì thật. Giao dịch có rủi
ro; phần mềm này là một công cụ, không phải lời khuyên tài chính.

Được rồi — đủ lời mở đầu. Đi xây gì đó thôi. →
