---
title: قم بتشغيله محليًا
description: احصل على cMind يعمل على جهازك الخاص في بضع دقائق مع Docker Compose (أو .NET Aspire للتطوير).
sidebar_position: 1
---

# قم بتشغيل cMind محليًا

هذه هي الطريقة الأسرع لرؤية cMind بالفعل - حالة كاملة على جهازك الخاص. احصل على القهوة؛ من المحتمل أنك ستكون قد دخلت قبل أن تبرد.

:::tip ما ستحصل عليه في النهاية
تطبيق ويب قيد التشغيل على **localhost:8080**، خادم MCP على **localhost:8081**، قاعدة بيانات Postgres، وعقدة عامل محلية جاهزة للبناء والاختبار cBots. كل شيء على جهازك، كل شيء لك.
:::

**قبل البدء، تحتاج إلى واحد من:**

- **Docker فقط** → استخدم الخيار A (لا يتطلب .NET SDK). موصى به للنظرة الأولى.
- **.NET 10 SDK + Docker** → استخدم الخيار B إذا كنت تريد العبث بالكود.

كلا المسارين متعددين الأنظمة (Windows / macOS / Linux).

## الخيار A - Docker Compose (لا يتطلب .NET SDK)

المتطلب الأساسي: Docker Desktop (أو Docker Engine + compose plugin).

```bash
cp .env.example .env        # تحرير PG_PASSWORD، OWNER_EMAIL، OWNER_PASSWORD
docker compose up --build
```

- واجهة الويب: <http://localhost:8080> (تسجيل الدخول بصاحب من `.env`؛ مجبر على تغيير كلمة المرور عند أول تسجيل دخول).
- خادم MCP: <http://localhost:8081/mcp>.
- بيانات Postgres التي تستمر في حجم `pgdata`؛ مخطط الهجرة تلقائي عند بدء التشغيل.

حاوية الويب تحمل مقبس Docker المضيف (`/var/run/docker.sock`) حتى المنشئ في المتصفح والعقدة المحلية **المسلح** تبني + تشغيل حاويات cTrader Console على جهازك.

**ملاحظات متعددة الأنظمة**
- Docker Desktop (Windows/macOS) يعرض المقبس على `/var/run/docker.sock` — mount compose يعمل كما هو.
- Linux: تأكد من أن المستخدم الخاص بك يمكنه الوصول إلى المقبس، أو قم بتشغيل compose بامتيازات كافية.
- صورة الويب هي `linux/amd64`؛ على Apple Silicon Docker يقوم بتشغيلها تحت محاكاة.

توقف وامسح:

```bash
docker compose down          # الاحتفاظ بالبيانات
docker compose down -v       # أيضًا حذف حجم قاعدة البيانات
```

## الخيار B - .NET Aspire (للتطوير)

المتطلب الأساسي: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire ينسق Postgres والويب و MCP و pgAdmin؛ أسلاك سلاسل الاتصال + OTLP؛ يفتح لوحة التحكم. تعيين بيانات اعتماد المالك كمعاملات Aspire (`OwnerEmail`، `OwnerPassword`).

تشغيل تطبيق الويب فقط ضد Postgres الموجود:

```bash
dotnet run --project src/Web
```

## إضافة عقد عاملة محليًا

عقدة LocalNode المحلية بالفعل تشغيل العمل على جهازك. لممارسة **الكشف التلقائي** محليًا، ابدأ وكيل عقدة يشير إلى تطبيق الويب (انظر [اكتشاف العقد](../operations/node-discovery.md)) مع `NodeAgent:MainUrl=http://host.docker.internal:8080` وتطابق `JoinToken`.

## استكشاف الأخطاء

Docker له آراء. إليك المريبين المعتادين:

| الأعراض | السبب المحتمل والإصلاح |
|---|---|
| `port is already allocated` على 8080/8081 | شيء آخر يستخدم المنفذ. توقفه، أو غير الخريطة في `docker-compose.yml`. |
| يبدأ الويب لكن البناء/الاختبار يفشل | لم يتم تحميل أو الوصول إلى مقبس Docker. على Linux، تأكد من أن المستخدم الخاص بك يمكنه الوصول إلى `/var/run/docker.sock`. |
| `permission denied` على المقبس (Linux) | أضف المستخدم الخاص بك إلى مجموعة `docker` (`sudo usermod -aG docker $USER`) وأعد تسجيل الدخول، أو قم بتشغيل بامتيازات كافية. |
| التشغيل الأول بطيء جداً | البناء الأول يسحب الصور والترجمات — التشغيلات اللاحقة أسرع بكثير. على Apple Silicon صورة الويب `linux/amd64` تعمل تحت المحاكاة. |
| لا يمكن تسجيل الدخول | تحقق من `OWNER_EMAIL` / `OWNER_PASSWORD` في `.env`. أول تسجيل دخول يجبر على تغيير كلمة المرور. |
| غرابة قاعدة البيانات بعد الترقيات | `docker compose down -v` يمسح الحجم لحالة نظيفة (ستفقد البيانات المحلية). |

لا تزال عالقاً؟ [فتح نقاش](https://github.com/amusleh-spotware-com/cmind/discussions) - نحن ودي. محطة التالية: [نشر حقيقي →](./cloud.md)
