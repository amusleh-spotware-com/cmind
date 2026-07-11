# Local deployment

Two supported paths. Both cross-platform (Windows/macOS/Linux).

## Option A — Docker Compose (no .NET SDK required)

Prereq: Docker Desktop (or Docker Engine + compose plugin).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (sign in with owner from `.env`; forced to change password on first login).
- MCP server: <http://localhost:8081/mcp>.
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

Seeded LocalNode already runs work on your machine. To exercise **auto-discovery** locally, start node agent pointing at Web app (see `docs/operations/node-discovery.md`) with `NodeAgent:MainUrl=http://host.docker.internal:8080` and matching `JoinToken`.