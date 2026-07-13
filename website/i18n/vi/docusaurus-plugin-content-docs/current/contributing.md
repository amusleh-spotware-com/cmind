---
slug: /contributing
title: Đóng góp
description: Cách đóng góp vào cMind — các PR được hỗ trợ bởi con người hoặc AI được chào đón. Đóng góp đầu tiên trong 10 phút.
sidebar_position: 5
---

# Đóng góp vào cMind 🛠️

Cảm ơn bạn vì đã ở đây. cMind trở nên tốt hơn mỗi lần ai đó mở một vấn đề, báo cáo hành vi cTrader chính xác,
sửa một lỗi chính tả trong các tài liệu này hoặc gửi một PR. **Bạn không cần phải là một .NET
wizard** — các nhà kiểm tra, traders và những người sửa tài liệu cũng được coi trọng như những người viết tổng thể.

:::tip Hướng dẫn chính thức sống trong repo
Trang này là đà nhẹ. Quá trình đầy đủ và luôn cập nhật — các quy tắc cơ bản, quy ước mã hóa, luồng xem xét — là trong **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Đóng góp đầu tiên của bạn trong ~10 phút

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings, or CI will politely refuse you
dotnet test           # unit + integration + E2E
```

Tìm thấy cái gì để sửa? Nhánh nó, thay đổi nó, thêm một bài kiểm tra và mở một PR. Đó là toàn bộ vòng lặp.

## Cách giúp đỡ (không phải tất cả đều là mã)

| Đóng góp | Nỗ lực | Nơi |
|---|---|---|
| 🐛 Báo cáo một lỗi có thể tái tạo | 10 phút | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Gợi ý một tính năng | 10 phút | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Cải thiện những tài liệu này | 15 phút | Chỉnh sửa dưới `website/docs/` và PR |
| 🧪 Thêm một bài kiểm tra bị thiếu | 30 phút | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Báo cáo hành vi cTrader chính xác | 10 phút | [Open a Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Các quy tắc nhà (phiên bản ngắn)

cMind di chuyển **tiền thực**, vì vậy một vài điều là không thể chỉnh sửa được — và thành thật mà nói, chúng làm cho codebase
một niềm vui để làm việc trong:

- **Thiết kế Hướng Miền Nghiêm Ngặt.** Logic kinh doanh sống trên các tổng thể và các đối tượng giá trị, không bao giờ trong
  các điểm cuối hoặc UI. (Có một sách hướng dẫn thân thiện cho nó trong repo.)
- **Ba lớp thử nghiệm, mỗi thay đổi.** Unit + tích hợp + E2E, *bao gồm* các đường thất bại (bỏ rơi
  kết nối, các lệnh bị từ chối, các nút chết). Các bài kiểm tra xanh là giá của việc nhập học.
- **Không có cảnh báo.** `TreatWarningsAsErrors=true`. Thành ngữ C# 14 hiện đại.
- **Không có bí mật, không có chuỗi ma thuật, không bao giờ `DateTime.UtcNow`** (tiêm `TimeProvider` thay thế).
- **Tài liệu trong cùng một commit.** Hành vi thay đổi → cập nhật tài liệu của nó. Có, điều đó bao gồm trang web này.

Chi tiết đầy đủ, với *tại sao* đằng sau mỗi quy tắc, trong
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) và
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Đóng góp với AI 🤖

Chúng tôi thực sự chào đón **các PR được hỗ trợ bởi AI** — dự án này được xây dựng để được làm việc bởi các đại lý cũng như
con người. Nếu bạn đang điều khiển Claude, Copilot hoặc tương tự: chỉ nó tới
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), để nó đọc các tệp `CLAUDE.md` lồng nhau
và giữ nó với cùng một thanh (kiểm tra, không cảnh báo, DDD). Một PR AI tốt là
không thể phân biệt được với một PR tốt của con người — cùng một bài đánh giá, cùng một chào đón.

## Hãy tuyệt vời với nhau

Chúng tôi có một [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md).
Bản tóm tắt: hãy tốt bụng, cho rằng lòng tin tốt, và hãy nhớ có một người (hoặc đại lý của một người) trên
một đầu khác. Hỏi các câu hỏi sớm — đó là một sức mạnh, không phải một bother.

Chào mừng lên tàu. Chúng tôi không thể chờ đợi để thấy những gì bạn xây dựng. 🎉
