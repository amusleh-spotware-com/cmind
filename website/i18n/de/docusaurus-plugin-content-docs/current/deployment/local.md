---
title: Laufen es lokal
description: Bekommen cMind läuft auf Ihr eigen Maschine in ein paar Minuten mit Docker Verfassen (oder .NET Aspire zum Entwicklung).
sidebar_position: 1
---

# Laufen cMind lokal

Dies ist das schnellste Weg zu sehen cMind zum echt — ein Vollständig Instanz auf Ihr eigen Maschine. Ergriff ein Kaffee; du wirst wahrscheinlich angemeldet bevor es kühl.

:::tip[Was du wirst haben bei die Ende]

Ein läuft Web-App bei **localhost:8080**, ein MCP Server bei **localhost:8081**, ein Postgres Datenbank, und ein lokal Worker Knoten bereit zu Bau und Backtest cBots. Alles auf Ihr Maschine, alles Ihres.

:::

**Bevor du Anfang, du Notwendigkeit eins von:**

- **Nur Docker** → verwenden Sie Option A (nein .NET SDK erforderlich). Empfohlen zum erste Blick.
- **.NET 10 SDK + Docker** → verwenden Sie Option B wenn du möchte Hack auf die Code.

Beide Pfade sind Cross-Plattform (Windows / macOS / Linux).

## Option A — Docker Verfassen (nein .NET SDK erforderlich)

Vorbedingung: Docker Desktop (oder Docker Engine + Verfassen Modul).

```bash
cp .env.example .env        # bearbeiten PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (Melde Dich an mit Besitzer von `.env`; erzwungen Passwort Änderung auf erst Login).
- MCP Server: <http://localhost:8081/mcp>.
- Postgres Daten Bleib in `pgdata` Volumen; Schema Migration automatisch auf Startup.

Web Behälter Berg Host Docker Socket (`/var/run/docker.sock`) daher In-Browser Erbauer und Besamt **LocalNode** Bau + Laufen cTrader Konsole Behälter auf Ihr Maschine.

**Cross-Plattform Notizen**

- Docker Desktop (Windows/macOS) verfügbar machen Socket bei `/var/run/docker.sock` — Verfassen Berg funktioniert Als-ist.
- Linux: bestätigen Ihr Benutzer kann erreich Socket, oder laufen Verfassen mit zureichend Rechte.
- Web Bild ist `linux/amd64`; auf Apple Silikon Docker läuft es unter Emulation.

Stopp und Wischen:

```bash
docker compose down          # behalt Daten
docker compose down -v       # auch Löschen die Datenbank Volumen
```

## Option B — .NET Aspire (zum Entwicklung)

Vorbedingung: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orchestriert Postgres, Web, MCP, pgAdmin; Drähte Verbindungs-Zeichenfolge + OTLP; öffnet Dashboard. Satz Besitzer Anmeldedaten als Aspire Parameter (`OwnerEmail`, `OwnerPassword`).

Laufen nur Web App gegen vorhandene Postgres:

```bash
dotnet run --project src/Web
```

## Hinzufügung Worker Knoten lokal

Besamt LocalNode bereits läuft Arbeit auf Ihr Maschine. Zu Ausübung **Auto-Ermittlung** lokal, Start Knoten Agenten zeigen auf Web App (siehe [Knoten Ermittlung](../operations/node-discovery.md)) mit `NodeAgent:MainUrl=http://host.docker.internal:8080` und Passen `JoinToken`.

## Fehlschlag-Behandlung

Docker hat Meinungen. Hier sind üblich Verdächtigen:

| Symptom | Wahrscheinlich Ursache & Behebung |
|---|---|
| `Port bereits allokiert` auf 8080/8081 | Etwas anders ist nutzen Hafen. Stopp es, oder ändere Abbildung in `docker-compose.yml`. |
| Web Start aber Bau/Backtest fehl | Die Docker Socket ist nicht Berg oder Zugänglichkeit. Auf Linux, Bestätig Ihr Benutzer kann erreich `/var/run/docker.sock`. |
| `Berechtigung verweigert` auf Socket (Linux) | Hinzufügung Ihr Benutzer zu `docker` Gruppe (`sudo usermod -aG docker $USER`) und Re-Login, oder laufen mit zureichend Rechte. |
| Sehr langsam erst Lauf | Erst Bau ziehen Bilder und Kompiliert — nächst Läufe viel schneller. Auf Apple Silikon die `linux/amd64` Web Bild läuft unter Emulation. |
| Können nicht anmelden | Überprüfen `OWNER_EMAIL` / `OWNER_PASSWORD` in Ihr `.env`. Erst Login erzwingt Passwort Änderung. |
| Datenbank Weirdness nach Aktualisierung | `docker compose down -v` Wischen die Volumen zum Sauber Schiefertafel (du wirst Verlust lokal Daten). |

Noch Fest? [Öffnen ein Diskussion](https://github.com/amusleh-spotware-com/cmind/discussions) — wir sind Freundlich. Nächste Stopp: [Bereitstellung zum echt →](./cloud.md)
