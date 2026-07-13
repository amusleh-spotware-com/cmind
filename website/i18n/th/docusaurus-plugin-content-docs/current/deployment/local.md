---
title: รันมันทั้งเครื่องแบบ
description: รับ cMind ทำงานบนเครื่องของคุณเองในไม่กี่นาทีด้วย Docker Compose (หรือ .NET Aspire สำหรับการพัฒนา)
sidebar_position: 1
---

# รัน cMind locally 🖥️

นี่คือวิธีที่เร็วที่สุดในการเห็น cMind จริง — อินสแตนซ์ที่สมบูรณ์บนเครื่องของคุณเอง หยิบกาแฟ; คุณมักจะลงชื่อเข้าระบบก่อนที่จะเย็น

:::tip สิ่งที่คุณจะมี
Web app ที่ทำงาน **localhost:8080**, MCP server at **localhost:8081**, Postgres database และ local worker node ที่พร้อมสร้างและ backtest cBots ทั้งหมดบนเครื่องของคุณ ทั้งหมดของคุณ
:::

**ก่อนที่จะเริ่ม คุณต้องการหนึ่งใน:**

- **Just Docker** → ใช้ Option A (ไม่จำเป็น .NET SDK) recommended สำหรับมองแรก
- **.NET 10 SDK + Docker** → ใช้ Option B ถ้าคุณต้องการ hack บนโค้ด

ทั้งเส้นทางเป็น cross-platform (Windows / macOS / Linux)

## Option A — Docker Compose (ไม่จำเป็น .NET SDK)

Prereq: Docker Desktop (หรือ Docker Engine + compose plugin)

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (เข้าสู่ระบบด้วย owner จาก `.env`; บังคับเปลี่ยนรหัสผ่านในการ login ครั้งแรก)
- MCP server: <http://localhost:8081/mcp>
- Postgres data persists ใน `pgdata` volume; schema migrates อัตโนมัติใน startup

Web container mounts host Docker socket (`/var/run/docker.sock`) ดังนั้นตัวสร้างในเบราว์เซอร์และ seeded **LocalNode** build + run cTrader Console containers บนเครื่องของคุณ

**Cross-platform notes**
- Docker Desktop (Windows/macOS) exposes socket ที่ `/var/run/docker.sock` — compose mount ทำงาน as-is
- Linux: ensure user ของคุณสามารถเข้าถึง socket หรือ run compose ด้วย privileges ที่เพียงพอ
- Web image คือ `linux/amd64`; บน Apple Silicon Docker รันมันภายใต้ emulation

Stop และ wipe:

```bash
docker compose down          # keep data
docker compose down -v       # also delete the database volume
```

## Option B — .NET Aspire (สำหรับการพัฒนา)

Prereq: .NET 10 SDK + Docker

```bash
dotnet run --project src/AppHost
```

Aspire orchestrates Postgres Web MCP pgAdmin; wires connection strings + OTLP; opens dashboard ตั้ง owner credentials เป็น Aspire parameters (`OwnerEmail` `OwnerPassword`)

Run เพียง web app เทียบกับ Postgres ที่มีอยู่:

```bash
dotnet run --project src/Web
```

## เพิ่มโหนด worker locally

Seeded LocalNode ทำงานแล้ว บนเครื่องของคุณ เพื่อ exercise **auto-discovery** locally start node agent ชี้ที่ Web app (ดู [node discovery](../operations/node-discovery.md)) ด้วย `NodeAgent:MainUrl=http://host.docker.internal:8080` และจับคู่ `JoinToken`

## Troubleshooting 🔧

Docker มีความเห็น นี่คือผู้ต้องสงสัยปกติ:

| Symptom | Likely cause & fix |
|---|---|
| `port is already allocated` บน 8080/8081 | บางสิ่งอื่นใช้พอร์ต หยุดมัน หรือเปลี่ยนการแมปใน `docker-compose.yml` |
| Web starts แต่ builds/backtests fail | Docker socket ไม่ได้ mounted หรือเข้าถึงได้ บน Linux ให้แน่ใจว่า user ของคุณสามารถเข้าถึง `/var/run/docker.sock` |
| `permission denied` บน socket (Linux) | เพิ่ม user ของคุณไปยัง `docker` group (`sudo usermod -aG docker $USER`) และ re-login หรือ run ด้วย privileges ที่เพียงพอ |
| Very slow first run | First build pulls images และ compiles — subsequent runs เร็วมาก บน Apple Silicon `linux/amd64` web image รันภายใต้ emulation |
| ไม่สามารถเข้าสู่ระบบ | ตรวจสอบ `OWNER_EMAIL` / `OWNER_PASSWORD` ใน `.env` ของคุณ First login บังคับเปลี่ยนรหัสผ่าน |
| Database weirdness หลังการอัปเกรด | `docker compose down -v` wipes volume สำหรับ clean slate (คุณจะสูญเสีย local data) |

Still stuck? [Open a Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) — เราเป็นมิตร next stop: [deploy สำหรับจริง →](./cloud.md)
