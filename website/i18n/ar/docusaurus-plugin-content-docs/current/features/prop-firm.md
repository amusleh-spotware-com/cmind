---
description: "شركات Prop-firm بالتجزئة (على غرار FTMO) تبيع حسابات التقييم: يجب على المتداول تحقيق هدف الربح مع البقاء داخل حدود المخاطر (الحد الأقصى للخسائر اليومية والحد الأقصى ..."
---

# محاكاة تحدي Prop-firm

شركات Prop-firm بالتجزئة (على غرار FTMO) تبيع **حسابات التقييم**: يجب على المتداول تحقيق هدف الربح مع
البقاء داخل حدود المخاطر (الحد الأقصى للخسائر اليومية والسحب الأقصى/المتراجع والاتساق وحدود الوقت) قبل التمويل. يسمح cMind للمستخدم بإنشاء **تحد مخصص من أي شكل صناعي**، الربط بـ
`TradingAccount`، **التشغيل مثل عملية النسخ التجاري** — بدء/إيقاف وتستضيف على العقدة
المتتبع **حية فوق cTrader Open API**. تقيم المجموعة كل قاعدة بشكل حتمي؛ عند النجاح أو الانتهاك،
ينتهي التحدي والعلامات والتنبيهات للمستخدم.

## المجال (السياق المحدود: PropFirm)

`PropFirmChallenge` = جذر المجموعة (الوحدة `Core.PropFirm`)، ويشير إلى `TradingAccount` الخاص به فقط
من خلال معرف قوي (لا توجد FK عبر المجموعة). يمتلك تقييم القواعد وآلة الحالة والمرحلة وعقد العقدة.

### الكائنات القيمة ومجموعة القواعد

- **`Money`** (غير سالب)، **`MoneyAmount`** (موقّع)، **`Percent`** (0–100]، **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — قراءة التغذية إلى المجموعة.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — حقائق غير الأسهم.
- **`DailyLossLimit`** `(percent, basis)` — الأساس `Equity` (داخل اليوم، يتضمن P&L عائمة) أو `Balance`
  (المحققة فقط).
- **`DrawdownLimit`** — `Static` (من الرصيد الأولي)، `TrailingPercent` (من ذروة الأسهم)، أو
  `TrailingThresholdDollar` (يتابع ذروة الأسهم بمبلغ دولار ثابت، ثم **أقفال في الرصيد الأولي**
  بمجرد وصول الأسهم إلى الحد الأدنى — نمط العقود الآجلة).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — يحظر المرور بينما يهيمن يوم واحد على إجمالي الربح.
- **`ChallengeRules`** يحمل أعلاه بالإضافة إلى `MaxCalendarDays` و`MaxInactivityDays` و`MaxOpenPositions` و
  `AllowWeekendHolding` و`AllowNewsTrading` و`Kind` و`SingleStep`. تعيش رياضيات القاعدة على VOs
  (`DrawdownLimit.IsBreached` و`DailyLossLimit.IsBreached` و`ConsistencyRule.IsSatisfied`)؛ منظم المجموعة.

### أنواع التحدي والقوالب

`ChallengeTemplates.For(kind)` يبني preset صالح لـ `OnePhase` و`TwoPhase` و`ThreePhase` و
`InstantFunding` أو `Custom` (التحكم الكامل). واجهة المستخدم مليئة مسبقًا بالقالب؛ قد يعدل المستخدم أي حقل.

### المراحل والحالة

- **المراحل:** `Evaluation → Verification → Funded` (الخطوة الفردية تتخطى التحقق).
- **الحالة:** `Active` و`Passed` و`Failed` بالإضافة إلى دورة الحياة `Stopped` (التتبع متوقف) — `Create` يبدأ
  التحدي `Active`؛ `Stop()`/`Resume()` تبديل `Active↔Stopped`.
- **`BreachReason:`** `DailyLoss` و`MaxDrawdown` و`Consistency` و`TimeLimit` و`Inactivity` و
  `WeekendHolding` و`NewsTrading` و`MaxExposure`.

### تقييم القاعدة

