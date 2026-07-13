---
description: "cMind يتسع مع الحد الأدنى من جهد المشغل. مهمتين stateful — run/backtest execution و copy-trading — كلاهما يستخدم قاعدة البيانات كنقطة تنسيق لذا ..."
---

# التوسع الأفقي

cMind يتسع مع الحد الأدنى من جهد المشغل. مهمتين stateful — run/backtest
execution و copy-trading — كلاهما يستخدم قاعدة البيانات كنقطة تنسيق لذا إضافة replicas
بدون منسق خارجي (بدون ZooKeeper وبدون انتخاب الزعيم).

## النسخ التجاري (عقد الشفاء الذاتي)

تشغيل كل عقدة `CopyEngineSupervisor` (مسورة على `App:Copy:Enabled`). كل دورة المصالحة و
المشرف:

1. **الدعاوى** كل ملف تعريف تشغيل غير معين *أو* lease-lapsed في واحد ذري `UPDATE` —
   لا يدعي اثنان من الإشراف على نفس الملف الشخصي لذلك نسخ الملف الشخصي بالضبط
   عقدة واحدة (بدون أوامر مزدوجة).
2. **تجديد** عقد على ملفات تعريف يستضيفها.
3. ملفات تعريف معينة الاستضافة وتقديم rotations رمز الوصول إلى مضيف التشغيل في المكان (بدون
   event-stream drop).

كنقطة تعطل → توقفات التجديد؛ مرة واحدة `App:Copy:LeaseTtl` يمر أي عقدة ناجية تطالب
ملفات تعريفها الدورة التالية وإعادة بناء الحالة من المصالحة دون تكرار الصفقات. **التوسع
من** = إضافة replicas؛ ملفات تعريف غير معين/مجاني التقطتها تلقائيًا.

**Graceful scale-in / rolling update (S1)** = في `SIGTERM`، `CopyEngineSupervisor.StopAsync`
**يطلق عقد leases هذا العقدة** (`AssignedNode`/`LeaseExpiresAt` → null) لذلك الناجي يطالب بهم
**ليس** بعد كامل `LeaseTtl`. فقط كنقطة تعطل صعبة تنتظر TTL. نسخة-عامل `terminationGracePeriodSeconds` (الافتراضي 30) يعطي وقت الإفراج لإنهاء قبل
pod قتل.

### المقابض (`App:Copy`)

| الإعداد | الافتراضي | ملاحظات |
|---------|---------|-------|
| `Enabled` | `false` | تشغيل استضافة النسخ على العقدة. |
| `ReconcileInterval` | `30s` | كم مرة عقدة المطالبات/تجديد/المصالحة. |
| `LeaseTtl` | `120s` | Grace قبل ملفات تعريف عقدة صامتة المطالب بها. إبقاء بعض دورات المصالحة لذا لا تسبب دورة بطيئة التقسيم الزائف. |
| `NodeName` | اسم الجهاز | اضبط بشكل متميز عندما يشارك اثنان من الإشراف مضيف. |

في Kubernetes copy supervisors تشغيل كـ Deployment؛ اضبط `replicas` إلى parallelism المطلوب. كل
pod يحصل على مستقر `NodeName` (الافتراضي: اسم مضيف pod)، لذلك leases نسب لكل pod. قاعدة البيانات هي
مصدر الحقيقة الوحيد — بدون جلسات دبقة وبدون حالة per-pod للهجرة.

**التوزيع المتوازن (S4):** اضبط `App:Copy:MaxProfilesPerNode` > 0 للحد من عدد التشغيل
ملفات التعريف التي تستضيفها العقدة. كل مشرف ثم المطالبات **على معظم** المتبقية headroom الخاصة بها عبر ذري
`FOR UPDATE SKIP LOCKED` مطالبة محدودة، لذلك ملفات التعريف **spread** عبر replicas بدلاً من أول
المشرف الاستيلاء على الكل — بدون pod واحد حار / SPOF. تخطي-locked مطالبة يبقي "بالضبط عقدة واحدة
لكل ملف تعريف" ضمان (بدون double-hosting) حتى تحت المطالبات المتزامنة. `0` (الافتراضي) =
غير محدود (واحد يستضيف كل شيء وغير متغير).

