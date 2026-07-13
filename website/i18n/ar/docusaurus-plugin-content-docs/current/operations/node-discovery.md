---
description: "عقد cTrader CLI تنضم إلى الكتلة بـ تسجيل ذاتي + نبض - لا إدخال يدوي. نفس نمط وكلاء Consul/Nomad/kubeadm: وكيل boots معرفة موقع العقدة الرئيسية..."
---

# اكتشاف العقدة التلقائي

عقد cTrader CLI تنضم إلى الكتلة بـ **تسجيل ذاتي + نبض** - لا إدخال يدوي. نفس النمط كـ وكلاء Consul/Nomad/kubeadm: وكيل boots معرفة موقع العقدة الرئيسية + shared cluster secret ثم بشكل مستمر يعلن عن نفسها.

> تم التحقق منها end-to-end على Docker Compose و `kind` كتلة Kubernetes: وكلاء التسجيل الذاتي و تظهر في قاعدة البيانات قابلة للوصول و تلقائياً ميل غير قابل للوصول عندما نبضات توقف بعد TTL و عودة على الإنترنت عند استئناف.

## كيف يعمل

```
وكيل CtraderCliNode              رئيسي (ويب)
------------------              ----------
POST /api/nodes/register  ── رمز الانضمام ──▶ تحقق من الرمز (constant-time)
  { name و baseUrl و mode و     تحقق من نسخة البروتوكول
    maxInstances و dataDir و    upsert CtraderCliNode حسب الاسم
    protocolVersion }            طابع LastHeartbeatAt و IsReachable=true
        ▲                        └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  كل HeartbeatInterval    NodeHeartbeatMonitor (الخلفية):
        └──────────────────────────────── إذا الآن - LastHeartbeatAt > HeartbeatTtl
                                             → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **التسجيل == نبض.** وكيل re-POSTs على `HeartbeatIntervalSeconds`. أول اتصال ينشئ عقدة (`NodeRegistered` event)؛ المكالمات لاحقة تحديث الحيوية. استئناف نبض بعد انقطاع يقلب العقدة مرة أخرى قابل للوصول (`NodeCameOnline`).
- **المصالحة الحيوية.** `NodeHeartbeatMonitor` علامات عقد الذي يتجاوز نبض الأخير `HeartbeatTtl` غير قابل للوصول. المجدول (`IsActive`/`AcceptsRun`/`AcceptsBacktest` مبواب على القابلية للوصول) يتوقف عن وضع العمل حتى يبلغ مرة أخرى.
- **مطالبة الحالة اليتيمة.** `NodeInstanceReclaimer` (الخلفية) تحولات أي عقدة غير محطة تركت على عقدة غير قابل للوصول إلى **فشل** (`FailureReason = "Node unreachable - instance reclaimed"` و `InstanceFailed` domain event → user notification) لذا crashed/partitioned node لا يمكن أبداً ترك instance stuck "Running" للأبد. المطالبة فقط نار بمجرد نبض العقدة الأخير stale تتجاوز `HeartbeatTtl + InstanceReclaimGrace` و إعطاء brief-blip فرصة لاستعادة أول. المطالبة **التشغيل لا تُعيد جدولة تلقائياً**: كقطة partitioned-but-alive node قد يكون لا يزال تنفيذ الحاوية وليس توطين حاوية مستوى و لذا إعادة إطلاق كان المخاطرة double execution - المستخدم يعيد تشغيل التشغيل المطالب بقصد. Backtests self-exit لذا backtest المطالبة ببساطة re-run.
- **الهوية هي اسم العقدة.** رئيسي upserts بواسطة `NodeName` لذا pod الذي IP/URL التغييرات على إعادة التشغيل تحتفظ بالهوية و re-registers جديد `AdvertiseUrl`.
- **الوضع ثابت في أول تسجيل.** نوع وضع العقدة (`Run`/`Backtest`/`Mixed`) هو persisted type و لا يمكن التغيير على نبض؛ re-registration مع وضع مختلف موجود للحيوية لكن التغيير الوضع تم تجاهله (مسجل كتحذير). لتغيير الوضع: حذف العقدة و السماح لها re-register.

## التكوين

رئيسي (ويب) - `App:Discovery`:

| المفتاح | الافتراضي | المعنى |
|-----|---------|---------|
| `Enabled` | `false` | مفتاح Master للتسجيل endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 حرف) يجب على الوكلاء تقديم. |
| `HeartbeatTtl` | `00:01:30` | نعمة قبل العقدة الصامتة ميل غير قابل للوصول. |
| `InstanceReclaimGrace` | `00:01:00` | هامش إضافي تتجاوز `HeartbeatTtl` قبل instance stranded على عقدة غير قابل للوصول يتم المطالبة (فشل). |
| `MonitorInterval` | `00:00:30` | ما مدى سرعة monitor و instance-reclaimer sweep. |
| `HeartbeatInterval` | `00:00:30` | القيمة المرجعة للوكلاء كـ cadence المقترح. |

وكيل (CtraderCliNode) - `NodeAgent`:

| المفتاح | المعنى |
|-----|---------|
| `MainUrl` | Base URL من عقدة رئيسية. فارغ = وضع التسجيل اليدوي (حلقة no-op). |
| `AdvertiseUrl` | URL رئيسي يستخدم للوصول إلى **هذا** الوكيل. |
| `NodeName` | الاسم الفريد؛ الافتراضي اسم الجهاز إذا فارغ. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | تلميح القدرة المحترمة من قبل المجدول. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | يجب أن تساوي رئيسي `JoinToken` - كل من التسجيل bearer و dispatch JWT signing key. |

## نموذج الأمان (v1)

العقد المسجلة التلقائي تشارك **سر واحد cluster** (`JoinToken` == كل وكيل `JwtSecret`). موقع رئيسي كل طلب dispatch كـ 5-minute HS256 JWT مع هذا السر؛ وكيل يتحقق. المتطلبات:
