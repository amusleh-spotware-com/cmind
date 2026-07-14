---
title: Uruchom go lokalnie
description: Uruchom cMind na własnym komputerze w kilka minut z Docker Compose (lub .NET Aspire do rozwoju).
sidebar_position: 1
---

# Uruchom cMind lokalnie 🖥️

To najszybszy sposób, aby zobaczyć cMind naprawdę — pełna instancja na własnym komputerze. Weź kawę; prawdopodobnie będziesz zalogowany, zanim się schłodzi.

:::tip[Co będziesz mieć na koniec]
Uruchamiająca się aplikacja sieciowa na **localhost:8080**, serwer MCP na **localhost:8081**, baza danych Postgres i lokalny węzeł pracownika gotowy do kompilacji i backtestowania cBots. Wszystko na Twojej maszynie, wszystko twoje.
:::

**Zanim zaczniesz, potrzebujesz jednego z:**

- **Tylko Docker** → użyj Opcji A (brak .NET SDK wymagany). Rekomendowany na pierwszy przejrzysty.
- **.NET 10 SDK + Docker** → użyj Opcji B, jeśli chcesz hakowć kod.

Obie ścieżki są wieloplatformowe (Windows / macOS / Linux).

## Opcja A — Docker Compose (brak wymaganego .NET SDK)

Prereq: Docker Desktop (lub Docker Engine + wtyczka compose).

```bash
cp .env.example .env        # edytuj PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Web UI: <http://localhost:8080> (zaloguj się właścicielem z `.env`; zmuszony do zmiany hasła przy pierwszym logowaniu).
- Serwer MCP: <http://localhost:8081/mcp>.
- Dane Postgres utrwalają się w wolumenie `pgdata`; schemat migruje automatycznie przy uruchomieniu.

Kontener sieci montuje gniazdo Docker hosta (`/var/run/docker.sock`), więc builder wewnątrz przeglądarki i zasiane **LocalNode** kompiluj + uruchamiaj kontenery cTrader Console na Twojej maszynie.

**Notatki wieloplatformowe**
- Docker Desktop (Windows/macOS) udostępnia gniazdo na `/var/run/docker.sock` — montaż compose działa jak jest.
- Linux: upewnij się, że twój użytkownik może uzyskać dostęp do gniazda, lub uruchamiaj compose z wystarczającymi uprawnień.
- Obraz Web to `linux/amd64`; na Apple Silicon Docker uruchamia go pod emulacją.

Zatrzymaj i wyczyszcz:

```bash
docker compose down          # utrzymaj dane
docker compose down -v       # też usunąć wolumin bazy danych
```

## Opcja B — .NET Aspire (do rozwoju)

Prereq: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire orkiestruje Postgres, Web, MCP, pgAdmin; przewody ciągi połączenia + OTLP; otwiera pulpit. Ustaw poświadczenia właściciela jako parametry Aspire (`OwnerEmail`, `OwnerPassword`).

Uruchom tylko aplikację sieciową wobec istniejącego Postgres:

```bash
dotnet run --project src/Web
```

## Dodawanie węzłów pracownika lokalnie

Zasiane LocalNode już uruchamia pracę na Twojej maszynie. Do ćwiczenia **auto-odkrycia** lokalnie, uruchom agenta węzła wskazującego na aplikację Web (patrz [node discovery](../operations/node-discovery.md)) z `NodeAgent:MainUrl=http://host.docker.internal:8080` i dopasowaniem `JoinToken`.

## Rozwiązywanie problemów 🔧

Docker ma opinie. Oto zwykle podejrzane:

| Objaw | Prawdopodobna przyczyna i naprawa |
|---|---|
| `port is already allocated` na 8080/8081 | Coś innego używa portu. Zatrzymaj to, lub zmień mapowanie w `docker-compose.yml`. |
| Web zaczyna się, ale kompilacje/backtesty zawodzą | Gniazdo Docker nie jest zmontowane lub dostępne. Na Linuksie upewnij się, że twój użytkownik może osiągnąć `/var/run/docker.sock`. |
| `permission denied` na gniazdo (Linux) | Dodaj swojego użytkownika do grupy `docker` (`sudo usermod -aG docker $USER`) i zaloguj się ponownie, lub uruchom z wystarczającymi uprawnień. |
| Bardzo powolny pierwszy przebieg | Pierwsza kompilacja pobiera obrazy i kompiluje — kolejne przebiegi są znacznie szybsze. Na Apple Silicon obraz Web `linux/amd64` działa pod emulacją. |
| Nie mogę się zalogować | Sprawdzić `OWNER_EMAIL` / `OWNER_PASSWORD` w `.env`. Pierwsze logowanie zmusza zmianę hasła. |
| Dziwactwo bazy danych po uaktualnieniach | `docker compose down -v` wyciera wolumin dla czystego stanu (utracisz dane lokalne). |

Wciąż zablokowany? [Otwórz dyskusję](https://github.com/amusleh-spotware-com/cmind/discussions) — jesteśmy miłych. Następny przystanek: [wdrażaj naprawdę →](./cloud.md)
