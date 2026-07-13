---
description: "cMind AI là không phụ thuộc vào nhà cung cấp — Anthropic, OpenAI, Azure OpenAI, Google Gemini, và bất kỳ điểm cuối tương thích OpenAI nào bao gồm các mô hình cục bộ (Ollama, LM Studio, vLLM). Chọn một nhà cung cấp, mô hình và điểm cuối; mọi tính năng AI hoạt động không thay đổi."
---

# Các tính năng AI

Lớp AI của cMind là **không phụ thuộc vào nhà cung cấp**. Mọi tính năng đều nói chuyện với một đường xuyên không phụ thuộc vào nhà cung cấp duy nhất (`IAiClient.CompleteAsync`); một **khách hàng định tuyến** giải quyết thông tin xác thực nhà cung cấp hoạt động và gửi đi đến bộ điều hợp dây phù hợp. Bạn chọn một nhà cung cấp + mô hình + điểm cuối (và nếu nhà cung cấp cần nó, một khóa); mọi tính năng hiện có hoạt động không thay đổi với cùng một gating, mã hóa, khả năng phục hồi và suy thoái.

**Pin bao gồm:** một **LLM cục bộ tích hợp sẵn được gửi với ứng dụng và bật theo mặc định** (Microsoft.ML.OnnxRuntimeGenAI, ví dụ Phi-3-mini) — vì vậy mọi triển khai đều có hoạt động AI **không có khóa API và không có dịch vụ bên ngoài**. Triển khai white-label có thể xóa nó và hạn chế những nhà cung cấp nào mà người dùng có thể thêm. Ngoài bản tích hợp sẵn, hãy kết nối bất kỳ nhà cung cấp bên ngoài nào.

Các nhà cung cấp được hỗ trợ:

- **AI cục bộ tích hợp sẵn** (`BuiltInOnnx`) — mô hình ONNX GenAI trong quy trình, không khóa, được gửi + bật theo mặc định.
- **Anthropic** (Claude — Messages API)
- **OpenAI** và **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Bất kỳ điểm cuối tương thích OpenAI nào**, bao gồm **các mô hình cục bộ** (Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) và các đám mây tương thích OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek) — tất cả thông qua bộ điều hợp tương thích OpenAI, khác biệt chỉ bằng URL cơ bản + mô hình + khóa.

Chính xác **một** nhà cung cấp hoạt động cùng một lúc. Thông tin xác thực được lưu trữ **được mã hóa** (`AiProviderCredential` tổng hợp + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); một điểm cuối cục bộ cần **không khóa**. Với **không** nhà cung cấp hoạt động, mọi tính năng trả về kết quả bị vô hiệu hóa và phần còn lại của ứng dụng chạy không thay đổi (không cần khóa để xây dựng, kiểm tra hoặc chạy nền tảng).

**Tương thích ngược:** thông tin xác thực `App:Ai:ApiKey` cũ của triển khai hiện có (hoặc cài đặt `ai.api_key` được mã hóa cũ) được tôn trọng tự động như nhà cung cấp **Anthropic** hoạt động mặc định — không cần hành động.

AI không được cấu hình → các trang AI mờ các hành động và hiển thị một banner cộng với lời nhắc một lần để thêm nhà cung cấp trong **Cài đặt → AI** (`AiFeatureNotice`). Trạng thái tại `GET /api/ai/status` (`{ enabled, kind, model }`); các nhà cung cấp được quản lý (chỉ chủ sở hữu) thông qua `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}` và ping kết nối `POST /api/ai/providers/test`.

## Mặc định triển khai vs nhà cung cấp của chính người dùng

Thông tin xác thực AI có hai phạm vi:

- **Mặc định triển khai (được quản lý bởi chủ sở hữu).** Chủ sở hữu cấu hình một nhà cung cấp (hoặc gửi một thông qua `App:Ai:Providers[]` / `App:Ai:ApiKey` cũ). Nó trở thành **mặc định được chia sẻ cho mọi người dùng** — vì vậy nhà môi giới hoặc nhà cung cấp lưu trữ có thể tài trợ AI cho tất cả người dùng của họ với **không cần cài đặt cho mỗi người dùng và không giới hạn cho mỗi người dùng**. Được quản lý thông qua các tuyến `/api/ai/providers` chỉ dành cho chủ sở hữu ở trên.
- **Nhà cung cấp của chính người dùng (tự phục vụ).** Bất kỳ người dùng đã đăng nhập nào có thể thêm nhà cung cấp của họ dưới `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}`. Khi có mặt, **nhà cung cấp hoạt động của riêng họ ghi đè mặc định triển khai cho các tính năng AI của riêng họ**; xóa nó sẽ quay lại mặc định.

