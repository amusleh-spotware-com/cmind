# Local deployment

Two supported paths. Both are cross-platform (Windows/macOS/Linux).

## Option A — Docker Compose (no .NET SDK required)

Prereq: Docker Desktop (or Docker Engine + compose plugin).

```bash
cp .env.example .env        # edit PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (sign in with the owner from `.env`; you'll be forced to change
  the password on first login).
- MCP server: <http://localhost:8081/mcp>.
- Postgres data persists in the `pgdata` volume; the schema migrates automatically on startup.

The Web container mounts the host Docker socket (`/var/run/docker.sock`) so the in-browser builder
and the seeded **LocalNode** can build and run cTrader Console containers on your machine.

**Cross-platform notes**
- Docker Desktop (Windows/macOS) exposes the socket at `/var/run/docker.sock` — the compose mount
  works as-is.
- Linux: ensure your user can access the socket, or run compose with sufficient privileges.
- The Web image is `linux/amd64`; on Apple Silicon Docker runs it under emulation.

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

Aspire orchestrates Postgres, Web, MCP and pgAdmin, wires connection strings and OTLP, and opens
the dashboard. Set owner credentials as Aspire parameters (`OwnerEmail`, `OwnerPassword`).

Run just the web app against an existing Postgres:

```bash
dotnet run --project src/Web
```

## Adding worker nodes locally

The seeded LocalNode already runs work on your machine. To exercise **auto-discovery** locally,
start a node agent pointing at the Web app (see `docs/operations/node-discovery.md`) with
`NodeAgent:MainUrl=http://host.docker.internal:8080` and a matching `JoinToken`.
