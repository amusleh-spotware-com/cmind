---
slug: /for-traders
title: cMind สำหรับผู้ค้า cTrader
description: ทำไมผู้ค้า cTrader ควร self-host cMind — เป็นเจ้าของ stack และข้อมูลของคุณ author backtest run และ monitor cBots ในคอนโซล AI-powered เดียว บนแล็ปท็อป VPS หรือโทรศัพท์ของคุณ
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind สำหรับผู้ค้า cTrader 📈

คุณค้าแล้ว cTrader คุณ juggle code editor backtester VPS และ browser tabs สามแท็บอยู่แล้ว **cMind ปิดทั้งหมดนั้นเป็น dark keyboard-friendly console เดียวที่คุณรัน** — และมันเป็น open source ดังนั้นไม่มีอะไรเกี่ยวกับ edge strategies หรือ credentials ของคุณออกจากกล่องของคุณ

:::tip TL;DR
Self-host cMind บนแล็ปท็อป cheap VPS หรือ home server Author backtest run และ monitor cBots ในสถานที่เดียว กับ AI core ทำงานหนัก → [รันใน 5 นาที](./deployment/local.md)
:::

## ทำไมถึง self-host แทน hosted service

- **เป็นเจ้าของ stack และข้อมูลของคุณ** cBots credentials tokens และ equity history ของคุณอยู่บน **infrastructure ของคุณ** — ไม่มี third party ไม่มี lock-in ไม่มี "เรา sunsetting ผลิตภัณฑ์นี้" email
- **มันเป็นของคุณจริงๆเพื่อเปลี่ยน** C# 14 / .NET 10 strict DDD EF Core + PostgreSQL MCP server — ทั้งหมด open source และ hackable Fork มัน extend มัน ส่ง PR
- **ไม่มี per-feature paywall** นำ AI key ของคุณเองสำหรับผู้ให้บริการใด ทุก AI feature เปิด

ชอบไม่รัน servers เอง A hosting company สามารถรัน managed cMind สำหรับคุณ — ดู [For cloud & VPS providers](./for-cloud-providers.md)

## คอนโซลเดียว ไม่มี tab-juggling

- **Author** ใน Monaco IDE จริง (the VS Code editor) ด้วย C# **และ** Python templates และ sandboxed `dotnet build` ใน throwaway containers → [Build & backtest](./features/build-and-backtest.md)
- **Backtest** ข้ามเหนือ fleet ของ nodes และดู equity curves stream กลับมา live
- **Run** strategies live และ **monitor** พวกเขาจาก dashboard เดียว → [Dashboard](./features/dashboard.md)
- **Copy** master account ไปที่ accounts มากมายข้ามโบรกเกอร์ และ cTrader IDs ด้วย reconciliation ที่เอาชีวิตรอด dropped connections และ rotating tokens → [Copy trading](./features/copy-trading.md)

## AI ที่ทำ chores ไม่ใช่ small talk

นำ API key ของคุณเอง (ผู้ให้บริการที่รองรับ — cloud หรือ local model) และรับ plain-English → real compiling cBot ด้วย self-repair loop parameter tuning backtest post-mortems และ risk guard ที่สามารถ auto-stop misbehaving bot → [Meet the AI core](./features/ai.md)

## Institutional-grade tooling สำหรับคนคนเดียว

ความแข็งกร้าว desk จ่ายอยู่ บนกล่องของคุณเอง:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## วิ่งที่สถานที่คุณทำ

เริ่มบนแล็ปท็อปของคุณด้วย `docker compose up` graduate เป็น cheap VPS หรือ home server เมื่อคุณพร้อม และตรวจสอบ bots ของคุณจากโทรศัพท์ของคุณ — cMind เป็น installable mobile-first [PWA](./features/pwa.md) → [รันมัน locally](./deployment/local.md)

ต้องการ AI client ของคุณเพื่อขับเคลื่อนมันหรือ มี built-in [MCP server](./features/mcp.md)

## ช่วยทำให้ดีขึ้น

cMind เป็น open source และ MIT-licensed — roadmap เป็น community-shaped:

- ไฟล์ issues และ feature requests และ vote บนสิ่งที่สำคัญ
- เพิ่มเทมเพลต cBot AI provider adapters หรือ UI translations
- ส่ง PRs — three test tiers (unit + integration + E2E) และ strict DDD เก็บ bar สูง และ [Contributing guide](./contributing.md) เดิน คุณผ่านมัน

พร้อม → [อ่าน intro](./intro.md) แล้ว [รันมัน locally](./deployment/local.md)
