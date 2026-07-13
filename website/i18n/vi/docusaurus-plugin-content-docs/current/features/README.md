---
slug: /features
title: Tính năng — chuyến du ngoạn đầy đủ
description: Tất cả những gì cMind có thể làm — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP, và hơn thế nữa.
sidebar_label: Tổng quan
---

# Tính năng — chuyến du ngoạn đầy đủ 🧭

Chào mừng đến chuyến du ngoạn hoành tráng. cMind đóng gói *rất nhiều* vào một ứng dụng, vì vậy đây là bản đồ. Mỗi khả năng có tài liệu deep-dive riêng của nó — nhấp vào bất kỳ thứ gì bạn đang tìm kiếm.

## 🔁 Copy trading

Viên kim cương quý. Phản chiếu tài khoản chính sang nhiều tài khoản, và giữ chúng đồng bộ ngay cả khi internet hoạt động không tốt.

- **[Copy trading](./copy-trading.md)** — cốt lõi: mirroring, order types, SL/TP, slippage, desync/resync.
- **[Execution transparency](./copy-execution-transparency.md)** — xem chính xác những gì được copy, khi nào, và tại sao.
- **[Performance fees](./copy-performance-fees.md)** — tính phí cho tín hiệu của bạn, kiểu high-water-mark.
- **[Provider marketplace](./copy-provider-marketplace.md)** — cho phép traders khám phá và theo dõi providers.
- **[Notifications](./copy-notifications.md)** — được thông báo khi có gì cần bạn.
- **[AI copy recommender](./ai-copy-recommender.md)** — để AI gợi ý ai để copy.
- **[Open API token lifecycle](./token-lifecycle.md)** — cách cMind giữ chính xác một token hợp lệ per cID.

## 📊 Cơ sở nhà của bạn

- **[Dashboard](./dashboard.md)** — trung tâm chỉ huy di động trực tiếp: KPIs với sparklines, biểu đồ hoạt động, vòng trạng thái, feed trực tiếp, và (cho admins) tình trạng cluster. Nó làm tươi bản thân.

## 🧠 AI core

Không phải một hộp chat được bolted vào bên cạnh — AI mà thực sự *làm công việc*.

- **[AI assistant, agent, risk guard & alerts](./ai.md)** — strategy generation, self-repairing builds, background risk guard có thể auto-stop bots, và smart alerts.

## 🛠️ Build & run

- **[Build & backtest cBots](./build-and-backtest.md)** — in-browser Monaco IDE, C#/Python templates, sandboxed builds, và live equity curves.
- **[MCP server](./mcp.md)** — hiển thị các công cụ của cMind qua HTTP + SSE để AI clients có thể chạy nó.

## 🏢 Chạy nó như một doanh nghiệp

- **[White-label / branding](./white-label.md)** — rebrand mỗi surface qua config.
- **[Prop-firm challenge simulation](./prop-firm.md)** — thực thi daily-loss, drawdown, và target rules với live equity.
- **[Feature toggles](./feature-toggles.md)** — quyết định những gì mỗi deployment/tenant nhìn thấy.
- **[Compliance / legal](./compliance.md)** — audit trail và legal surface.

## 📱 Trải nghiệm

- **[Installable app (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[UI design system & mobile-first](../ui-guidelines.md)** — design tokens và rules đằng sau look.

## ⚙️ Phía dưới động cơ

Các bits vận hành giữ tất cả chạy:

- **[Node fleet & discovery](../operations/node-discovery.md)** — cách nodes tự đăng ký và chữa lành.
- **[Horizontal scaling](../deployment/scaling.md)** — thêm replicas, không cần external coordinator.
- **[Logging & audit](../operations/logging.md)** — structured logs + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — chạy nó ở bất kỳ nơi nào.

:::note Giữ docs trung thực
Mỗi feature doc được giữ đồng bộ với code — thay đổi behavior, cập nhật doc, same commit. Nếu bạn bao giờ phát hiện drift, đó là bug: vui lòng [mở issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) hoặc gửi PR. 🙏
:::
