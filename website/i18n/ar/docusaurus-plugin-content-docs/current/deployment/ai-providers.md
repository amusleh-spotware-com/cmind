---
description: "كتالوج الإعداد لكل موفر AI يدعمه cMind - Anthropic و OpenAI و Azure OpenAI و Google Gemini وكل OpenAI-compatible endpoint بما في ذلك النماذج المحلية (Ollama و LM Studio و vLLM و llama.cpp و LocalAI) و clouds متوافقة OpenAI."
---

# موفري AI — كتالوج الإعداد

طبقة AI cMind هي agnostic الموفر (انظر [ميزات AI](../features/ai.md)). تكوين موفر بطريقتين:

1. **UI (المالك):** الإعدادات → AI → **إضافة موفر** → اختر kind و base URL و model و key (اختياري للمحلي) و capability toggles و **تعيين نشط** → **اختبر الاتصال**.
2. **الإعدادات/env (ops):** بذر `App:Ai:Providers[]` و `App:Ai:ActiveProvider` - استيراد إلى المتجر عند بدء التشغيل الأول عندما لا توجد بيانات اعتماد. مثال (env و مؤشر موفر `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (حذف للنقاط النهائية المحلية بدون مفتاح)
   ```

بالضبط موفر واحد نشط في وقت واحد. المفاتيح مخزنة مشفرة؛ نقطة نهاية محلية لا تحتاج إلى أي.

## الأمان: http مقابل https

Plaintext `http://` يقبل **فقط** لأرضية / خاص (intranet) hosts - الحالة المحلية LLM (Ollama و LM Studio و vLLM و على-prem box). أي مضيف قابل للمسار على الإنترنت الكبير **يجب** أن يكون `https://` لذا مفتاح API لا يتم إرسال أبداً في الواضح. Air-gapped/on-prem: اشير base URL في نقطة نهايتك الداخلية (loopback أو private IP) واترك المفتاح فارغاً إذا كان runtime unauthenticated.

## Built-in local AI (ONNX، مُرسل)

cMind يشحن **real in-process local LLM** (Microsoft.ML.OnnxRuntimeGenAI) وهو **مفعل بشكل افتراضي** - لا مفتاح و لا خدمة خارجية. عند بدء التشغيل الأول عندما لا يتم تكوين موفر وـ `App:Branding:AllowBuiltInAi` هي `true` يتم بذره وتفعيل تلقائياً.

- **الإعدادات:** `App:Ai:BuiltIn:Enabled` (default `true`) و `App:Ai:BuiltIn:ModelPath` (default `models/onnx` نسبي لدليل قاعدة التطبيق) و `App:Ai:BuiltIn:MaxTokens` (default `1024`).
- **ملفات النموذج:** اشير `ModelPath` في دليل يحتوي على نموذج ONNX GenAI - `genai_config.json` و tokenizer و `.onnx` weights. CPU **Phi-3-mini** بناء يعمل بشكل جيد مثل:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # ثم تعيين App:Ai:BuiltIn:ModelPath إلى هذا المجلد (يحتوي على genai_config.json)
  ```
