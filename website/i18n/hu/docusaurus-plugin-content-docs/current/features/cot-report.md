# Kereskedők Elkötelezettsége (COT)

A cMind tartalmaz egy beépített **Kereskedők Elkötelezettsége** jelentést — a CFTC heti bontása arról, hogy ki van hosszú és rövid pozícióban az amerikai határidős piacon (kereskedelmi fedezőket, nagy spekulánsok, alapok), interaktív történeti grafikonokkal, normalizált **COT indexszel**, hitelesített REST API-val cBotokhoz és MCP eszközökkel AI kliensek számára. Az adatok közvetlenül a **CFTC nyilvános Socrata adathalmazaiból** érkeznek — nincs API kulcs, nincs aggregátor. Az gazdasági naptárhoz hasonlóan ez egy lecsatolt modul, amely nulla hatással kikapcsolható a kereskedési magra.

## Mit ad neked

- **Mind a három jelentés család, csak határidősök és határidősök + opciók kombinálva:**
  - **Öröklött** — Nem kereskedelmi (nagy spekulánsok), Kereskedelmi (fedezők), Nem jelentendő.
  - **Szétválasztott** — Termelő/Kereskedő, Swap Kereskedők, Kezelt Pénz, Más jelentendő.
  - **Kereskedők Pénzügyi Határidősökben (TFF)** — Kereskedő, Eszközkezelő, Tőkeáttételes Alapok, Más jelentendő.
- **Kuratált piaci katalógus** — FX fő párok, arany/ezüst/réz, kőolaj és földgáz, Államkötvények, részvény indexek, kriptó és a fő gabonák/lágy áruk — mindegyik hozzárendelve annak a stabil CFTC szerződéskódhoz és, ahol egyértelmű, egy kereskedésre alkalmas szimbólumhoz (pl. Euro FX → `EURUSD`, Arany → `XAUUSD`).
- **A COT index (0–100)** — ahol az aktuális spekuláns nettó pozíciója az történeti tartományon belül helyezkedik el (alapértelmezett ~3 év visszatekintés). Az extrémek közeli leolvasások zsúfolt pozicionálást jeleznek, amely gyakran megelőz egy fordulást; a jelentés címkéz egy **hosszú szélsőséges** (≥80) vagy **rövid szélsőséges** (≤20).
- **Pontbeli idő helyessége.** Egy heti jelentést kedden mérnek, de csak a következő pénteken válnak nyilvánossá; minden olvasás tiszteletben tartja azt a kiadási pillanatot, így egy tesztelt pozicionálási jel soha nem látja a jelentést annak közzétételét megelőzően (nincs előrelátás).

## Az oldal használata

Nyissa meg a **Kereskedők Elkötelezettsége** a bal oldali navigációból. Válasszon egy **piacot**, egy **jelentés típust** (Örökölt / Szétválasztott / Pénzügyi) és kapcsolja be a **Határidősök + opciók** kapcsolót a csak határidősök és a kombinált változat közötti váltáshoz. Az oldal megjeleníti:

- **Nettó pozicionálás az idő múlásával** — az egyes kereskedő kategóriák nettó pozícióját (hosszú − rövid) interaktív vonaldiagram mutatja a történeti ablak során.
- **COT index** — a 0–100 index vonaldiagramja, a legutolsó leolvasással és annak szélsőséges címkéjével.
- **Legutolsó pillanatkép** — egy táblázat hosszú / rövid / nettó / nyílt kamat %-a kereskedő kategóriánként, plusz teljes nyílt kamat és a jelentés dátuma.

## Az adatok áramlása

Egy heti betöltési feldolgozó lekéri a hat CFTC adathalmazt a nyomon követett piacokra, frissíti a piaci katalógust és **idempotens** módon hozzáadja az egyes új jelentéseket (az újbóli futtatás soha nem duplikál egy pillanatképet). Az első futás több év történetét tölti fel; a később futások a legutóbbi heteket szinkronizálják újra a késői revíziók rögzítéséhez. Minden kulcs nélkül működik, egy opcionális Socrata alkalmazás token csak a sávszélesség korlátját növeli.

## Konfiguráció

Minden kulcs az `App:Cot` alatt található (lásd [funkció toggle-kat](./feature-toggles.md) és [fehér címke tulajdonosi beállítások](./white-label-owner-settings.md)):

| Kulcs | Alapértelmezett | Cél |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Legyen futtatva a heti betöltési feldolgozó. |
| `PollInterval` | `6h` | Milyen gyakran lekérdez a feldolgozó a CFTC adathalmazokra. |
| `BackfillYears` | `5` | Az első futáskor lekért történet évei. |
| `ReconcileLookbackWeeks` | `4` | Legutóbbi hetek szinkronizálása újra minden ciklusban a revíziók rögzítéséhez. |
| `SocrataAppToken` | — | Opcionális token, amely növeli a névtelen sávszélesség korlátját. |
| `CotIndexLookbackWeeks` | `156` | Heti jelentések, amelyeket COT-index tartományként (~3 év) használnak. |

## Kapu

A láthatóság kétszintű kapu, azonos a gazdasági naptárral: a fehér címke kemény kapu `App:Branding:EnableCot` (fordítási szint) **és** a futási időbeli funkció kapcsoló `App:Features:Cot`. Az egyik kikapcsolása esetén a navigációs hivatkozás, az oldal, a REST API és az MCP eszközök mind eltűnnek (az API `404` értéket ad vissza). Mivel az adatforrás kulcs nélküli, nincs adatforrás-kulcs kapu — engedélyezve azt jelenti, hogy látható.

## Fejlesztőknek

- Tartomány: `Core.Cot` — `CotMarket` és `CotReport` aggregátumok, `CotPositions` érték objektum, `CotIndexCalculator` tartomány szolgáltatás, és `ICotReports` / `ICotSource` portok.
- Infrastruktúra: `Infrastructure.Cot` — `CftcSocrataSource` anti-korrupciós elemzője, sávszélesség kapu, csak hozzáadott írási szolgáltatás, olvasási oldal és heti betöltési feldolgozó (EF `cot` séma).
- cBot & AI hozzáférés: a [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) és az MCP eszközök `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
