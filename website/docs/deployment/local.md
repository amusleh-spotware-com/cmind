---
title: Run it locally
description: Get cMind running on your own machine in a few minutes with Docker Compose (or .NET Aspire for development).
sidebar_position: 1
---

# Run cMind locally 🖥️

This is the fastest way to see cMind for real — a full instance on your own machine. Grab a coffee;
you&apos;ll likely be signed in before it&apos;s cool.

:::tip[What you&apos;ll have at the end]
A running web app at **localhost:8080**, an MCP server at **localhost:8081**, a Postgres database,
and a local worker node ready to build and backtest cBots. All on your machine, all yours.
:::

**Before you start, you need one of:**

- **Just Docker** → use Option A (no .NET SDK required). Recommended for a first look.
- **.NET 10 SDK + Docker** → use Option B if you want to hack on the code.

Both paths are cross-platform (Windows / macOS / Linux).

## Option A — Docker Compose (no .NET SDK required)

Prereq: Docker Desktop (or Docker Engine + compose plugin).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: [http://localhost:8080](http://localhost:8080) (sign in with owner from `.env`; forced to change password on first login).
- MCP server: [http://localhost:8081/mcp](http://localhost:8081/mcp).
- Postgres data persists in `pgdata` volume; schema migrates automatically on startup.

Web container mounts host Docker socket (`/var/run/docker.sock`) so in-browser builder and seeded **LocalNode** build + run cTrader Console containers on your machine.

**Cross-platform notes**
- Docker Desktop (Windows/macOS) exposes socket at `/var/run/docker.sock` — compose mount works as-is.
- Linux: ensure your user can access socket, or run compose with sufficient privileges.
- Web image is `linux/amd64`; on Apple Silicon Docker runs it under emulation.

Stop and wipe:

```bash
docker compose down          # keep data
docker compose down -v       # also delete the database volume
```

## Option B — .NET Aspire (for development)

Prereq: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orchestrates Postgres, Web, MCP, pgAdmin; wires connection strings + OTLP; opens dashboard. Set owner credentials as Aspire parameters (`OwnerEmail`, `OwnerPassword`).

Run just web app against existing Postgres:

```bash
dotnet run --project src/Web
```

## Adding worker nodes locally

Seeded LocalNode already runs work on your machine. To exercise **auto-discovery** locally, start node agent pointing at Web app (see [node discovery](../operations/node-discovery.md)) with `NodeAgent:MainUrl=http://host.docker.internal:8080` and matching `JoinToken`.

## Troubleshooting 🔧

Docker has opinions. Here are the usual suspects:

| Symptom | Likely cause & fix |
|---|---|
| `port is already allocated` on 8080/8081 | Something else is using the port. Stop it, or change the mapping in `docker-compose.yml`. |
| Web starts but builds/backtests fail | The Docker socket isn&apos;t mounted or accessible. On Linux, make sure your user can reach `/var/run/docker.sock`. |
| `permission denied` on the socket (Linux) | Add your user to the `docker` group (`sudo usermod -aG docker $USER`) and re-login, or run with sufficient privileges. |
| Very slow first run | First build pulls images and compiles — subsequent runs are much faster. On Apple Silicon the `linux/amd64` web image runs under emulation. |
| Can&apos;t sign in | Check `OWNER_EMAIL` / `OWNER_PASSWORD` in your `.env`. First login forces a password change. |
| Database weirdness after upgrades | `docker compose down -v` wipes the volume for a clean slate (you&apos;ll lose local data). |

Still stuck? [Open a Discussion](https://github.com/amusleh-spotware-com/cmind/discussions) — we&apos;re
friendly. Next stop: [deploy for real →](./cloud.md)