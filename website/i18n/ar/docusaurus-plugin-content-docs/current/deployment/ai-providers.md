---
description: "كتالوج الإعداد لكل مزود AI تدعمه cMind — Anthropic و OpenAI و Azure OpenAI و Google Gemini وكل نقطة نهاية متوافقة مع OpenAI بما فيها النماذج المحلية (Ollama و LM Studio و vLLM و llama.cpp و LocalAI) وسحابات متوافقة مع OpenAI."
---

# مزودي AI — كتالوج الإعداد

طبقة AI في cMind محايدة من حيث المزود (انظر [ميزات AI](../features/ai.md)). قم بتكوين مزود بطريقتين:

1. **واجهة المستخدم (المالك):** الإعدادات → AI → **إضافة مزود** → اختر النوع وعنوان URL الأساسي والنموذج والمفتاح (اختياري
   محلي) وتبديلات القدرة و **ضبط النشط** → **اختبار الاتصال**.
2. **التكوين/البيئة (عمليات):** بذرة `App:Ai:Providers[]` و `App:Ai:ActiveProvider` — مستورد إلى المتجر
   عند بدء التشغيل الأول عندما لا تكون هناك بيانات اعتماد. مثال (env وفهرس المزود `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (حذف لنقاط نهاية محلية بدون مفتاح)
   ```

بالضبط مزود واحد نشط في كل مرة. يتم تخزين المفاتيح مشفرة؛ لا يحتاج نقطة النهاية المحلية إلى أي شيء.

## الأمان: http مقابل https

يتم قبول `http://` النص العادي **فقط** لأجهزة loopback / الخاصة (intranet) — حالة LLM المحلية
(Ollama و LM Studio و vLLM و مربع على الأرض). أي مضيف قابل للتوجيه على الإنترنت العام **يجب** أن يكون
`https://`، لذا لا يتم إرسال مفتاح API أبدًا في الواضحة. Air-gapped/on-prem: وجهة عنوان URL الأساسي في
نقطة النهاية الداخلية (loopback أو IP خاصة) واترك المفتاح فارغًا إذا كان runtime غير موثق.

## AI محلي مدمج (ONNX، مشحون)

يشحن cMind **نموذج LLM محلي حقيقي في العملية** (Microsoft.ML.OnnxRuntimeGenAI) الذي هو **ممكّن بواسطة
افتراضي** — لا مفتاح، لا خدمة خارجية. عند بدء التشغيل الأول عندما لا يتم تكوين أي مزود و
`App:Branding:AllowBuiltInAi` هو `true`، يتم البذر والتفعيل تلقائيًا.

- **التكوين:** `App:Ai:BuiltIn:Enabled` (الافتراضي `true`) و`App:Ai:BuiltIn:ModelPath` (الافتراضي
  `models/onnx`، نسبة إلى دليل قاعدة التطبيق)، `App:Ai:BuiltIn:MaxTokens` (الافتراضي `1024`).
- **ملفات النموذج:** وجهة `ModelPath` في دليل يحتوي على نموذج ONNX GenAI — `genai_config.json` و
  tokenizer و أوزان `.onnx`. بناء CPU **Phi-3-mini** يعمل بشكل جيد، على سبيل المثال:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # ثم قم بتعيين App:Ai:BuiltIn:ModelPath إلى هذا المجلد (يحتوي على genai_config.json)
  ```

  احزم المجلد مع صورة النشر الخاصة بك / مجلد Helm أو حمله في وقت التشغيل. عندما تكون الملفات
  غائبة المدمج يتدهور إلى "نموذج غير مثبت" واضح — يعمل التطبيق؛ قم بتكوين
  مزود آخر أو تثبيت النموذج.
- **GPU:** استبدل حزمة CPU/نموذج ببناء CUDA/DirectML ONNX GenAI؛ مسار الكود لم يتغير.

## Markup-label: تحديد AI

عيّن تحت `App:Branding` (يتم فرضها من جانب الخادم — فشل upsert محظور `400`):

- `AllowBuiltInAi: false` — إزالة النموذج المدمج المشحون بالكامل.
- `AllowLocalProviders: false` — حظر نقاط النهاية المحلية/المستضافة ذاتيًا (Ollama/LM Studio/vLLM وأي
  URL متوافق مع OpenAI loopback/private).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — السماح فقط بهذه الأنواع (فارغ = الكل).

## الإضافة مع نماذج مدمجة مستقبلية

طبقة المزود مبنية على محول (`IAiProvider` مفتاح بواسطة `AiProviderKind`)، لذلك مستقبل مدمج نموذج
runtime يتم إضافته دون لمس أي ميزة AI: أضف نوع، وينفذ محول واحد، سجله. ال
ONNX المدمج هو تنفيذ المرجع. انظر [ميزات AI → الإضافة](../features/ai.md#extending-future-built-in-models).

## موفري السحابة

### Anthropic (Claude)

- المفتاح: <https://console.anthropic.com/> → مفاتيح API.
- عنوان URL الأساسي: `https://api.anthropic.com/` · النموذج: على سبيل المثال `claude-opus-4-8`.
- القدرات: بحث الويب + الرؤية افتراضيًا.

### OpenAI

- المفتاح: <https://platform.openai.com/api-keys>.
- عنوان URL الأساسي: `https://api.openai.com/v1/` · النموذج: على سبيل المثال `gpt-4o`.
- النوع: **OpenAiCompatible**. تمكين الرؤية في الحوار إذا كان استخدام نموذج الرؤية.

### Azure OpenAI

- المفتاح + نقطة النهاية: مدخل Azure → مورد Azure OpenAI الخاص بك.
- عنوان URL الأساسي: `https://<resource>.openai.azure.com/` · النموذج: **اسم النشر** الخاص بك.
- النوع: **AzureOpenAi** (يستخدم رأس `api-key` + استعلام `api-version` ومسار النشر).

### Google Gemini

- المفتاح: <https://aistudio.google.com/app/apikey>.
- عنوان URL الأساسي: `https://generativelanguage.googleapis.com/` · النموذج: على سبيل المثال `gemini-2.0-flash`.
- النوع: **Gemini**. تأسيس بحث الويب + الرؤية بشكل افتراضي.

### سحابات أخرى متوافقة مع OpenAI (OpenRouter و Groq و Together و Mistral و DeepSeek)

- النوع: **OpenAiCompatible**. عنوان URL الأساسي = نقطة نهاية المزود المتوافقة مع OpenAI والنموذج = معرف النموذج الخاص به و
  ApiKey = مفتاح المزود. لا يوجد تغيير cMind مطلوب — محول واحد يخدمهم جميعًا.

## نماذج محلية (بدون مفتاح)

جميع أوقات التشغيل المحلية تعرض أسلاك OpenAI Chat Completions، لذا استخدم **النوع: OpenAiCompatible** مع
عنوان URL الأساسي للوقت التشغيل واسم النموذج المخدوم؛ ترك المفتاح فارغًا.

### Ollama

```
# التثبيت من https://ollama.com، ثم:
ollama pull llama3.1:8b
```

- عنوان URL الأساسي: `http://localhost:11434/v1/` · النموذج: الاسم المسحوب (على سبيل المثال `llama3.1:8b` و`qwen2.5-coder`).
- لا مفتاح API. القدرات الافتراضية إلى نص فقط؛ تمكين الرؤية فقط لنموذج الرؤية.

### LM Studio

- ابدأ الخادم المحلي (المطور → ابدأ الخادم).
- عنوان URL الأساسي: `http://localhost:1234/v1/` · النموذج: معرف النموذج المحمل. لا مفتاح API.

### vLLM / llama.cpp `server` / LocalAI

- خدمة نقطة نهاية متوافقة مع OpenAI (كل واحد شحنة واحد).
- عنوان URL الأساسي: عنوان URL المقدم الخاص بك (على سبيل المثال `http://localhost:8000/v1/`) · النموذج: اسم النموذج المقدم. لا مفتاح
  ما لم تضع المصادقة في الأمام.

## التحقق

- **اختبار الاتصال** في الحوار يشغل إكمال ping صغير ويقرر النجاح + الكمون — مثالي
  للتأكيد من نقطة نهاية محلية.
- آلي: يقود جناح E2E للتطبيق كل ميزة AI مقابل خادم OpenAI متوافق داخل العملية بشكل افتراضي،
  أو مزودك الحقيقي عندما يتم تعيين `AI_E2E_BASEURL` (+ اختياري `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`). انظر [ميزات AI → الاختبار](../features/ai.md#testing-with-the-fake-local-llm).

## التبديل / الدوران

- **التبديل المزود النشط:** الإعدادات → AI → **ضبط النشط** على بطاقة أخرى (تفعيل واحد يعطل
  الباقي).
- **تدوير مفتاح:** تحرير المزود وتوفير مفتاح جديد (ترك فارغ للاحتفاظ بالمخزن).
- **إزالة:** احذف البطاقة. بدون مزود نشط، ميزات AI معطلة والبقية من التطبيق تعمل
  دون تغيير.
