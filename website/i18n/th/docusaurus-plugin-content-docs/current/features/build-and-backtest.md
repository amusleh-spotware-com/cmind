---
description: "สร้าง รัน และ backtest cTrader cBots (C# และ Python ทั้งคู่ .NET) จาก in-browser Monaco IDE ที่รันบน official ghcr.io/spotware/ctrader-console image"
---

# Build & backtest cBots

สร้าง รัน backtest cTrader cBots (C# **และ** Python ทั้งคู่ .NET) จาก in-browser Monaco
IDE ที่รันบน official `ghcr.io/spotware/ctrader-console` image

## Build

- **Builder** page host Monaco editor; `CBotBuilder` compile project ด้วย
  `dotnet build` **ใน throwaway container** (`AppOptions.BuildImage`, work dir bind-mount
  at `/work`) ดังนั้น untrusted user MSBuild targets ไม่ถึง host NuGet restore cached
  ข้าม builds ผ่าน shared volume Web host ต้องมี Docker socket access
- C# + Python starter templates อยู่ใน `src/Nodes/Builder/Templates/`

## Run & backtest

- **Instances** = TPH state hierarchy (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`) Transition replace entity (id change),
  container id ถูกพาไป
- `NodeScheduler` เลือก least-loaded eligible node; `ContainerDispatcherFactory` route ไปยัง
  remote node HTTP agent หรือ local Docker dispatcher
- Completion pollers reconcile exited containers (backtest containers self-exit ผ่าน
  `--exit-on-stop`); report มี → completed (เก็บ `ReportJson`), ไม่มี → failed
- Live container logs stream ไปยัง browser ผ่าน SignalR; backtest equity curves parse จาก
  report + charted

## cTrader Console CLI notes

Backtests ต้องการ `--data-mode` (default `m1`), dates เป็น `dd/MM/yyyy HH:mm` และ
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only) ดู
`ContainerCommandHelpers`

## Nodes & scale

Execution capacity scale โดยเพิ่ม node agents (self-register + heartbeat) ดู
[node discovery](../operations/node-discovery.md) และ [scaling](../deployment/scaling.md)

## เรียกใช้จากตัวแก้ไขโค้ด

การคลิก **เรียกใช้** ในตัวแก้ไขโค้ดจะเปิดกล่องโต้ตอบแทนที่จะเริ่มการเรียกใช้แบบตายตัวที่ตั้งค่าไว้ล่วงหน้า:

- **บัญชีเทรด** (จำเป็น) — บัญชี cTrader ที่ cBot เชื่อมต่อ
- **ชุดพารามิเตอร์** (ไม่บังคับ) — เลือกชุดที่มีอยู่ หรือเว้นว่างไว้เพื่อเรียกใช้ด้วย**ค่าพารามิเตอร์เริ่มต้น**ของ cBot ปุ่ม **+** ข้างตัวเลือกจะสร้างชุดพารามิเตอร์ใหม่ในบรรทัด (ดูด้านล่าง) และเลือกมัน
- **สัญลักษณ์ / กรอบเวลา** ค่าเริ่มต้นคือ `EURUSD` / `h1` และเปลี่ยนได้ **ยกเลิก** หรือ **เรียกใช้**

เมื่อ **เรียกใช้** ตัวแก้ไขจะบันทึกและสร้างซอร์สโค้ดปัจจุบัน เริ่มอินสแตนซ์บนบัญชีที่เลือกด้วยพารามิเตอร์ที่เลือก แล้วติดตามบันทึกคอนเทนเนอร์แบบสด (สตรีมบันทึกจะส่งต่อคุกกี้การตรวจสอบสิทธิ์ของผู้ใช้ที่ลงชื่อเข้าใช้ไปยังฮับ SignalR `/hubs/logs` จึงเชื่อมต่อแทนที่จะล้มเหลวด้วย `Invalid negotiation response received`)

## ชุดพารามิเตอร์

**ชุดพารามิเตอร์** คือชุดการแทนที่พารามิเตอร์ cBot ที่มีชื่อและใช้ซ้ำได้ จัดเก็บเป็นอ็อบเจ็กต์ JSON แบบแบนที่แมปชื่อพารามิเตอร์แต่ละตัวกับค่าสเกลาร์ เช่น `{"Period": 14, "Label": "trend"}` เมื่อเรียกใช้/backtest จะถูกแปลงเป็นไฟล์ cTrader `params.cbotset` (`{ "Parameters": { … } }`) คุณสามารถสร้าง/แก้ไขชุดเป็น JSON ดิบจากกล่องโต้ตอบ **ชุดพารามิเตอร์** ของ cBot หรือในบรรทัดจากกล่องโต้ตอบเรียกใช้

JSON จะถูก**ตรวจสอบ**เมื่อบันทึก: ต้องเป็นอ็อบเจ็กต์แบบแบนเดี่ยวที่ค่าทั้งหมดเป็นสเกลาร์ (สตริง / ตัวเลข / bool) รูทที่ไม่ใช่อ็อบเจ็กต์ อาร์เรย์ อ็อบเจ็กต์ซ้อน ค่า `null` หรือ JSON ที่ผิดรูปแบบจะถูกปฏิเสธ (ข้อผิดพลาดที่ชัดเจนในกล่องโต้ตอบ `400 Bad Request` ที่ API) อ็อบเจ็กต์ว่าง `{}` ได้รับอนุญาตและหมายถึง "ไม่มีการแทนที่"
