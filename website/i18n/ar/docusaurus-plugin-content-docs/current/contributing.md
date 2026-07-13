---
slug: /contributing
title: المساهمة
description: كيفية المساهمة في cMind - جهود بشرية أو مساعدة AI مرحب بها. المساهمة الأولى في 10 دقائق.
sidebar_position: 5
---

# المساهمة في cMind

شكراً لوجودك هنا. cMind تحسن في كل مرة يفتح فيها شخص ما مشكلة، أو يبلغ عن سلوك cTrader دقيق، أو يصلح خطأ إملائياً في هذه الوثائق بالفعل، أو ينقل PR. **أنت لا تحتاج إلى أن تكون ساحراً .NET** - المختبرون والمتاجرون والمصلحون الأوثائق يتم تقديرهم مثل الأشخاص الذين يكتبون التجميعات.

:::tip الدليل الكنسي يعيش في الريبو
هذه الصفحة هي على المنحدر الصديقة. العملية الكاملة الحالية دائماً - القواعد الأساسية والاتفاقيات الترميز وتدفق المراجعة - موجودة في **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## مساهمتك الأولى في ~10 دقائق

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 تحذيرات، أو CI سيرفض بلطف
dotnet test           # وحدة + تكامل + E2E
```

وجدت شيء لإصلاحه؟ فرعه، غيره، أضف اختبار، وفتح PR. هذه هي حلقة كاملة.

## طرق للمساعدة (ليست كلها الكود)

| المساهمة | الجهد | حيث |
|---|---|---|
| بلّغ عن خطأ قابل للتكرار | 10 دقائق | [تقرير الخطأ](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| اقترح ميزة | 10 دقائق | [طلب الميزة](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| تحسين هذه الوثائق | 15 دقائق | تحرير تحت `website/docs/` و PR |
| أضف اختبار مفقود | 30 دقائق | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| بلّغ عن سلوك cTrader الدقيق | 10 دقائق | [فتح نقاش](https://github.com/amusleh-spotware-com/cmind/discussions) |

## قواعس المنزل (إصدار قصير)

cMind تحرك **الأموال الحقيقية**، لذا بعض الأشياء غير قابلة للتفاوض - وبصراحة، تجعل base code فرحة للعمل في:

- **Strict Domain-Driven Design.** منطق الأعمال يعيش على التجميعات وكائنات القيمة، أبداً في نقاط النهاية أو UI. (هناك playbook ودي لها في repo.)
- **ثلاث طبقات اختبار، كل تغيير.** وحدة + تكامل + E2E، *يشمل* مسارات الفشل (اتصالات مسقوطة، أوامر مرفوضة، عقد ميتة). اختبارات خضراء هي سعر الدخول.
- **صفر تحذيرات.** `TreatWarningsAsErrors=true`. لغة C# 14 الحديثة الأساليب.
- **لا أسرار، لا سلاسل السحر، لا أبداً `DateTime.UtcNow`** (inject `TimeProvider` بدلاً من ذلك).
- **وثائق في نفس الالتزام.** السلوك تغيير → تحديث وثيقته. نعم، يشمل هذا الموقع.

التفاصيل الكاملة، مع *السبب* وراء كل قاعدة، في [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) و [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## المساهمة مع AI

نرحب بصدق **PRs المساعدة بـ AI** - هذا المشروع مبني ليتم العمل على وكلاء وكذلك البشر. إذا كنت تقود Claude أو Copilot أو ما شابه: اشير إلى [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md)، دعه يقرأ المتداخلة ملفات `CLAUDE.md`، والحفاظ عليها إلى نفس الشريط (اختبارات، صفر تحذيرات، DDD). PR AI جيد لا يمكن تمييزه عن PR بشري جيد - نفس المراجعة، نفس الترحيب.

## كن رائع لبعضنا البعض

لدينا [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). المحتوى: كن طيباً، افترض حسن نية، وتذكر أن هناك شخصاً (أو وكيل شخص) على الطرف الآخر. اسأل الأسئلة مبكراً - هذه نقطة قوة، وليس مزعجة.

مرحبا بك على متن الطائرة. نحن بالكاد يمكن أن نتطلع لرؤية ما تبني.
