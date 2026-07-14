---
title: Spusťte to lokálně
description: Rozjeďte cMind na vlastním počítači během několika minut s Docker Compose (nebo .NET Aspire pro vývoj).
sidebar_position: 1
---

# Spusťte cMind lokálně

Toto je nejrychlejší způsob, jak cMind vidět naživo — celá instance na vašem vlastním počítači. Dejte si kafe;
nejspíš budete přihlášení dřív, než vychladne.

:::tip[Co budete mít na konci]
Běžící webovou aplikaci na **localhost:8080**, MCP server na **localhost:8081**, Postgres databázi
a lokální worker node připravený buildovat a backtestovat cBots. Vše na vašem počítači, všechno vaše.
:::

**Než začnete, potřebujete jedno z:**

- **Pouze Docker** → použijte Možnost A (není potřeba .NET SDK). Doporučeno pro první náhled.
- **.NET 10 SDK + Docker** → použijte Možnost B pokud chcete upravovat kód.

Obě cesty jsou cross-platform (Windows / macOS / Linux).

## Možnost A — Docker Compose (není potřeba .NET SDK)

Požadavky: Docker Desktop (nebo Docker Engine + compose plugin).

```bash
cp .env.example .env        # upravte PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (přihlaste se jako owner z `.env`; při prvním přihlášení vynucena změna hesla).
- MCP server: <http://localhost:8081/mcp>.
- Postgres data přetrvávají v `pgdata` volume; schéma se automaticky migrje při startu.

Web container mountuje host Docker socket (`/var/run/docker.sock`) takže in-browser builder a seeded **LocalNode** buildují a spouštějí cTrader Console containery na vašem počítači.

**Cross-platform poznámky**
- Docker Desktop (Windows/macOS) vystavuje socket na `/var/run/docker.sock` — compose mount funguje jak je.
- Linux: ujistěte se, že váš uživatel může přistupovat k socketu, nebo spusťte compose s dostatečnými privilegii.
- Web image je `linux/amd64`; na Apple Silicon Docker běží pod emulací.

Stop a wipe:

```bash
docker compose down          # zachovat data
docker compose down -v       # také smazat databázový volume
```

## Možnost B — .NET Aspire (pro vývoj)

Požadavky: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orchestruje Postgres, Web, MCP, pgAdmin; propojí connection strings + OTLP; otevře dashboard. Nastavte owner credentials jako Aspire parametry (`OwnerEmail`, `OwnerPassword`).

Spusťte pouze webovou aplikaci proti existujícímu Postgres:

```bash
dotnet run --project src/Web
```

## Přidání worker nodů lokálně

Seeded LocalNode již spouští práci na vašem počítači. Pro procvičení **auto-discovery** lokálně, startujte node agenta namířeného na Web aplikaci (viz [node discovery](../operations/node-discovery.md)) s `NodeAgent:MainUrl=http://host.docker.internal:8080` a odpovídajícím `JoinToken`.

## Řešení problémů

Docker má své názory. Zde jsou obvyklí podezřelí:

| Symptom | Pravděpodobná příčina a oprava |
|---|---|
| `port is already allocated` na 8080/8081 | Něco jiného používá port. Zastavte to, nebo změňte mapování v `docker-compose.yml`. |
| Web startuje ale buildy/backtesty selžou | Docker socket není namontován nebo nepřístupný. Na Linuxu se ujistěte, že váš uživatel může dosáhnout `/var/run/docker.sock`. |
| `permission denied` na socketu (Linux) | Přidejte svého uživatele do `docker` skupiny (`sudo usermod -aG docker $USER`) a znovu se přihlaste, nebo spusťte s dostatečnými privilegii. |
| Velmi pomalý první běh | První build stahuje image a kompiluje — další běhy jsou mnohem rychlejší. Na Apple Silicon běží `linux/amd64` web image pod emulací. |
| Nemůžete se přihlásit | Zkontrolujte `OWNER_EMAIL` / `OWNER_PASSWORD` v `.env`. První přihlášení vynucuje změnu hesla. |
| Podivné chování databáze po upgradu | `docker compose down -v` smaže volume pro čistý začátek (přijdete o lokální data). |

Stále zaseknuti? [Otevřete Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) — jsme
přátelští. Další krok: [nasazení do reálu →](./cloud.md)
