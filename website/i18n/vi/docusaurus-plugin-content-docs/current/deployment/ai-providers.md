---
description: "Danh mục cài đặt cho mọi nhà cung cấp AI mà cMind hỗ trợ — Anthropic, OpenAI, Azure OpenAI, Google Gemini, và mọi điểm cuối tương thích OpenAI bao gồm các mô hình cục bộ (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) và các đám mây tương thích OpenAI."
---

# Các nhà cung cấp AI — danh mục cài đặt

Lớp AI của cMind là không phụ thuộc vào nhà cung cấp (xem [Các tính năng AI](../features/ai.md)). Cấu hình một nhà cung cấp theo hai cách:

1. **Giao diện người dùng (chủ sở hữu):** Cài đặt → AI → **Thêm nhà cung cấp** → chọn loại, URL cơ bản, mô hình, khóa (tùy chọn cho cục bộ), bật/tắt khả năng, **Đặt hoạt động** → **Kiểm tra kết nối**.
2. **Cấu hình/env (ops):** ghi `App:Ai:Providers[]` và `App:Ai:ActiveProvider` — được nhập vào cửa hàng khi khởi động lần đầu tiên khi không có thông tin xác thực. Ví dụ (env, chỉ mục nhà cung cấp `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (bỏ qua đối với các điểm cuối cục bộ không cần khóa)
   ```

Chính xác một nhà cung cấp hoạt động cùng một lúc. Các khóa được lưu trữ được mã hóa; một điểm cuối cục bộ không cần khóa.

## Bảo mật: http vs https

Văn bản thuần `http://` được chấp nhận **chỉ** cho loopback / riêng (intranet) máy chủ — trường hợp LLM cục bộ (Ollama, LM Studio, vLLM, một hộp on-prem). Bất kỳ máy chủ nào có thể định tuyến trên internet công cộng **phải** là `https://`, vì vậy khóa API không bao giờ được gửi dưới dạng rõ ràng. Air-gapped/on-prem: trỏ URL cơ bản vào điểm cuối nội bộ của bạn (loopback hoặc IP riêng) và để trống khóa nếu thời gian chạy không được xác thực.

## AI cục bộ tích hợp sẵn (ONNX, được gửi)

cMind gửi một **LLM cục bộ trong quy trình thực (Microsoft.ML.OnnxRuntimeGenAI)** được **bật theo mặc định** — không khóa, không có dịch vụ bên ngoài. Khi khởi động lần đầu tiên, khi không có nhà cung cấp nào được cấu hình và `App:Branding:AllowBuiltInAi` là `true`, nó được ghi và kích hoạt tự động.

- **Cấu hình:** `App:Ai:BuiltIn:Enabled` (mặc định `true`), `App:Ai:BuiltIn:ModelPath` (mặc định `models/onnx`, tương đối với thư mục cơ sở ứng dụng), `App:Ai:BuiltIn:MaxTokens` (mặc định `1024`).
- **Tệp mô hình:** trỏ `ModelPath` vào một thư mục chứa mô hình ONNX GenAI — `genai_config.json`, trình mã hóa và trọng số `.onnx`. Bản dựng **Phi-3-mini** CPU hoạt động tốt, ví dụ:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-128k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # sau đó đặt App:Ai:BuiltIn:ModelPath thành thư mục đó (chứa genai_config.json)
  ```

  Đóng gói thư mục với hình ảnh triển khai / khối lượng Helm của bạn hoặc gắn nó khi chạy. Khi các tệp không có, hệ thống tích hợp sẽ giảm xuống thành thông báo rõ ràng "mô hình chưa được cài đặt" — ứng dụng vẫn chạy; cấu hình nhà cung cấp khác hoặc cài đặt mô hình.
- **GPU:** hoán đổi gói CPU/mô hình cho bản dựng CUDA/DirectML ONNX GenAI; đường dẫn mã không thay đổi.

## White-label: giới hạn AI

Đặt dưới `App:Branding` (được thực thi phía máy chủ — upsert bị cấm trả về `400`):

- `AllowBuiltInAi: false` — xóa mô hình tích hợp sẵn được gửi hoàn toàn.
- `AllowLocalProviders: false` — cấm các điểm cuối cục bộ/tự lưu trữ (Ollama/LM Studio/vLLM và bất kỳ URL OpenAI-compatible loopback/private nào).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — chỉ cho phép các loại này (rỗng = tất cả).

## Mở rộng với các mô hình tích hợp sẵn trong tương lai

Lớp nhà cung cấp dựa trên bộ điều hợp (`IAiProvider` được khóa bởi `AiProviderKind`), vì vậy một thời gian chạy mô hình tích hợp sẵn trong tương lai được thêm vào mà không cần chạm vào bất kỳ tính năng AI nào: thêm một loại, triển khai một bộ điều hợp, đăng ký nó. ONNX tích hợp sẵn là triển khai tham chiếu. Xem [Các tính năng AI → Mở rộng](../features/ai.md#extending-future-built-in-models).

## Các nhà cung cấp đám mây

### Anthropic (Claude)

- Khóa: <https://console.anthropic.com/> → API keys.
- URL cơ bản: `https://api.anthropic.com/` · Mô hình: ví dụ `claude-opus-4-8`.
- Khả năng: tìm kiếm web + thị lực bật theo mặc định.

### OpenAI

- Khóa: <https://platform.openai.com/api-keys>.
- URL cơ bản: `https://api.openai.com/v1/` · Mô hình: ví dụ `gpt-4o`.
- Loại: **OpenAiCompatible**. Bật thị lực trong hộp thoại nếu sử dụng mô hình thị lực.

### Azure OpenAI

- Khóa + điểm cuối: Cổng thông tin Azure → tài nguyên Azure OpenAI của bạn.
- URL cơ bản: `https://<resource>.openai.azure.com/` · Mô hình: **tên triển khai** của bạn.
- Loại: **AzureOpenAi** (sử dụng tiêu đề `api-key` + `api-version` truy vấn và đường dẫn triển khai).

### Google Gemini

- Khóa: <https://aistudio.google.com/app/apikey>.
- URL cơ bản: `https://generativelanguage.googleapis.com/` · Mô hình: ví dụ `gemini-2.0-flash`.
- Loại: **Gemini**. Sự vận động của tìm kiếm web + thị lực bật theo mặc định.

### Các đám mây OpenAI-compatible khác (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Loại: **OpenAiCompatible**. URL cơ bản = điểm cuối OpenAI-compatible của nhà cung cấp, Mô hình = id mô hình của nó, ApiKey = khóa nhà cung cấp. Không cần thay đổi cMind — một bộ điều hợp phục vụ cho tất cả.

## Các mô hình cục bộ (không cần khóa)

Tất cả các thời gian chạy cục bộ hiển thị dây OpenAI Chat Completions, vì vậy hãy sử dụng **Kind: OpenAiCompatible** với URL cơ bản của thời gian chạy và tên mô hình được phục vụ; để trống khóa.

### Ollama

```
# cài đặt từ https://ollama.com, sau đó:
ollama pull llama3.1:8b
```

- URL cơ bản: `http://localhost:11434/v1/` · Mô hình: tên được kéo (ví dụ `llama3.1:8b`, `qwen2.5-coder`).
- Không có khóa API. Khả năng mặc định chỉ là văn bản; chỉ bật thị lực cho mô hình thị lực.

### LM Studio

- Bắt đầu máy chủ cục bộ (Nhà phát triển → Bắt đầu máy chủ).
- URL cơ bản: `http://localhost:1234/v1/` · Mô hình: id mô hình được tải. Không có khóa API.

### vLLM / llama.cpp `server` / LocalAI

- Phục vụ một điểm cuối OpenAI-compatible (mỗi cái vận chuyển một).
- URL cơ bản: URL được phục vụ của bạn (ví dụ `http://localhost:8000/v1/`) · Mô hình: tên mô hình được phục vụ. Không có khóa trừ khi bạn đặt xác thực ở phía trước.

## Xác minh

- **Kiểm tra kết nối** trong hộp thoại chạy một hoàn thành ping nhỏ và báo cáo thành công + độ trễ — lý tưởng để xác nhận một điểm cuối cục bộ.
- Tự động: bộ E2E của ứng dụng lái mọi tính năng AI so với máy chủ OpenAI-compatible giả trong quy trình theo mặc định, hoặc nhà cung cấp thực của bạn khi `AI_E2E_BASEURL` (+ `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL` tùy chọn) được đặt. Xem [Các tính năng AI → Kiểm tra](../features/ai.md#testing-with-the-fake-local-llm).

## Chuyển / Xoay vòng

- **Chuyển nhà cung cấp hoạt động:** Cài đặt → AI → **Đặt hoạt động** trên thẻ khác (kích hoạt một thẻ sẽ vô hiệu hóa phần còn lại).
- **Xoay vòng khóa:** chỉnh sửa nhà cung cấp và cung cấp khóa mới (để trống để giữ khóa được lưu trữ).
- **Xóa:** xóa thẻ. Không có nhà cung cấp hoạt động, các tính năng AI sẽ bị vô hiệu hóa và phần còn lại của ứng dụng chạy không thay đổi.
