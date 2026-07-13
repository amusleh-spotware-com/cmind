---
title: Dashboard
description: "A cMind dashboard — egy élő, mobil-először parancsközpont a cBot futtatásokhoz, backtesztekhez, erőforrásokhoz és node klaszterhez."
---

# Dashboard 📊

Az első dolog, amit a bejelentkezés után látsz, és őszintén az az oldal, amit egész nap nyitva hagysz. A landing oldal (`/`, `Components/Pages/Index.razor`) egy **élő, mobil-először parancsközpont** a bejelentkezett felhasználó tevékenysége számára a cBot futtatások, backtesztek, erőforrások és (adminoknak) a node klaszter területén. Frissíti magát, jól néz ki egy telefonton, és soha nem kényszerít F5-re.

## Mit mutat

Felülről lefelé, prioritás szerint rendezve telefonra (minden blokk teljes szélességű stack elem mobilnézetben, responszív rács tableten/asztali gépen):

1. **Fejléc** — cím, egy élő indikátor (egy valódi pulzáló pont; statikus `prefers-reduced-motion` alatt), az utolsó frissítés ideje, és egy **periódus toggle** (`1H · 24H · 7D · 30D`), amely a KPI-kat és a chartot vezérli.
2. **Hero KPI-k** — négy pillantásnyi kártya, mindegyik egy nagy szám + egy beágyazott SVG sparkline, és (ahol értelmezhető) egy **delta az előző periódushoz képest**:
   - **Aktív most** — jelenleg induló/futó futtatások + backtesztek.
   - **Sikerességi ráta** — befejezett ÷ (befejezett + sikertelen) az időszakra; delta százalékpontokban.
   - **Befejezve** — befejezett futtatások/backtesztek ebben az időszakban; delta az előző időszakhoz.
   - **Sikertelen** — hibák ebben az időszakban; delta (a kevesebb jobb, tehát csökkenés zölden jelenik meg).
3. **Tevékenység chart** — egy ApexCharts terület timeline az indított / befejezett / sikertelen per időbuckettárolva.
4. **Instance státusz gyűrű** — egy donut a futó / backteszt / függőben lévő / befejezett / sikertelen arányairól, összesen a közepén.
5. **Backtesztek** — egy három csempe snapshot (futó / befejezett / sikertelen), átkattintás `/backtest`-re.
6. **Copy trading** — a másolási profiljaid egy élő státusz ponttal, cél számossal és egy **Élő** badge-vel a futó profilokon; átkattintás `/copy-trading`-re.
7. **AI ügynökök** — a persona-vezérelt kereskedési ügynökeid futási állapottal (archetípus · státusz) és utolsó-akció idővel; átkattintás `/agent-studio`-ra.
8. **Élő tevékenységi feed** — a 20 legutóbbi esemény (legújabb elöl) státusz-színű ponttal és relatív időbélyeggel.
9. **Klaszter egészség** (csak adminok) — aktív-vs-összes node és kapacitás-használat mérő.
10. **Erőforrás csempék** — cBot-ok, kereskedési számlák, cTrader ID-k, MCP kulcsok (átkattintás az oldalaikra).

## Testreszabd a dashboardot

A fenti minden blokk egy **widget, amit te irányítasz**. Nyomd meg a **Testreszabás**-t (a fejléc jobb felső sarkában) egy dialog megnyitásához, ahol **megmutathatod/elrejtheted** bármely widgetet és **átrendezheted** őket fel/le nyilakkal. **Visszaállítás alapértelmezésre** visszaállítja a katalógus sorrendet. A választásod **szerver oldalon per felhasználó perzisztálódik**, tehát követ a böngészők és eszközök között — nem csak ebben a tabban.

- A funkció-kapuzott és admin-kizárólagos widgetek (Copy trading, AI ügynökök, Klaszter egészség) csak akkor jelennek meg a dialogban, ha a telepítésed/szereped használhatja őket.
- A widget katalógus egyetlen forrása valóság `Core/Dashboard/DashboardWidgets.cs`-ben; a prezentáció (címke + ikon + elérhetőség) `Components/Dashboard/DashboardWidgetMeta.cs`-ben él.

## Hogyan marad élő

Az oldal 10 másodpercenként poll-olja a `GET /api/dashboard/overview?period=<1h|24h|7d|30d>`-et és újrarajzolja a widgeteket a helyükön — nincs kézi újratöltés. Egy átmeneti fetch hiba el van nyelve és újrapróbálkozik a következő tick-en; a hurok tiszta leáll dispose-kor. Az első betöltés skeletont mutat; egy tartós hiba hibakártyát mutat **Újrapróbálkozás**-sal; egy felhasználó, akinek nincs adata, nullázott KPI-kat és üres-állapotú copy-t lát.

