---
title: Spustite lokálne
description: Spustite cMind na svojom vlastnom počítači za pár minút s Docker Compose (alebo .NET Aspire pre development).
sidebar_position: 1
---

# Spustite cMind lokálne 🖥️

Toto je najrýchlejší spôsob, ako vidieť cMind naozaj — plná inštancia na vašom vlastnom počítači. Chyťte si kávičku;
pravdepodobne budete prihláseníed pred tým, ako bude cool.

:::tip[Čo budete mať na konci]
Bežiaca web aplikácia na **localhost:8080**, MCP server na **localhost:8081**, Postgres databáza
a local worker node pripravený stavať a backtestovať cBots. Všetko na vašom počítači, všetko vaše.
:::

**Pred začatím potrebujete jednu z:**

- **Len Docker** → použite Option A (žiadny .NET SDK potrebný). Odporúčané pre prvý pohľad.
- **.NET 10 SDK + Docker** → použite Option B, ak chcete hackovať kód.

Obe cesty sú cross-platform (Windows / macOS / Linux).

## Option A — Docker Compose (žiadny .NET SDK potrebný)

Prereq: Docker Desktop (alebo Docker Engine + compose plugin).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (prihlaste sa vlastníkom z `.env`; nútené zmeniť heslo pri prvom login).
- MCP server: <http://localhost:8081/mcp>.
- Postgres data persists v `pgdata` volume; schéma sa migruje automaticky pri startup.

Web kontajner mountuje host Docker socket (`/var/run/docker.sock`), takže in-browser builder a seeded **LocalNode** build + run cTrader Console kontajnery na vašom počítači.

**Cross-platform notes**
- Docker Desktop (Windows/macOS) vystavuje socket na `/var/run/docker.sock` — compose mount funguje as-is.
- Linux: ubezpečte sa, že váš používateľ má prístup k socket alebo run compose s dostatočnými právami.
- Web image je `linux/amd64`; na Apple Silicon Docker to spúšťa pod emulation.

Stop a wipe:

```bash
docker compose down          # keep data
docker compose down -v       # also delete the database volume
```

## Option B — .NET Aspire (pre development)

Prereq: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orchestruje Postgres, Web, MCP, pgAdmin; zapojuje connection strings + OTLP; otvára dashboard. Nastavte owner poverenia ako Aspire parametre (`OwnerEmail`, `OwnerPassword`).

Spustite len web app protiv existing Postgres:

```bash
dotnet run --project src/Web
```

## Pridanie worker nodes lokálne

Seeded LocalNode už spúšťa prácu na vašom počítači. Aby ste cvičili **auto-discovery** lokálne, spustite node agent nasmerovaný na Web app (pozrite [node discovery](../operations/node-discovery.md)) s `NodeAgent:MainUrl=http://host.docker.internal:8080` a matching `JoinToken`.

## Troubleshooting 🔧

Docker má názory. Tu sú obvyklí podozrivci:

| Príznak | Pravdepodobná príčina & fix |
|---|---|
| `port is already allocated` na 8080/8081 | Niečo iné používa port. Zastavte to alebo zmeňte mapping v `docker-compose.yml`. |
| Web starts ale builds/backtests fail | Docker socket nie je mounted alebo accessible. Na Linux, ubezpečte sa, že váš používateľ môže dosiahnuť `/var/run/docker.sock`. |
| `permission denied` na socket (Linux) | Pridajte svojho používateľa do `docker` skupiny (`sudo usermod -aG docker $USER`) a re-login alebo spustite s dostatočnými právami. |
| Veľmi pomalý prvý run | Prvý build pull images a compile — ďalšie runs sú omnoho rýchlejšie. Na Apple Silicon `linux/amd64` web image beží pod emulation. |
