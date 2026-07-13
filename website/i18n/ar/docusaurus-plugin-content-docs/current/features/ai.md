---
description: "AI المتخصص لـ cMind - Anthropic و OpenAI و Azure OpenAI و Google Gemini وأي OpenAI-compatible endpoint بما في ذلك النماذج المحلية (Ollama و LM Studio و vLLM). اختر موفراً وموديل ونقطة نهاية؛ كل ميزة AI تعمل دون تغيير."
---

# ميزات AI

طبقة AI cMind هي **agnostic الموفر**. كل ميزة تتحدث إلى خيط محايد موفر واحد (`IAiClient.CompleteAsync`)؛ عميل **التوجيه** يحل بيانات اعتماد الموفر النشطة وينشر إلى محول الأسلاك المطابق. تختار موفراً + نموذج + نقطة نهاية (وإذا كان الموفر يحتاجه، مفتاح)؛ كل ميزة موجودة تعمل دون تغيير مع نفس البوابات والتشفير والمرونة والانحطاط.

**البطاريات المضمنة:** نموذج LLM محلي **مدمج يشحن مع التطبيق وتفعيل بشكل افتراضي** (Microsoft.ML.OnnxRuntimeGenAI، مثل Phi-3-mini) — حتى كل نشر لديه عمل AI **بدون مفتاح API وبدون خدمة خارجية**. يمكن لنشر white-label إزالته وتقييد الموفرين التي قد يضيفها المستخدمون. بما يتجاوز المدمج، توصيل أي موفر خارجي.

الموفرون المدعومون:

- **Built-in local AI** (`BuiltInOnnx`) — نموذج ONNX GenAI في العملية، بدون مفتاح، مُرسل + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** و **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **أي OpenAI-compatible endpoint**، بما في ذلك **النماذج المحلية** (Ollama و LM Studio و vLLM و llama.cpp `server` و LocalAI) و clouds متوافقة OpenAI (OpenRouter و Groq و Together و Mistral و DeepSeek) — كل عبر محول OpenAI-compatible الواحد، يختلف فقط بـ base URL + model + key.

بالضبط **واحد** الموفر نشط في وقت واحد. بيانات الاعتماد مخزنة **مشفرة** (`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`، `EncryptionPurposes.AiApiKey`)؛ نقطة نهاية محلية تحتاج **لا مفتاح**. مع **لا** الموفر النشط، كل ميزة تعيد النتيجة المعطلة وبقية التطبيق يعمل دون تغيير (لا مفتاح مطلوب للبناء أو الاختبار أو تشغيل المنصة).

**Back-compat:** وجود نشر وراثي legacy `App:Ai:ApiKey` (أو الإعداد المشفر القديم `ai.api_key`) محترم تلقائياً كـ موفر **Anthropic** نشط بشكل افتراضي — لا إجراء مطلوب.

لم يتم تكوين AI → صفحات AI خافتة الإجراءات وتظهر لافتة إضافة موفر في الإخطار **الإعدادات → AI** (`AiFeatureNotice`). الحالة على `GET /api/ai/status` (`{ enabled, kind, model }`); الموفرون مُدارون (مالك فقط) عبر `GET/PUT /api/ai/providers`، `POST /api/ai/providers/{id}/activate`، `DELETE /api/ai/providers/{id}`، وـ `POST /api/ai/providers/test` اتصال ping.

## الإعدادات الافتراضية للنشر مقابل موفر المستخدم الخاص

بيانات اعتماد AI لديها نطاقات اثنين:

- **الإعدادات الافتراضية للنشر (يُدير المالك).** يقوم المالك بتكوين موفر (أو سفينة واحدة عبر `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). يصبح **الموفر الافتراضي المشترك لكل مستخدم** - حتى يمكن لوسيط أو موفر استضافة تمويل AI لجميع مستخدميهم **بدون إعداد كل مستخدم وبدون حد كل مستخدم**. مُدار عبر routes `/api/ai/providers` المملوكة فقط أعلاه.
- **موفر المستخدم الخاص (الخدمة الذاتية).** قد يضيف أي مستخدم موقع عليه موفره الخاص تحت `GET/PUT /api/ai/my-providers`، `POST /api/ai/my-providers/{id}/activate`، `DELETE /api/ai/my-providers/{id}`. عند الحضور، **الموفر النشط الخاص بهم يتجاوز الإعدادات الافتراضية للنشر بالنسبة لميزات AI الخاصة بهم**؛ إزالته تتراجع عن الإعدادات الافتراضية.

**ترتيب الدقة** (في `AiProviderStore`، لكل طلب مستخدم): بيانات اعتماد المستخدم النشط الخاص → الإعدادات الافتراضية للنشر → مفتاح الإعدادات الوراثي → لا أحد (AI معطل). بالضبط واحد بيانات اعتماد نشطة **لكل نطاق** (فهرس فريد جزئي لكل `OwnerUserId`)، وكل نطاق يتم الحل بشكل مستقل، لذا لا يزعج المستخدم الذي ينشط مفتاحهم الافتراضي المشترك أبداً. السياقات الخلفية/غير الويب (لا طلب مستخدم) دائماً حل الإعدادات الافتراضية للنشر.

## مصفوفة قدرات الموفر

الإمكانيات الافتراضية لكل موفر وقابلة للتجاوز من قبل المالك. عندما تكون القدرة قبالة الميزة **يتحلل، لا تطرح أبداً**: البحث على الويب ينخفض ​​بصمت؛ تصور عودات نوع فشل عدم دعم القدرة.

| الموفر | نوع | عنوان URL الافتراضي الأساسي | مفتاح مطلوب | البحث على الويب | الرؤية | ملاحظات |
|---|---|---|---|---|---|---|
| Built-in local AI | `BuiltInOnnx` | لا (في العملية) | لا | ✖ | ✖ | نموذج ONNX GenAI المشحون والافتراضي |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | نعم | ✅ | ✅ | Messages API و `web_search` tool |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | نعم | اختياري | اختياري | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | نعم | ✅ | ✅ | مسار النشر + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | نعم | ✅ | ✅ | `generateContent` و `google_search` grounding |
| Ollama (محلي) | `OpenAiCompatible` | `http://localhost:11434/v1/` | لا | ✖ | يعتمد على النموذج | عبر محول OpenAI-compatible |
| LM Studio (محلي) | `OpenAiCompatible` | `http://localhost:1234/v1/` | لا | يعتمد على النموذج | يعتمد على النموذج | عبر محول OpenAI-compatible |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | عنوان URL المخدوم | لا | ✖ | يعتمد على النموذج | عبر محول OpenAI-compatible |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL الموفر | نعم | ✖ | يعتمد على النموذج | عبر محول OpenAI-compatible |

أدلة إعداد لكل موفر كاملة (المفاتيح والعناوين والمعرفات النموذجية وخطوات UI): انظر [موفري AI — كتالوج الإعداد](../deployment/ai-providers.md).