## Backend

- `Endpoints/DashboardEndpoints.cs` mapolja a `/overview`-ot (és tartja a régebbi skalár `/stats`-ot). Per-felhasználó és admin-kapuzott `ICurrentUser` révén; az óra `TimeProvider`-ből jön. Emellett mapolja a `GET/PUT /api/dashboard/layout`-ot — a felhasználó widget elrendezése, betöltve az oldal indításakor és mentve a Testreszabás dialogból.
- **Elrendezés perzisztencia** a `UserDashboard` aggregátum (`Core/Dashboard/UserDashboard.cs`): egy dashboard per felhasználó (`UserId`-n egyedi), birtokában a widget beállítások rendezett listájának (látható + sorrend) `jsonb` oszlopként tárolva. A rendezett lista csak az `Apply` / `Reset` révén van mutálva, amelyek minden kulcsot a `DashboardWidgets` katalógus ellen validálnak és a kollekciót teljes és de-duplikáltan tartják. Ismeretlen kulcsok `DomainException`-nel elutasítva → `400`.
- `Endpoints/DashboardQuery.cs` építi a kompozit `DashboardOverview` olvasási modellt: egy all-time státusz pillanatfelvételt (csoportosított counts), egy ablakolt instance halmazt once materializálva, és resource/node counts-okat. Az instance státusz és terminal timestamps a TPH altípusokon élnek (nem oszlopokon), így a sorok memóriában olvashatók a megosztott `InstanceEndpoints.GetStartedAt/GetStoppedAt` helper-eken keresztül. Esemény ideje = `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` tartalmazza a DTO-kat, a period→(ablak, bucket-count) tervet és `DashboardMath`-ot — tiszta, determinisztikus bucketing + KPI/delta matek (nincs I/O, a `now` be van adva).

A KPI delták az aktuális ablakot a közvetlenül megelőzővel hasonlítják össze (a query dupla ablakot kér le erre). **Nincs élő számla P&L feed** — a platform csak backtesztek és prop-firm tracking esetén van equityje — tehát a dashboard szándékosan *operatív* (tevékenység, átfutás, sikerességi ráta), nem bróker egyenleg ticker.

## Design & tokenek

Minden szín a design tokenekből jön (`var(--app-success|-warning|-error|-info|-primary|-text*)`), így egy white-label paletta ingyen átfolyik — beleértve a chartot, amelynek sorozat színei futási időben a feloldott tokenekből olvashatók a `window.appReadTokens` révén (az SVG nem tudja közvetlenül fogyasztani a CSS változókat). Nincs hardcoded hex a dashboardban sehol. Lásd [../ui-guidelines.md](../ui-guidelines.md).

## A "Powered by cMind" link

A dashboard egy kicsi, ízléses **"Powered by cMind"** linket mutat, amely erre a dokumentációs oldalra mutat. **Alapértelmezés szerint megjelenik** — büszkék vagyunk a projektre és segít más kereskedőknek megtalálni — de teljesen a te döntésed. Viszonteladók, akik teljesen white-labeled instance-ot futtatnak, átállítják az `App:Branding:ShowSiteLink`-et `false`-ra és eltűnik. Lásd [White-label branding](./white-label.md#powered-by-link).

## Tesztek

- **Egység-stílusú** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, sikerességi ráta, előző-periódus delták, period parsing, üres/határérték (esemény `now`-kor, osztás-nullával védelem).
- **Egység** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — a `UserDashboard` aggregátum: alapértelmezett seed, apply sorrend/láthatóság, append-omitted, duplicate-collapse, ismeretlen-kulcs elutasítás, reset.
- **Integráció** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — az olvasási modell valódi Postgres ellen (státusz/KPI-k/tevékenység/erőforrások, admin node health, üres-felhasználó útvonal), az új backtests/copy-profiles/agents szekciók, és egy elrendezés **round-trip** (mentés → újratöltés → sorrend + láthatóság perzisztálva).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — asztali + mobil: KPI kártyák, chart, gyűrű és feed renderelés; a periódus toggle váltja az aktív periódust és újratölti; egy KPI átfúr `/run`-ra; **widget elrejtése perzisztálódik újratöltés felett**, **Visszaállítás** visszahozza, és a Testreszabás dialog működik telefalon vízszintes túlcsordulás nélkül. `/` szintén benne van a `PageSmokeTests`-ben, `MobileLayoutTests` (shell + no-overflow) és `MobileJourneyTests`-ben.
