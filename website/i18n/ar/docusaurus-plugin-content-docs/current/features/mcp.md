---
description: "cMind ship Model Context Protocol (MCP) server كعملية منفصلة/النشر — مقياس + إعادة النشر مستقل عن تطبيق الويب. أداة cBot وinstance وAI ..."
---

# خادم MCP

cMind ship Model Context Protocol (MCP) server كـ **عملية منفصلة/النشر** — مقياس + إعادة النشر
مستقل عن تطبيق الويب. أداة cBot وinstance وAI لعملاء MCP (على سبيل المثال مساعدي AI) عبر نقل HTTP + SSE.

## المصادقة

- مفاتيح API لكل مستخدم `mcpk_<hex>`، SHA-256 مجزأ وفهرس البادئة (`McpKeyAuthHandler`). إدارة من صفحة **Mcp**
  (`McpApiKey` aggregate).
- نقل HTTP بدون حالة مع `AddHttpContextAccessor` — مكالمات الأداة تعمل كمستخدم موثق.

## أدوات

- `CBotTools` — مؤلف / بناء cBots.
- `InstanceTools` — تشغيل / backtest / فحص instances.
- `AiTools` — إنشاء ومراجعة وحساسية وتحليل-backtest وأدوات نسخ.

## العمليات

أداة `/version`؛ نقاط نهاية الصحة (`/health` و`/alive`) تم تعيينها جميع البيئات لـ K8s/cloud probes. Serilog المهيكلة JSON + OpenTelemetry، نفس تطبيق الويب.
