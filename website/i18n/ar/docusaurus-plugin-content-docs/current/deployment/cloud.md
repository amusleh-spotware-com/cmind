---
title: النشر إلى السحابة
description: انشر cMind إلى Azure أو AWS أو Kubernetes. أي منصة تناسب، المتطلبات الأساسية، والأدلة خطوة بخطوة.
sidebar_position: 2
---

# النشر إلى السحابة

تجاوزت جهازك المحمول؟ حان الوقت لوضع cMind على البنية التحتية الحقيقية. خبر جيد: إنها مصممة للتوسع مع حفل المشغل تقريباً - لا ZooKeeper، لا انتخاب الزعيم، فقط النسخ والقاعدة البيانات.

**الشيء الواحد المهم مقدماً:** الطبقة بدون حالة (الويب + MCP) تعمل بسعادة على *أي* منصة حاوية، لكن **وكلاء العقد يحتاجون إلى Docker الامتيازات** (فهم يبنون ويشغلون حاويات cTrader). هذا يستبعد runtimes بدون خادم مثل Azure Container Apps و AWS Fargate للـ *agents* - تشغيل تلك على [Kubernetes](./kubernetes.md)، أو VM، أو EC2 والإشارة إليهم في URL الويب الخاص بك.

اختر مسارك:

- 🟦 **[Azure](./cloud-azure.md)** — حاويات الحاويات + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — الرسم البياني Helm، يعمل على AKS / EKS / في أي مكان.
- 📈 **[التوسع](./scaling.md)** — كيف يتوسع الكل والشفاء الذاتي بمجرد رفعه.

الطبقة بدون حالة (الويب + MCP) تعمل على أي منصة حاوية؛ Postgres = قاعدة بيانات مُدارة. **وكلاء العقد يحتاجون إلى Docker الامتيازات (DinD)** — runtimes حاوية بدون خادم (Azure Container Apps، AWS Fargate) كتلة. تشغيل وكلاء على Kubernetes ([kubernetes.md](kubernetes.md)) أو VM/EC2، الإشارة في URL الويب.

| السحابة | الطبقة بدون حالة | قاعدة البيانات | الدليل |
| ----- | -------------- | -------- | ----- |
| Azure | تطبيقات الحاويات (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

المتطلبات الأساسية المشتركة، كلاهما:

1. بناء + دفع ثلاث صور إلى registry السحابة يمكن أن تسحب (`cmind-web`، `cmind-mcp`، `cmind-node-agent`).
2. اختر الأسرار: كلمة مرور قاعدة البيانات، البريد الإلكتروني للمالك/كلمة المرور، **رمز انضمام الاكتشاف** (≥ 32 حرف) يشاركه تطبيق الويب + كل وكيل عقدة.
3. نشر IaC (أدناه)، ثم أحضر وكلاء العقد بشكل منفصل (K8s/VM) مع `NodeAgent__MainUrl` = URL الويب المنشورة، `NodeAgent__JwtSecret` = رمز الانضمام.

الكشف والتسجيل والمسبارات تتصرف بنفس الطريقة التي تعمل بها في الإعدادات المحلية/K8s - انظر [اكتشاف العقد](../operations/node-discovery.md) و [التسجيل](../operations/logging.md).