**في النطاق (S7/S8):** كل pod jitters المصالحة بحوالي 20٪ من `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) لذا N replicas لا يطلقون المطالبة/تجديد `UPDATE`
في نفس الوقت (Postgres thundering-herd). عندما `copyAgent.replicas > 1` مخطط أيضا ينتشر
replicas عبر العقد (`topologySpreadConstraints`) ويضيف `PodDisruptionBudget` (`minAvailable: 1`)
لذا الصرف/التحديث لا يأخذ أبدًا نسخ السعة إلى الصفر.

## تشغيل/المحاكاة التنفيذ

`NodeScheduler` يختار الأقل تحميل عقدة مؤهلة احترام `MaxInstances`؛ وكلاء عقدة بعيدة
تسجيل ذاتي و heartbeat (`App:Discovery`)، `NodeHeartbeatMonitor` علامات عقدة التي لا يمكن الوصول إليها
عندما يتجاوز heartbeat `Discovery:HeartbeatTtl`. إضافة وكلاء عقدة لإضافة السعة التنفيذية؛
عامل ميت موجه حولها تلقائيًا.

## الهجرات على scale-out / rolling deploy

كل Web/MCP replica يشغل `OwnerSeeder` عند بدء التشغيل والذي ينطبق EF migrations والبذور المالك.
لجعل هذا آمنًا عندما N replicas ابدأ في آن واحد، هاجر + البذور تشغيل داخل **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`، مفتاح `DatabaseDefaults.MigrationAdvisoryLockKey`):
أول replica لاكتسابها ينقل والبذور؛ بقية الكتلة على القفل ثم البحث هجرات
بالفعل تطبيق (no-op) والمالك الموجود بالفعل. لا يوجد وظيفة هجرة منفصلة أو انتخاب الزعيم هو
المطلوبة. إذا أضفت البذور first-run قم بوضعها **داخل** نفس كتلة حراسة حتى يكون كاتب واحد.

## عقدة-وكيل HTTP resilience

يتحدث العقدة الرئيسية إلى كل عامل `CtraderCliNode` عبر HTTP من خلال ثلاثة عملاء مقسمة الغرض حتى
عقدة غير مستقرة أو الشبكة أبدًا فساد الحالة:

- **قراءة** (`status` / `report` / `stats`) — idempotent GETs، أعاد محاولة على عابرة الفشل
  (exponential backoff + jitter، `NodeAgentHttp.ReadRetryCount`) مع per-attempt و timeouts الكلي.
- **كتابة** (`start` / `stop` / `clean`) — غير idempotent POSTs، مهلة ولكن **أبدًا أعيد محاولة**: أ
  retried `start` قد الإطلاق المزدوج لحاوية.
- **تدفق** (`logs`) — الطويل المعيشة `docker logs -f` تدفق يحصل على timeout اللانهائي ولا
  خط أنابيب المرونة لذا tailing أبدا قطع.

عقدة التي تبقى غير قابلة للوصول يتم التعامل معها بواسطة heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md);
طبقة HTTP فقط يسلس البثور العابرة.

## طبقات بدون حالة

الويب (Blazor Server + API) وخادم MCP بدون حالة خلف قاعدة البيانات وتكرار بحرية.
المصادقة قائمة على cookie؛ ويب مقياس أفقيًا خلف موازن التحميل. خادم MCP منفصل
العملية/النشر لذا يتسع بشكل مستقل عن الويب.

## مرونة اتصال قاعدة البيانات

كل مضيف يفتح استراتيجية قاعدة البيانات باستخدام **تحديث** لذا عابر
قطع أو managed-Postgres failover (RDS / Flexible Server patching) يتم إعادة محاولة بدلاً من
السطح كخطأ للمستخدم:

- الويب و MCP تسجيل السياق من خلال مكون Aspire Npgsql مع `DisableRetry=false`
  و `CommandTimeout` صريح (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) سجل عبر `UseAppNpgsql` والذي ينطبق نفس
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout من `DatabaseDefaults`.

جميع الكتابات هي `SaveChanges` واحد / واحد `ExecuteUpdate` / واحد `ExecuteSql` البيانات لذلك ال
استراتيجية إعادة المحاولة آمنة (لا توجد حاجة لمعاملة متعددة البيانات دليل `strategy.ExecuteAsync`
الالتفاف). إذا أضفت معاملة يدوية أو متعددة `SaveChanges` في عملية منطقية واحدة والتفافه
هذا في `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — وإلا فإنه يطرح تحت إعادة محاولة.

## قائمة التحقق من التوسع

- [ ] Postgres مقاسة لحمل الاتصال المضافة (كل Web/MCP/node replica يفتح حوض).
- [ ] `App:Copy:Enabled=true` على كل عقدة يجب أن تستضيف ملفات تعريف النسخ.
- [ ] متميز `App:Copy:NodeName` لكل مشرف co-located (K8s: per-pod الافتراضي بخير).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] وكلاء عقدة نشرت حيث Docker مميز متاح (AKS/EKS/EC2/VM و ليس Fargate).
- [ ] Multi-replica الويب: اضبط سلسلة الاتصال `signalr` (Redis backplane) **و** تمكين ingress
      تقاربات الجلسة (جلسات دبقة) حتى دائرة Blazor الاتصال مجددا إلى pod حي. A component
      الاستثناء يتم التقاطه بواسطة `MainLayout` `ErrorBoundary` (صديقة إعادة محاولة والدائرة البقاء على قيد الحياة).