**Thứ tự phân giải** (trong `AiProviderStore`, cho mỗi người dùng yêu cầu): thông tin xác thực hoạt động của chính người dùng → mặc định triển khai → khóa cấu hình cũ → không (AI bị vô hiệu hóa). Chính xác một thông tin xác thực hoạt động **trên mỗi phạm vi** (một chỉ mục duy nhất một phần cho mỗi `OwnerUserId`), và mỗi phạm vi được giải quyết độc lập, vì vậy một người dùng kích hoạt khóa của họ sẽ không bao giờ làm xáo trộn mặc định được chia sẻ. Bối cảnh nền/không phải Web (không có người dùng yêu cầu) luôn giải quyết mặc định triển khai.

## Ma trận khả năng nhà cung cấp

Khả năng mặc định theo nhà cung cấp và được chủ sở hữu ghi đè có thể. Khi một khả năng được tắt, tính năng **suy thoái, không bao giờ ném**: tìm kiếm web được loại bỏ im lặng; thị lực trả về lỗi không được hỗ trợ khả năng được nhập.

| Nhà cung cấp | Loại | URL cơ bản mặc định | Khóa bắt buộc | Tìm kiếm web | Thị lực | Ghi chú |
|---|---|---|---|---|---|---|
| AI cục bộ tích hợp sẵn | `BuiltInOnnx` | n/a (trong quy trình) | không | ✖ | ✖ | mô hình ONNX GenAI được gửi, bật theo mặc định |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | có | ✅ | ✅ | Messages API, `web_search` công cụ |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | có | chọn tham gia | chọn tham gia | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | có | ✅ | ✅ | đường dẫn triển khai + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | có | ✅ | ✅ | `generateContent`, `google_search` nền tảng |
| Ollama (cục bộ) | `OpenAiCompatible` | `http://localhost:11434/v1/` | không | ✖ | phụ thuộc vào mô hình | thông qua bộ điều hợp tương thích OpenAI |
| LM Studio (cục bộ) | `OpenAiCompatible` | `http://localhost:1234/v1/` | không | phụ thuộc vào mô hình | phụ thuộc vào mô hình | thông qua bộ điều hợp tương thích OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | URL được phục vụ của bạn | không | ✖ | phụ thuộc vào mô hình | thông qua bộ điều hợp tương thích OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL nhà cung cấp | có | ✖ | phụ thuộc vào mô hình | thông qua bộ điều hợp tương thích OpenAI |

Hướng dẫn cài đặt đầy đủ cho mỗi nhà cung cấp (khóa, URL, id mô hình, các bước UI): xem [Các nhà cung cấp AI — danh mục cài đặt](../deployment/ai-providers.md).

## AI cục bộ tích hợp sẵn (được gửi, bật theo mặc định)

