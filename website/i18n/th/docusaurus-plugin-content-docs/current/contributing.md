---
slug: /contributing
title: Contributing
description: วิธี contribute ไป cMind — human หรือ AI-assisted PRs ยินดีต้อนรับ First contribution ใน 10 นาที
sidebar_position: 5
---

# Contributing to cMind 🛠️

ขอบคุณสำหรับการอยู่ที่นี่ cMind ดีขึ้นทุกครั้งที่คนใดคนหนึ่ง opens issue reports precise cTrader behavior fixes typo ใน docs เหล่านี้ หรือ ships PR **คุณไม่ต้องเป็น .NET wizard** — testers traders และ doc-fixers ถูกประเมินค่าเท่า folks writing aggregates

:::tip canonical guide อยู่ใน repo
หน้านี้เป็น friendly on-ramp full always-current process — ground rules coding conventions review flow — เป็น **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**
:::

## First contribution ของคุณใน ~10 นาที

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 warnings หรือ CI ปฏิเสธจะ politely คุณ
dotnet test           # unit + integration + E2E
```

พบสิ่งใด ๆ ที่ fix Branch มัน เปลี่ยนมัน เพิ่ม test และ open PR นั่นคือ loop ทั้งหมด

## วิธี ช่วย (ไม่ใช่ทั้งหมดของพวกเขา code)

| Contribution | Effort | ที่ |
|---|---|---|
| 🐛 Report reproducible bug | 10 นาที | [Bug report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Suggest feature | 10 นาที | [Feature request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Improve docs เหล่านี้ | 15 นาที | Edit ภายใต้ `website/docs/` และ PR |
| 🧪 เพิ่ม missing test | 30 นาที | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Report exact cTrader behavior | 10 นาที | [Open Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## house rules (short version)

cMind เคลื่อน **real money** ดังนั้นสิ่งต่าง ๆ ไม่กี่ อย่าง non-negotiable — และ honestly พวกเขา ทำให้ codebase joy ทำงาน:

- **Strict Domain-Driven Design** Business logic อยู่บน aggregates และ value objects ไม่เคย endpoints หรือ UI (มี playbook friendly ใน repo)
- **Three test tiers ทุกครั้ง** Unit + integration + E2E *รวมทั้ง* failure paths (dropped connections rejected orders dead nodes) Green tests คือ price ของ admission
- **Zero warnings** `TreatWarningsAsErrors=true` Modern C# 14 idioms
- **ไม่มี secrets ไม่มี magic strings ไม่เคย `DateTime.UtcNow`** (inject `TimeProvider` แทน)
- **Docs ใน commit เดียว** เปลี่ยน behavior → update doc. ใช่ รวมถึง site นี้

Full detail ด้วย *why* ด้านหลัง rule แต่ละ ใน [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) และ [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md)

## Contributing ด้วย AI 🤖

เรา genuinely ยินดีต้อนรับ **AI-assisted PRs** — project นี้ built เพื่อ worked บน agents เก่าเทพ humans ถ้าคุณกำลัง driving Claude Copilot หรือ similar: point มันที่ [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md) ให้มัน read nested `CLAUDE.md` files และ hold มันเป็น bar เดียว (tests zero warnings DDD) Good AI PR indistinguishable from good human PR — same review same welcome

## Be excellent ต่อกัน

เรามี [Code of Conduct](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md) gist: be kind assume good faith และ remember มี person (หรือ person agent ของ) ที่ end อื่น Ask questions early — นั่นเป็น strength ไม่ bother

ยินดีต้อนรับ we can't รอ เห็น what you build 🎉