- **`RecordEquity(EquitySnapshot, now)`** — لفات يوم التداول عند حدود اليوم (التقاط ربح اليوم السابق
  لقاعدة الاتساق)، وتحديث الذروة/الذروات اليومية، ثم **الفشل عند أول انتهاك**
  (الخسارة اليومية → السحب → حد الوقت → عدم النشاط، بالترتيب) أو تقدم المرحلة عندما يكون هدف الربح
  ويوم التداول الأدنى ومتطلبات الاتساق تم تلبيتها جميعًا. لقطات خارج الترتيب والسجلات في
  التحدي الطرفي رمي `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — يقيّم قواعس السلوك (أقصى المراكز المفتوحة والقيام بنهاية الأسبوع
  وتداول الأخبار)، يختم النشاط لقاعدة عدم النشاط.
- الناعم **`PropFirmDrawdownWarning`** ينطلق مرة واحدة عندما يعبر استخدام الأسهم عتبة قابلة للتكوين.

أحداث المجال: `PropFirmChallengeStarted` و`PropFirmChallengeStopped` و`PropFirmPhasePassed` و
`PropFirmChallengePassed` و`PropFirmChallengeBreached` و`PropFirmDrawdownWarning`.

## التتبع الحي (التنفيذ) — استضافة العقدة، الشفاء الذاتي

يعكس التتبع مجموعة استضافة النسخ التجاري بالضبط؛ متتبع الدعم = **ابن عم للقراءة فقط** من محرك النسخ.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` على كل عقدة، مسورة على
  `App:PropFirm:Enabled`. كل دورة **الدعاوى** التحديات النشطة على عقد الشفاء الذاتي
  (`AssignedNode` + `LeaseExpiresAt`؛ تحديات العقدة الميتة المطالب بها بمجرد انتهاء الإيجار —
  نفس الدعوى الذرية `ExecuteUpdate` مثل النسخ التجاري، لذلك لا تتبع العقدتان مرتين أبدًا)، وتجديد الإيجارات
  ودفع الرموز المستديرة في المكان، وإيقاف المضيفين الذين تركوا التحدي `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — واحد لكل تحد. فتح `IOpenApiTradingSession`
  للحساب وعند `App:PropFirm:EquityPollInterval`، إعادة حساب الأسهم الحية والتغذية إلى
  المجموعة. مبادلة رمز الوصول في المكان عند الدوران (بدون إسقاط الجلسة). الخروج عندما يكون التحدي
  لم يعد `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — رياضيات الأسهم الموالية cTrader.
  الأسهم **ليست** مسلمة من Open API، لذا مشتقة: `equity = balance + Σ(unrealized P&L)`،
  حيث P&L لكل موضع هو `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`؛ طويل يقيم عند الطلب، قصير في الطلب). الرصيد من
  `ProtoOATrader`؛ المواضع (سعر الدخول، المبادلة، العمولة) من المصالحة؛ عرض/طلب حي من الفور
  الاشتراكات. نقي ومعزول — نقطة ساخنة لتحويل العملات مختبرة بمفردها.

## التنبيهات

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) الاشتراك في المسار/الانتهاك/تحذير المجال الأحداث
(مسجل كـ `IDomainEventHandler<>`، يتم إرسالها بعد نجاح `SaveChanges`)، ويخطر المستخدم
عبر مسار التنبيه/الحسابات المنظمة (`LogMessages`). تعكس واجهة المستخدم المباشرة نفس تغيير الحالة. هذا
= رد فعل السياق المتقاطع — أبدًا لا يغير المجموعة التحدي.

## API (`/api/prop-firm`، ميزة `PropFirm`، دور User+)

| الطريقة | المسار | الغرض |
|--------|-------|---------|
| GET | `/challenges` | قائمة تحديات المستخدم (النوع والمرحلة والحالة والأسهم الحية والإيجار) |
| GET | `/challenges/{id}` | تحد واحد |
| GET | `/templates` | نماذج صناعية لحوار الإنشاء |
| POST | `/challenges` | إنشاء من القالب **أو** مجموعة قواعد مخصصة بالكامل |
| POST | `/challenges/{id}/start` | استئناف التتبع (Stopped → Active) |
| POST | `/challenges/{id}/stop` | إيقاف التتبع (Active → Stopped، الإفراج عن الإيجار) |
| POST | `/challenges/{id}/equity` | سجل الأسهم Snapshot → إعادة التقييم (المسار اليدوي/بدون التغذية الحية) |
| DELETE | `/challenges/{id}` | حذف الناعمة (مسدود أثناء النشاط) |