cMind gửi một **LLM cục bộ thực chạy trong quy trình** qua [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (một mô hình hướng dẫn compact như Phi-3-mini). Nó cần **không khóa API và không có dịch vụ bên ngoài**, và khi khởi động lần đầu tiên — khi không có nhà cung cấp nào được cấu hình và cổng white-label cho phép — nó được **ghi và kích hoạt tự động**, vì vậy mọi triển khai đều có hoạt động AI ngay từ hộp.

- Thư mục mô hình (`genai_config.json` + trình mã hóa + trọng số) được cấu hình bởi `App:Ai:BuiltIn:ModelPath` (mặc định `models/onnx`, tương đối với thư mục cơ sở ứng dụng). Khi các tệp mô hình không có nhà cung cấp **suy thoái thành lỗi được nhập với gợi ý cài đặt** — nó không bao giờ ném và phần còn lại của ứng dụng không bị ảnh hưởng.
- Nó cung cấp năng lượng cho mọi tính năng AI văn bản. Là một mô hình compact, nó chỉ là văn bản (không tìm kiếm web phía máy chủ hoặc thị lực) và việc tạo được nối tiếp (một phiên bản mô hình, được sử dụng lại sau khi tải lười).
- Lấy/đóng gói mô hình: xem [Các nhà cung cấp AI → tích hợp sẵn](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Điều khiển White-label

Triển khai white-label hạn chế AI thông qua `App:Branding` (được thực thi phía máy chủ trên mọi upsert nhà cung cấp):

- `AllowBuiltInAi` (mặc định `true`) — đặt `false` để **xóa mô hình tích hợp sẵn** hoàn toàn.
- `AllowLocalProviders` (mặc định `true`) — đặt `false` để cấm các điểm cuối cục bộ/tự lưu trữ (loopback / OpenAI-compatible riêng, ví dụ Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (mặc định rỗng = tất cả) — liệt kê chỉ các loại mà triển khai phê chuẩn (ví dụ `["Anthropic","OpenAiCompatible"]`) để khóa các nhà cung cấp nào mà người dùng có thể thêm.

## Mở rộng: các mô hình tích hợp sẵn trong tương lai

Lớp AI là **dựa trên bộ điều hợp và được xây dựng để phát triển**. Mỗi nhà cung cấp là `IAiProvider` được chọn bởi `AiProviderKind`; đường xuyên hướng tới tính năng (`IAiClient`/`AiFeatureService`) không bao giờ thay đổi. Thêm một thời gian chạy mô hình tích hợp sẵn mới sau này (một mô hình ONNX khác, một công cụ trong quy trình khác, GGUF/llama.cpp trong quy trình, v.v.) là một thay đổi được bản địa hóa: thêm `AiProviderKind`, triển khai một bộ điều hợp `IAiProvider`, đăng ký nó, và (tùy chọn) kết nối ghi mặc định + tùy chọn hộp thoại — không có tính năng, điểm cuối hoặc thay đổi công cụ MCP. Nhà cung cấp ONNX tích hợp sẵn là triển khai tham chiếu của mô hình này.

## Khả năng

- **Xây dựng cBot** — lời nhắc bằng tiếng Anh thuần túy → cBot có thể chạy được thông qua vòng **tạo → xây dựng → sửa chữa AI** tự sửa chữa (`build-strategy`), tại `/ai/build`.
- **Tối ưu hóa tham số** — vòng lặp đóng: AI đề xuất các bộ tham số, mỗi bộ được duy trì + kiểm tra lại trên các nút (`optimize-run` / `optimize-params`).
- **Tác nhân danh mục đầu tư tự chủ** — các đề xuất được trao quyền hạn với nhật ký quyết định đầy đủ (`AgentMandate` → `AgentProposal`).
- **Lính gác rủi ro hành động** — `AiRiskGuard` dịch vụ nền đánh giá bots chạy, có thể **tự động dừng** khi có rủi ro nghiêm trọng (chọn tham gia).
- **Vệ sĩ tiếp xúc công ty prop** — giới hạn rút vốn/tiếp xúc với auto-flatten.
- **Cảnh báo thị trường** — công cụ `AlertRule` với tâm điểm AI (tìm kiếm web neo trong trường hợp nhà cung cấp hỗ trợ nó).
- **Phân tích** — xem xét cBot, phân tích kiểm tra lại, bình tĩnh sau sự cố, tâm điểm thị trường, thiết kế tầm nhìn biểu đồ, quản lý thị trường.

## Bề mặt

- Điểm cuối Web dưới `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …).
- Công cụ MCP (`AiTools`) cho các khách hàng AI — xem [mcp.md](mcp.md). Lựa chọn nhà cung cấp trong suốt đối với khách hàng MCP.
- **AI** nhóm điều hướng — một trang Blazor **cho mỗi tính năng**: Xây dựng cBot (`/ai/build`), Xem xét (`/ai/review`), Tranh luận (`/ai/debate`), Tâm điểm Thị trường (`/ai/sentiment`), Kiểm tra Tiếp xúc (`/ai/exposure`), Tóm tắt Danh mục đầu tư (`/ai/digest`), Cố vấn Điều chỉnh (`/ai/tune`), Tối ưu hóa (`/ai/optimize`), cộng với Tác nhân Danh mục đầu tư, Cảnh báo, Phím MCP. Các trang chia sẻ `AiFeaturePageBase` + `AiOutputPanel`; mỗi trang hiển thị `AiFeatureNotice` khi không có nhà cung cấp được cấu hình.
- **Cài đặt → AI** (`/settings/ai`, chỉ chủ sở hữu) — danh sách nhà cung cấp với **hộp thoại Thêm / chỉnh sửa nhà cung cấp** (loại, URL cơ bản với mẹo cụ thể theo loại bao gồm Ollama/LM Studio localhost preset, mô hình, khóa tùy chọn, bật tắt khả năng, "đặt hoạt động") và nút **Kiểm tra kết nối**.

## Cấu hình

`App:Ai` hỗ trợ cả khóa duy nhất cũ và ghi chép nhiều nhà cung cấp:

- Cũ: `ApiKey`, `Model` (mặc định `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — vẫn được tôn trọng như nhà cung cấp Anthropic mặc định.
- Nhiều nhà cung cấp: `ActiveProvider` (loại) và `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — được nhập vào cửa hàng khi khởi động nếu chưa có thông tin xác thực, vì vậy một đội ops có thể gửi một triển khai được cấu hình (bao gồm cục bộ-LLM) hoàn toàn thông qua appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` không thay đổi. Đối với các bài kiểm tra/phát triển, khóa cấu hình sống trong [tệp thông tin xác thực phát triển thống nhất](../testing/dev-credentials.md) dưới `Ai`.

## Độ tin cậy

Nhà cung cấp được coi là không đáng tin cậy — không có gì mà nó có thể làm để hạ gục ứng dụng. Điều này giữ nguyên hệt như vậy đối với các điểm cuối đám mây và cục bộ (Ollama chết thử lại rồi suy thoái giống hệt như Anthropic bị điều chỉnh):

- **Suy thoái nhuyễn mục.** Mọi chế độ lỗi (không có nhà cung cấp, HTTP 4xx/5xx/429, thời gian chờ, cơ thể biến dạng, nội dung trống, khả năng không được hỗ trợ) trả về `AiResult.Fail(reason)` được nhập — khách hàng không bao giờ ném vào một trang, công cụ MCP hoặc dịch vụ được lưu trữ.
- **Đường ống khả năng phục hồi.** `AddAiHttpClient` mang `HttpClient` AI được chia sẻ duy nhất với thử lại được giới hạn trên 5xx tạm thời / lỗi mạng (backoff theo cấp số nhân + rơi vào) cộng với hết thời gian chờ rộng rãi cho mỗi lần thử và tổng cộng (`AiHttp`), được sử dụng lại bởi mọi bộ điều hợp.

## Kiểm tra với LLM cục bộ giả

Lớp AI được chứng minh từ đầu đến cuối **mà không có bất kỳ phụ thuộc bên ngoài nào** bởi `FakeLocalLlmServer` — một điểm cuối **tương thích OpenAI** nhỏ trong quy trình trả về một câu trả lời được ghi sẵn xác định, giống hệt như Ollama/LM Studio/vLLM. Nó hỗ trợ:

- **Đơn vị** — trên mỗi bộ điều hợp dịch yêu cầu + kiểm tra phân tích phản hồi, định tuyến/suy thoái khả năng.
- **Tích hợp** — bộ điều hợp OpenAI-compatible từ đầu đến cuối, lý thuyết khả năng phục hồi được tham số hóa trên mọi bộ điều hợp, và **công cụ AI MCP**.
- **E2E** — `AiLocalFixture` khởi động ứng dụng trỏ đến máy chủ giả (hoặc nhà cung cấp **thực** khi nhà phát triển đặt `AI_E2E_BASEURL` (+ `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL` tùy chọn) — creds thực tế chiến thắng) và lái mọi tính năng AI qua giao diện người dùng thực. Thêm hoặc thay đổi bất kỳ tính năng AI nào **yêu cầu** bài kiểm tra E2E qua fixture này (xem lệnh kiểm tra kho). Làn đường chọn tham gia (`AI_LOCAL_LLM=1`) chạy một hoàn thành thực qua **Ollama** Testcontainer.
