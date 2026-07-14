---
title: Eseguilo localmente
description: Ottieni cMind in esecuzione sulla tua macchina in pochi minuti con Docker Compose (o .NET Aspire per lo sviluppo).
sidebar_position: 1
---

# Esegui cMind localmente

Questo è il modo più veloce per vedere cMind per davvero — un'istanza completa sulla tua macchina. Prendi un caffè; probabilmente sarai autenticato prima che si sia raffreddato.

:::tip[Quello che avrai alla fine]
Un'app web in esecuzione a **localhost:8080**, un server MCP a **localhost:8081**, un database Postgres e un nodo di lavoro locale pronto per compilare e eseguire il backtest dei cBot. Tutto sulla tua macchina, tutto tuo.
:::

**Prima di iniziare, hai bisogno di uno di:**

- **Solo Docker** → usa Opzione A (nessun SDK .NET richiesto). Consigliato per una prima occhiata.
- **.NET 10 SDK + Docker** → usa l'Opzione B se vuoi hackerare il codice.

Entrambi i percorsi sono cross-platform (Windows / macOS / Linux).

## Opzione A — Docker Compose (nessun SDK .NET richiesto)

Prerequisito: Docker Desktop (o Docker Engine + plugin compose).

```bash
cp .env.example .env        # modifica PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (accedi con owner da `.env`; forzato a cambiare password al primo accesso).
- Server MCP: <http://localhost:8081/mcp>.
- I dati Postgres persistono nel volume `pgdata`; lo schema migra automaticamente all'avvio.

Il contenitore Web monta il socket Docker dell'host (`/var/run/docker.sock`) quindi il builder in-browser e il **LocalNode** seminato compilano e eseguono contenitori cTrader Console sulla tua macchina.

**Note cross-platform**
- Docker Desktop (Windows/macOS) espone il socket a `/var/run/docker.sock` — il mount compose funziona così.
- Linux: assicurati che il tuo utente possa accedere al socket o esegui compose con privilegi sufficienti.
- L'immagine web è `linux/amd64`; su Apple Silicon Docker la esegue sotto emulazione.

Ferma e pulisci:

```bash
docker compose down          # mantieni i dati
docker compose down -v       # cancella anche il volume del database
```

## Opzione B — .NET Aspire (per lo sviluppo)

Prerequisito: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orchestra Postgres, Web, MCP, pgAdmin; cablatura stringhe di connessione + OTLP; apre il dashboard. Imposta le credenziali del proprietario come parametri Aspire (`OwnerEmail`, `OwnerPassword`).

Esegui solo l'app web rispetto a Postgres esistente:

```bash
dotnet run --project src/Web
```

## Aggiunta di nodi di lavoro localmente

LocalNode seminato già esegue lavoro sulla tua macchina. Per esercitare **auto-discovery** localmente, avvia l'agente del nodo puntato all'app Web (vedi [node discovery](../operations/node-discovery.md)) con `NodeAgent:MainUrl=http://host.docker.internal:8080` e `JoinToken` corrispondente.

## Risoluzione dei problemi

Docker ha opinioni. Questi sono i soliti sospetti:

| Sintomo | Probabile causa e correzione |
|---|---|
| `port is already allocated` su 8080/8081 | Qualcos'altro sta usando la porta. Fermalo o cambia il mapping in `docker-compose.yml`. |
| Web inizia ma build/backtest fallisce | Il socket Docker non è montato o accessibile. Su Linux, assicurati che il tuo utente possa raggiungere `/var/run/docker.sock`. |
| `permission denied` sul socket (Linux) | Aggiungi il tuo utente al gruppo `docker` (`sudo usermod -aG docker $USER`) e ri-accedi, o esegui con privilegi sufficienti. |
| Primo run molto lento | Il primo build estrae immagini e compila — i run successivi sono molto più veloci. Su Apple Silicon l'immagine web `linux/amd64` viene eseguita sotto emulazione. |
| Non riesci ad accedere | Controlla `OWNER_EMAIL` / `OWNER_PASSWORD` nel tuo `.env`. Il primo accesso forza un cambio di password. |
| Strane cose con il database dopo gli aggiornamenti | `docker compose down -v` cancella il volume per una lavagna pulita (perderai i dati locali). |

Ancora bloccato? [Apri una discussione](https://github.com/amusleh-spotware-com/cmind/discussions) — siamo amichevoli. Prossima tappa: [distribuisci davvero →](./cloud.md)
