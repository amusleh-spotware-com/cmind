---
description: "Egy cTrader Open API alkalmazást szallitani minden felhasznalonak (white-label megosztott mod), a single redirect URL-t regisztralni, es per-message-type kliens rate limiteket."
---

# Megosztott Open API alkalmazas es rate limitek

Alapertelmezes szerint minden felhasznalo a **sajat** cTrader Open API alkalmazast regisztralja a **Beallitasok → Open API** alatt. Egy white-label operator (tipikusan egy cTrader broker vagy viszontelado) helyette **egy megosztott Open API alkalmazast szallithat minden felhasznalonak** - senki nem regisztralja a sajatjat; mindenki engedelyezi a szamlait az operator egyetlen alkalmazasan at.

## Ket ut a megosztott alkalmazas biztositashoz

A megosztott alkalmazas vagy telepitesi konfig-bol **vagy** a tulajdonos beallitasok UI-bol van provizionalva (a tulajdonos altal beallitott ertek nyer). Add meg egyszer es a megosztott mod mindenki szamara bekapcsol.

### 1. Telepitesi konfig (vetitve inditasnal)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // kanonikus publikus URL EBBOL a telepitesbol
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // titkositva nyugalomban; sosem naplozva
    }
  }
}
```

Inditasnal az alkalmazas seed-el egy megosztott alkalmazast a tulajdonos fiok alatt (idempotent - soha nem ir felul egy tulajdonos altal szerkesztett runtime erteket, es a re-seeding no-op).

### 2. Tulajdonos beallitasok (runtime, nincs redeploy)

**Beallitasok → Open API** (csak tulajdonos) ket dolgot mutat: egy **Sajat Open API alkalmazas** szakasz — a tulajdonos regisztralja, szerkeszti es engedelmezi a **sajat** per-felhasznalo alkalmazasat, pontosan mint barmelyik felhasznalo (elerheto, amig nincs megosztott alkalmazas konfiguralsva) — es egy **Deployment megosztott alkalmazas** kartya a megosztott alkalmazas hozzaadasahoz / szerkesztesehez / torlesehez, a redirect URL megjelenitve masolas-illestzeshez. A valtoztatasok az uj engedelyezesekhez azonnal hatasosak. Amint egy megosztott alkalmazas konfiguralsva van, felulirja a tulajdonos sajat alkalmazasat, es a **Sajat Open API alkalmazas** szakasz egy ertesitesre valt, hogy a fiok mostantol a megosztott alkalmazason keresztul engedelyeznek.

## A redirect URL (regisztrald a cTrader-ben)

Minden cTrader Open API alkalmazas **egy** redirect URL-t regisztral - **ugyanazt az egyetlen erteket** a megosztott alkalmazashoz es barmely per-user alkalmazashoz:

```
{your deployment URL}/openapi/callback
```

peldaul `https://cmind.yourbroker.com/openapi/callback`.

- Az alkalmazas **megjeleniti a pontos erteket** az Open API beallitasok oldalon (masolo gombbal) - illesztd be a cTrader partner portalba, amikor letrehozod az Open API alkalmazast.
- Ossze van allitva az `App:OpenApi:PublicBaseUrl`-bol, igy stabil marad egy reverse proxy / CDN mogott; amikor az nincs beallitva, az bejaro keres host-re esik vissza.
- Az invite vs normal-user elmeny csak abban kulonbozik, hogy a felhasznalo hol landol **utana** a callback-nel (a szamlak listaja vs egy "szamlak hozzaadva" megerosites) - a regisztralt redirect URL valtozatlan.

## Amit a felhasznalok latnak megosztott modban

Amikor egy megosztott alkalmazas letezik:

- A felhasznalok **nincs lehetoseguk** sajat Open API alkalmazast regisztralni - a beallitasok oldal **"Az Open API-t a szolgaltatod kezeli"**-t mutat es egy **Engedelyezes szamlak** gombot, ami a megosztott alkalmazast hasznalja.
- Minden mar meglevo szemelyes alkalmazas **el van tavolitva**; az engedelyezett szamlak atvannak iranyitva a megosztott alkalmazasra es **ujra engedelyezesre** van szukseguk (a regi tokenek egy masik kliens id alatt lettek kibocsajtva). Szemelyes alkalmazas letrehozasa megprobalasa egy "a szolgaltatod kezeli" hibaval ter vissza.

## Kliens rate limitek (per message tipus)

A kliens lepteti a kulso cTrader Open API uzeneteket, igy egy burst soha nem trip-el egy szerveroldali rate-limit block-ot. A limitek **per message tipusok**, megfeleloen a cTrader Open API dokumentumoknak:

| Kategori | Mit fed le | Alapertelmezes |
|---|---|---|
| `General` | kereskedes + olvasasi uzenetek (megbizások, szimbulumok, szamla lekerdezesek) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data lekerdezesek (cTrader altal nehezebben szabalyozva) | 5 msg/s |

Egy historical-data lekerdezes mindket sajat fuggojenyehez szamol - es a general fuggojenyhezis. Heartbeat es hitelesitesi uzenetek sosem leptetettek. Az uzenetek sorba allnak es uritodnek az elerheto rate-en - semmi nincs eldobva es a sorrend megmarad.

Hangold oket, ha a brokerod magasabb cTrader limiteket alkudott ki, vagy allits egy kategoriat **`0`**-ra a pacing teljes kikapcsolashoz (korlatlan):

- **Konfig:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Tulajdonos beallitasok:** a **Kliens rate limitek** kartya a **Beallitasok → Open API**-n (tulajdonos feluliras, az uj kapcsolatokra / reconnect-nel alkalmazodik).
