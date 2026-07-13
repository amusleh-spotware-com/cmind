---
title: Tečite ga lokalno
description: Dobite cMind, ki teče na svojem stroju v nekaj minutah z Docker Compose (ali .NET Aspire za razvoj).
sidebar_position: 1
---

# Tečite cMind lokalno 🖥️

To je najhitrejši način za pravi pogled cMind — polna instanca na svojem stroju. Vzemite kavo;
verjetno se boste prijavili, preden se ohladi.

:::tip Kaj boste imeli na koncu
Tekajoči spletni program na **localhost:8080**, strežnik MCP na **localhost:8081**, podatkovna baza
Postgres in lokalno delavsko vozlišče, pripravljeno za gradnjo in testiranje cBotov. Vse na
svojem stroju, vse je vaše.
:::

**Preden začnete, potrebujete eno izmed:**

- **Samo Docker** → uporabite Opcijo A (ni potreben .NET SDK). Priporočeno za prvi pogled.
- **.NET 10 SDK + Docker** → uporabite Opcijo B, če želite delati na kodi.

Obe poti sta platformsko neodvisni (Windows / macOS / Linux).

## Opcija A — Docker Compose (ni potreben .NET SDK)

Pogoj: Docker Desktop (ali Docker Engine + plugin za sestavo).

```bash
cp .env.example .env        # uredite PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD
docker compose up --build
```

- Uporabniški vmesnik splet: <http://localhost:8080> (prijavite se z lastnikom iz `.env`; silno
  spremenite geslo pri prvi prijavi).
- Strežnik MCP: <http://localhost:8081/mcp>.
- Podatki Postgres se ohranijo v glasnosti `pgdata`; shema se samodejno prenese ob zagonu.

Spletni kontejner montira host Docker vtičnico (`/var/run/docker.sock`), zato se graditelj v
brskalniku in posejano **LocalNode** zgradita + tečeta kontejnerje cTrader Console na vašem stroju.

**Opombe na osnovi platform**
- Docker Desktop (Windows/macOS) razkriva vtičnico pri `/var/run/docker.sock` — montaža sestave
  deluje kot je.
- Linux: zagotovite, da je vaš uporabnik lahko dostopi vtičnico, ali zaženite sestavo s
  dovolj privilegiji.
- Spletna slika je `linux/amd64`; na Apple Silicon Docker jo teče pod emulacijo.

Ustavite in obrišite:

```bash
docker compose down          # ohrani podatke
docker compose down -v       # tudi izbriši glasnost podatkovne baze
```

## Opcija B — .NET Aspire (za razvoj)

Pogoj: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire ureja Postgres, Web, MCP, pgAdmin; žice nizu povezav + OTLP; odpira nadzorno plošča. Nastavite
kredenciale lastnika kot parametre Aspire (`OwnerEmail`, `OwnerPassword`).

Tečite samo spletno aplikacijo pred obstoječim Postgres:

```bash
dotnet run --project src/Web
```

## Dodajanje delavskih vozlišč lokalno

Posejano LocalNode že teče delo na vašem stroju. Za vajo **samodejne odkrivanja** lokalno začnete
agenta vozlišča, ki kaže na spletno aplikacijo (glejte [odkrivanje vozlišča](../operations/node-discovery.md))
z `NodeAgent:MainUrl=http://host.docker.internal:8080` in ujemajočim `JoinToken`.

## Odpravljanje napak 🔧

Docker ima mnenja. Tukaj so običajni osumelci:

| Simptom | Verjetni vzrok in popravek |
|---|---|
| `port is already allocated` na 8080/8081 | Nekaj drugega uporablja vrata. Ga ustavite ali spremenite preslikavo v `docker-compose.yml`. |
| Splet se začne, vendar gradnje/testiranja ne uspejo | Vtičnica Docker ni montirana ali dostopna. Na Linuxu se prepričajte, da je vaš uporabnik lahko dostopi `/var/run/docker.sock`. |
| `permission denied` na vtičnici (Linux) | Dodajte svojega uporabnika skupini `docker` (`sudo usermod -aG docker $USER`) in se ponovno prijavite, ali tečite s dovolj privilegiji. |
| Zelo počasen prvi tek | Prva gradnja povleče slike in prevede — naslednji teki so precej hitrejši. Na Apple Silicon spletna slika `linux/amd64` teče pod emulacijo. |
| Ne morem se prijaviti | Preverite `OWNER_EMAIL` / `OWNER_PASSWORD` v vaši `.env`. Prva prijava prisili spremembo gesla. |
| Čudnost podatkovne baze po posodobitvah | `docker compose down -v` izbriše glasnost za čist list (izgubili boste lokalne podatke). |

Se še vedno zatikate? [Odprite razpravo](https://github.com/amusleh-spotware-com/cmind/discussions) — smo
prijazni. Naslednja postaja: [namestite za pravo →](./cloud.md)