MCP: `Mcp/Tools/PropFirmTools.cs` يكشف list/create(from template)/record-equity/start/stop، مسورة على
ميزة `PropFirm`.

واجهة المستخدم: `/prop-firm` (nav *Prop Firm*، مسورة بـ `PropFirm` flag) قائمة التحديات مع **إجراءات صف Start/Stop/Delete**
(ابدأ عندما توقفت، توقف عندما نشطة، حذف معطل أثناء نشطة)، أنشئها من خلال
`NewPropFirmChallengeDialog` (قاطع القالب + محرر القاعدة الكامل). كل الإنشاء/التحرير عبر حوار MudBlazor.

## Live equity feed — تم حلها

الفجوة السابقة "لا توجد تغذية حية P&L الحساب" مغلقة: عند ضبط `App:PropFirm:Enabled`، تتبع العقد
الحساب حي عبر Open API، تغذية الأسهم تلقائيًا. بدونها (الافتراضي)، المجال و
مسار **الأسهم اليدوية** (`POST …/equity`) تشغيل دون تغيير — لا توجد بيانات اعتماد cTrader مطلوبة للبناء/الاختبار/E2E.

## الاختبارات

- **الوحدة** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (تقدم المرحلة والأيام الدنيا والسحب الثابت/المتراجع والخسارة اليومية وحراس النهاية/خارج النظام)؛ `PropFirmChallengeRulesTests` (الرصيد مقابل أساس الخسارة اليومية من الأسهم والسحب المتراجع بالحد الأدنى والقفل والكتلة الاتساق/السماح بحد الوقت وعدم النشاط والحد الأقصى للتعرض والنهاية وتداول الأخبار والإيقاف/الاستئناف وحدود الإيجار والممرات تحرر الإيجار وتحذير السحب)؛ `PropFirmValueObjectTests` (نطاقات VO + قاعدة-VO الرياضيات)؛ `PropFirmEquityCalculatorTests` (P&L طويل/قصير و
  مبادلة/عمولة وتحويل الاقتباس→الإيداع والتسعير المفقود)؛ `PropFirmTrackingHostTests` (الأسهم الحية
  تقود المسار/الفشل مقابل جلسة وهمية موسعة)؛ `PropFirmAlertNotifierTests`. الوقت صريح /
  `FakeTimeProvider` — لا يوجد قراءات على مدار الساعة.
- **التكامل** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (الرحلة ذهابًا وإيابًا + تسجيل الأسهم +
  حذف ناعم وقواعد غنية + جولة إيجار) و `PropFirmTrackingLeaseTests` (الدعوى والإيجار المتنازع عليه
  وإعادة المطالبة بعد انقضاء الوقت عبر هويات عقدة اثنين) على Postgres الحقيقي.
- **E2E** — `E2ETests/PropFirmTests.cs`: الإنشاء + تسجيل الأسهم إلى `Passed`؛ إيقاف→ابدأ→مسار الانتهاك؛
  نقطة نهاية القوالب.
- **الإجهاد / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: تدفقات الأسهم والنشاط الموحاة العشوائية
  (لفات اليوم والارتفاعات والأعطال والرموز المكررة + خارج الترتيب عبر
  العديد من التحديات ذات القواعد المختلطة، تؤكد الحالات الطرفية الدقيقة بالضبط وحدود الذروة-الحالية الحالية
  الإخفاقات المعقولة.

## التشكيل (`App:PropFirm`)

`Enabled` (معطل افتراضيًا)، `ReconcileInterval` و`EquityPollInterval` و`LeaseTtl` و
`DrawdownWarnThresholdPercent` و`NodeName`.
