---
title: Futtassa helyileg
description: Kerülje a cMind-et az Ön gépén néhány perc alatt Docker Compose-sal (vagy .NET Aspire fejlesztéshez).
sidebar_position: 1
---

# cMind futtatása helyileg 🖥️

Ez a leggyorsabb módja a cMind igazi megnézésének — egy teljes instancia az Ön gépén. Vegyenek egy kávét; valószínűleg bejelentkezni fog, mielőtt lehűl.

:::tip[Mit fog lenni a vég végén]
Egy futó webalkalmazás a **localhost:8080**-on, egy MCP-szerver a **localhost:8081**-on, egy Postgres-adatbázis, és egy helyi munkacsomópont kész cBot-ok építésére és backtestjére. Mindez az Ön gépén, mindez az enyé.
:::

**Mielőtt elkezdené, az alábbiak egyike szükséges:**

- **Csak Docker** → használja az A opciót (nincs szükség .NET SDK-ra). Ajánlott az első pillantáshoz.
- **.NET 10 SDK + Docker** → használja a B opciót, ha a kódban szeretne hackelni.

Mindkét útvonal keresztplatformos (Windows / macOS / Linux).

## A lehetőség — Docker Compose (nincs szükség .NET SDK-ra)

Előfeltétel: Docker Desktop (vagy Docker Engine + compose plugin).

```bash
cp .env.example .env        # szerkessze a PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD értékeket
docker compose up --build
```

- Web UI: <http://localhost:8080> (jelentkezzen be a `.env` tulajdonosával; első bejelentkezéskor jelszóváltásra kényszerített).
- MCP szerver: <http://localhost:8081/mcp>.
- Postgres-adatok a `pgdata` kötetben maradnak; a séma automatikusan frissül az indításkor.

A Web-konténer a gazdagép Docker-aljátékát (`/var/run/docker.sock`) csatlakoztatja, így a böngészőbeli szerkesztő és mag a **LocalNode** cTrader Console-konténereket az Ön gépén építi és futtatja.

**Keresztplatformos megjegyzések**
- Docker Desktop (Windows/macOS) a `/var/run/docker.sock` címen teszi elérhetővé a aljátékot — az összeállítás csatlakoztatása olyan van-is.
- Linux: győződjön meg arról, hogy a felhasználó hozzáfér az aljátékhoz, vagy futtasson összeállítást kellő jogosultsággal.
- A Web-kép a `linux/amd64`; az Apple Silicon Docker alatt emulálódik.

Állítsd meg és töröld:

```bash
docker compose down          # adatok megtartása
docker compose down -v       # az adatbázis-kötet törlése is
```

## B lehetőség — .NET Aspire (fejlesztéshez)

Előfeltétel: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Az Aspire a Postgres-t, Web-et, MCP-t, pgAdmin-t orkesztrálja; összeköti a kapcsolati karakterláncokat + OTLP-t; megnyit egy irányítópultot. Állítsa be a tulajdonos hitelesítő adatait az Aspire-paraméterekként (`OwnerEmail`, `OwnerPassword`).

Csak webalkalmazás futtatása a meglévő Postgres ellen:

```bash
dotnet run --project src/Web
```

## Munkacsomópontok hozzáadása helyileg

A mag LocalNode már működteti a munkát az Ön gépén. Az **auto-felderítés** gyakorlásához helyileg indítsa el a node-ügynököt, amely a Web-alkalmazásra mutat (lásd [csomópont-felderítést](../operations/node-discovery.md)) a `NodeAgent:MainUrl=http://host.docker.internal:8080` értékkel és a megfelelő `JoinToken` értékkel.

## Hibaelhárítás 🔧

A Docker-nek van véleménye. Íme a szokásos gyanúsítottak:

| Tünet | Valószínű ok és fix |
|---|---|
| `port is already allocated` a 8080/8081 címen | Valami mást használ a portot. Állítsd meg, vagy módosítsd a `docker-compose.yml`-ben lévő hozzárendelést. |
| Web elindul, de az építések/backtestjek meghiúsulnak | A Docker-aljáték nincs csatlakoztatva vagy nem érhető el. Linux-on győződjön meg arról, hogy a felhasználó elérheti a `/var/run/docker.sock` címet. |
| `permission denied` az aljáték (Linux) | Adjon hozzá felhasználót a `docker` csoporthoz (`sudo usermod -aG docker $USER`) és jelentkezzen be újra, vagy futtasson kellő jogosultsággal. |
| Nagyon lassú első futás | Az első építés lekéri a képeket és lefordítja — az azt követő futtatások sokkal gyorsabbak. Az Apple Silicon `linux/amd64` webes képe emuláció alatt fut. |
| Nem tudom bejelentkezni | Ellenőrizze az `OWNER_EMAIL` / `OWNER_PASSWORD` értékeket a `.env` fájlban. Az első bejelentkezés jelszóváltásra kényszerít. |
| Adatbázis-furcsaságok frissítések után | `docker compose down -v` kitörlödi a kötetet a tiszta lapért (elveszítse a helyi adatokat). |

Még mindig elakadt? [Nyisson Vitát](https://github.com/amusleh-spotware-com/cmind/discussions) — barátságosak vagyunk. Következő megállás: [valódi telepítés →](./cloud.md)
