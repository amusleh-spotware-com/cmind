---
title: Покрени га локално
description: Узми cMind покретања на твој машини за неколико минута са Docker Compose (или .NET Aspire за развој).
sidebar_position: 1
---

# Покрени cMind локално 🖥️

Ово је најбржи начин да видиш cMind за прави — пуна инстанца на твој машини. Узми кафу;
вероватно ћеш бити пријављен пре его што буде хладно.

:::tip Шта ћеш имати на крају
Апликација која ради веб на **localhost:8080**, MCP сервер на **localhost:8081**, Postgres база,
и локални радни чвор спремни за грађење и тестирање cBots. Све на твој машини, сви твоја.
:::

**Пре него што почнеш, потребни су ти:**

- **Само Docker** → користи Опција A (без .NET SDK потребна). Препоручено за прву погледа.
- **.NET 10 SDK + Docker** → користи Опција B ако желиш da хакуј на кода.

Оба пута су крос-платформа (Windows / macOS / Linux).

## Опција A — Docker Compose (без .NET SDK потребна)

Предуслов: Docker Desktop (или Docker Engine + compose додатак).

```bash
cp .env.example .env        # уредити PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (пријава са власником из `.env`; присиљена је промена лозинке на прву пријаву).
- MCP сервер: <http://localhost:8081/mcp>.
- Postgres подаци остају у `pgdata` јачини; шема мигрира аутоматски при стартовању.

Web контејнер монтира домаћин Docker сокет (`/var/run/docker.sock`) тако у-прегледач градитељ и сеедна **LocalNode** грађење + трчене cTrader Console контејнери на твој машини.

**Крос-платформа забелешке**
- Docker Desktop (Windows/macOS) експонира сокет на `/var/run/docker.sock` — compose монтира ради као-је.
- Linux: осигура твој корисник може приступити сокету, или покрени compose са довољно привилегија.
- Web слика је `linux/amd64`; на Apple Silicon Docker трчи испод емулације.

Стоп и обришешиши:

```bash
docker compose down          # чувај подаци
docker compose down -v       # такође обришешиши базу волумен
```

## Опција B — .NET Aspire (за развој)

Предуслов: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire оркестрира Postgres, Web, MCP, pgAdmin; жице конекцион стрингови + OTLP; отвара контролну плоче. Постави власника верене као Aspire параметри (`OwnerEmail`, `OwnerPassword`).
