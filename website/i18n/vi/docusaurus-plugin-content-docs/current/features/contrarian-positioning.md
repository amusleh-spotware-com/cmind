---
description: "Vị thế Contrarian bán lẻ — chuyển % trader bán lẻ đang long thành thiên kiến contrarian (đánh ngược đám đông khi nó nghiêng hẳn), cộng với các value object tín hiệu point-in-time bảo vệ chống look-ahead bias."
---

# Vị thế Contrarian bán lẻ

Đám đông bán lẻ là một trong số ít tín hiệu tâm lý thực sự hữu ích trong FX — như một chỉ báo **contrarian**.
Khi đa số trader bán lẻ đang long, giá theo lịch sử có xu hướng giảm,
và ngược lại. Công cụ này chuyển vị thế đám đông thành một đọc có thể hành động.

Mở **cBots → Contrarian Positioning** (`/quant/positioning`).

## Nó làm gì

Nhập **% trader bán lẻ đang long** (từ trang sentiment của broker hoặc nguồn cấp như FXSSI) và
nó trả về:

- **Thiên kiến Contrarian** — **Giảm giá** khi ≥ 60% đang long (đám đông quá long), **Tăng giá** khi ≤ 40% đang
  long (đám đông quá short), **Trung lập** trong band 40–60% không quyết định;
- **Độ mạnh** — mức độ đám đông nghiêng hẳn (0 = cân bằng, 1 = hoàn toàn một phía), để weighing tín hiệu.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time theo cấu trúc

Bên dưới, lớp tín hiệu (`Core.Signals`) mô hình hóa một `PointInTimeSignal` được **đóng dấu với
thời điểm nó có thể biết được** và từ chối được khởi tạo nếu không có nó. Bất kỳ backtest hoặc tác nhân tự trị nào
tiêu thụ một tín hiệu đều kiểm tra `IsKnownAt(decisionTime)` — vì vậy dữ liệu tương lai không bao giờ rò rỉ vào quyết định lịch sử. Look-ahead bias là kẻ giết khả năng tái tạo hàng đầu trong tài chính định lượng; mô hình miền làm cho nó
về mặt cấu trúc không thể xảy ra.

## Tại sao nó đáng tin cậy

Mã miền tất định thuần không phụ thuộc hạ tầng — các ngưỡng contrarian và guard point-in-time được unit-test, bao gồm các ranh giới 40/60 và từ chối ngoài phạm vi.
