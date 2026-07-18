---
description: "Tükrözd a master cTrader-fiókot egy vagy több slave-fiókra — brókerek között, cID-k között — cél-specifikus vezérléssel és pénzügyi szintű egyeztetéssel."
---

# Copy trading

Tükrözd a **master** cTrader-fiókot egy vagy több **slave**-fiókra — brókerek között, cID-k között — cél-specifikus vezérléssel és pénzügyi szintű egyeztetéssel.

## Concepts

- **Copy profil** — egy master (`SourceAccountId`) + egy vagy több **célfiók**. Életciklus: `Draft → Running → Paused → Stopped` (`Error` meghibásodás esetén). Aggregát gyöker: `CopyProfile` (tulajdonosa `CopyDestination`).
- **Célfiók** — egy slave fiók + teljes szabályrendszer a master másolásmódjához. Minden konfiguráció cél-specifikus, így egy master egyszerre táplálhat konzervatív és agresszív slave-fiókokat.
- **Copy engine host** — futó feldolgozó a profilhoz (`CopyEngineHost`). Feliratkozik a master végrehajtási streamre, minden eseményt alkalmaz minden célfiókra.
- **Supervisor** — `CopyEngineSupervisor`, háttérszolgáltatás minden csomóponton. Üzemelteti a hozzárendelt profilokat, öngyógyító klaszter-szinten (lásd [scaling](../deployment/scaling.md)).

## What gets mirrored

| Master esemény | Slave művelet |
|--------------|--------------|
| Market / market-range pozíció nyitása | Méretezett másolat megnyitása (forrás-pozíció ID-vel jelölve) |
| Limit / stop / stop-limit szünetelő megbízás | Megfelelő szünetelő megbízás elhelyezése, vivő a master stop-loss / take-profit |
| Szünetelő megbízás módosítása | Tükrözött szünetelő megbízás helyben módosítása (beleértve a stop-loss / take-profit) |
| Szünetelő megbízás törlése / lejárata | Tükrözött szünetelő megbízás törlése |
| Részleges zárás | Slave-pozíció azonos arányának zárása |
| Scale-in (mennyiség növekedése) | Hozzáadott mennyiség megnyitása (opcionális) |
| Stop-loss / trailing-stop módosítása | Slave-pozíció védelme módosítása |
| Teljes zárás | Slave-másolat zárása |

Minden másolat **forrás-pozíció/megbízás ID-vel jelölve**. Újracsatlakozás után a host az egyeztetésből visszaépíti az állapotot: megnyitja a másolatokat, amelyeket a master tart de a slave nem, bezárja a slave „árvákat" amelyeket a master már nem tart — **megkettőzés nélkül**.

## Creating a profile

A **New Profile** egy dedikált **teljes oldal** formot nyit meg (`/copy-trading/new`), nem egy dialógust — az opciókészlet elég nagy ahhoz, hogy egy oldal jobban olvasható legyen telefonon és asztalon. Mindent előre összeít: profil neve, forrás (master) fiók, célfiók (slave) fiók (többszörös kiválasztás **Select all** gombbal; választott master kizárva a slave-listából), + a teljes cél-specifikus opciókészlet. **Csak az Open API-n keresztül csatolt fiókok választhatók** meg masterként vagy célfióként — a másolás az Open API felé rendel megbízásokat, így egy kézileg hozzáadott (cID-csak) fiók nem tud másolni és nem jelenik meg; amikor nincsenek csatolva, az oldal egy Trading Accountsra mutató figyelmeztetést mutat. A méretezési módok, irány és a szimbólum-szűrő **emberi címkékként** jelennek meg egy **módspecifikus felsorolt magyarázattal** a pénzkezelés súgó tooltipjén. **Minden vezérlő tartalmaz egy súgó-tooltip-et** amely elmagyarázza, mit tesz és hogyan kell használni. A strukturált bemenetek **megfelelő validált vezérlőket** használnak — számok/százalékok numerikus mezőkön, módok/irány/szűrő választón keresztül, a szimbólum-szűrő szimbólum-chip-ek hozzáadás/eltávolítás listájával, és a szimbólum-mappa hozzáadás/eltávolítás táblázattal `Forrás → Célfiók (× szorzó)` sorokkal — soha vesszővel elválasztott szöveg-blob. Az összes bemenet **mentés előtt validálva** — hiányzó név/forrás/célfiók, nem pozitív méretezési paraméter, negatív/inkonzisztens lot-határok, tartományon kívüli drawdown %, nincs engedélyezett megbízástípus, vagy üres szimbólum-szűrő hibalista felületre jelenik meg + mentés blokkolva. Létrehozáskor a profil létrehozódik + minden kiválasztott slave hozzáadódik a választott beállításokkal, majd az oldal visszatér a Copy Trading-listához.

**Importálás / exportálás.** A teljes beállítások blokk **exportálható JSON-fájlba** és újra **importálható** az űrlap előzetesen kitöltéséhez, így egy hangolás újrafelhasználható profilok között írás nélkül. A szimbólum-mappa hasonlóan **exportálható / importálható CSV-fájlként** (`Source,Destination,VolumeMultiplier`) — készítsd elő a nagy bróker-szimbólum-térképet egy táblázatkezelőben és töltsd be egy lépésben. Ugyanez a szimbólum-vezérlők és CSV-importálás/exportálás a Copy Trading-oldalon lévő célfióki dialógusban is elérhető.

Sor-műveletek tiszteletben tartják az életciklust: **Start** csak nem futó caso engedélyezett, **Stop** + **Pause** csak futó esetén, **Delete** futás közben letiltva + megerősítés kérése profil + célfiók eltávolítása előtt.

Egy éppen elindított profil röviden egy **Starting** állapotot mutat (nem egy zöld *Running*) míg a gazdája betölt referencia-adatokat és futtat az első resync-et — még nem tükröz megbízásokat a célfiók között. A **Running**-ra vált az a pillanat amikor az első resync befejeződik és a motor másolhat. A Starting futóként kezelendő a sor-vezérlésekhez (Start letiltva, Stop és live-naplók engedélyezve, Szerkesztés/Törlés blokkolva), így a melegítő profil nem indítható újra vagy nem szerkeszthető az indítás közben. A melegítési fázis in-process követendő a profilt üzemeltető csomóponton; egy másik replikán üzemeltetett profil (vagy amely nem üzemeeltethető — annak forrás-/célfióka nincsenek csatolva az Open API-n keresztül) a sima állapotot mutatja.

## Per-destination options

Beállítva a New Profile-oldalon, a Copy Trading-oldal célfiók-dialógusában, vagy `POST /api/copy/profiles/{id}/destinations` útvonalon:

- **Méretezés** (`MoneyManagementMode` + paraméter): rögzített lot, lot/notional szorzó, arányos egyenleg/equity/szabad margó, rögzített kockázat %, rögzített tőkeáttétel, auto-arányos, **kockázat-%-from-stop** (M7). Plusz min/max lot-határok + force-min-lot. **Risk-from-stop** a célfiókat úgy méretezi, hogy a **saját egyenlege** konfigurált százalékát kockáztassa, származtatva a **master stop-loss távolságból** (`master 2%-ot kockáztat → slave auto-2%-ot kockáztat`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master nyitás **stop-loss nélkül** nincs távolsága a méretezéshez → a konfigurált **max-kockázat fallback lot-ot** (M7) használja, ha be van állítva, egyébként kihagyva (`no_stop_loss`) nem találgatva. Arányos-**equity**/**szabad margó** valós fiók **equity**-ből méretez (`balance + Σ lebegő P&L`, cTrader Open API-ból származtatva amely nem biztosít equity-t), nem egyszerű egyenlegből — így a master nyitott profit/veszteséggel helyesen méretezi a másolatokat. Használt margó nincs kitéve az egyeztetési API által, így a szabad margó egyenlőségként kezelendő (őszinte rendelkezésre álló pénzeszközök proxy); más módok egyenleget olvasnak + extra revaluációs körút kihagynak.
- **Irányszűrő**: mindkettő / csak hosszú / csak rövid. **Reverse**: flip side (+ swap SL↔TP) contrarian másolathoz.
- **Manage-only** (Ignore-New-Trades / Close-Only): tükrözz zárásokat, részleges zárásokat + védelem-változásokat a már másolat pozíciókon, de nyiss meg **semmilyen** új pozíciókat/szünetelő megbízásokat (kihagyva `manage_only`). Használj a célfiókat lecsökkentéséhez meglévő másolatok vágása nélkül.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (alapértelmezetten bekapcsolva): a profil **első** resync-jén, hogy másolatokat nyisson meg a master pre-existing pozícióihoz, + hogy zárjon meg másolatokat amelyeket a master zárt míg a profil megállt. Mindkettő csak a start-nál érvényes — mid-run újracsatlakozás mindig teljes egyeztetéssel működik így desync helyreáll függetlenül.
- **Szimbólum-mappa** + **szimbólum-szűrő** (whitelist / blacklist). Minden szimbólum-mappa bejegyzés választható **cél-specifikus mennyiség-szorzót** (cMAM cél-specifikus felülbírálat) hordozhat a másolat méretét skálázva az adott szimbólumon a célfióki méretezésen felül (1 = nincs változás). Teljes mappa importálható/exportálható **CSV-ként** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; oszlopok `Source,Destination,VolumeMultiplier`) — minden sor validálva domain érték objektumok által, így az erőforrás-fájl nem tud érvénytelen térképet előállítani.
- **Trading-hours ablak** (C18) — cél-specifikus napi UTC-ablak (`start`/`end` nap percei, vég kizárólagos; `start == end` = egész nap). Ablak kívüli új nyitások kihagyva (`trading_hours`); ablak `start > end` esetén éjfél után átível (pl. 22:00–06:00). Meglévő pozíciók kezelve maradnak.
- **Source-label szűrő** (C18, cTrader egyenértéke az MT magic-number szűrőnek) — ha be van állítva, másolj csak master megbízásokat amelyek label **pontosan** illeszkednek (pl. egy bot megbízásai, vagy kézi-csak label); egyébként kihagyva (`source_label`). Üres = másolj mindent. Hordozva az `ExecutionEvent.SourceLabel`-en a master pozíció/megbízás `TradeData.Label`-ből, tiszteletben tartva resync-ben is.
- **Fiók-védelem** (ZuluGuard / Global Account Protection) — vizsgálj a célfióki **live equity-t** (`balance + Σ lebegő P&L`, szavazva minden `CopyDefaults.EquityGuardInterval`) a `StopEquity` alsó határ és/vagy választható `TakeEquity` felső határ ellen. Megsértéskor alkalmazzál módot: **CloseOnly** (stop új másolatok, tartsd a meglévőket), **Frozen** (stop nyitás), **SellOut** (zárj be **minden** másolatot a célfiókon azonnal). Egyszer lőttzután, célfióki latched — nincs új nyitás amíg a host újraindul — + `CopyAccountProtectionTriggered` riasztás emelkedik. `SellOut` követeli a `StopEquity`-t; `TakeEquity` a `StopEquity` felett kell hogy maradjon. **Nincs-garancia fenntartás:** a sell-out piaci végrehajtást használ — mint minden versenyző egyenértéke, nem garantálja a kitöltési árat gyors/szaggatott piacon.
- **Flatten-All panik gomb** (C8) — `POST /api/copy/profiles/{id}/flatten` azonnal bezárja **minden** másolt pozíciót minden célfiókon + zárást alkalmaz új nyitások ellen. Keresztfolyamat-irányított: az API флаг állít, a supervisor a futó host-hoz szállítja (token-rotáció csatorna újrafelhasználásával), amely helyben lelapít; флаг törlödik így pontosan egyszer lő (`CopyFlattenAll` riasztás). A felhasználó ezután szünetelteti/leállítja a profilt.
- **Prop-firm szabály-őr** (C7) — a prop-firm másoló felhasználók által kért kényszerítés. Célfiókonként, **napi veszteség-sapka** (napi nyitás equity-ből való veszteség) és/vagy **trailing-drawdown** korlát (lebegő csúcs equity-ből való veszteség), mindkettő betét-valutában. Megsértéskor célfióki **auto-lelapítva** (minden másolat bezárva) + **kizárva** a nap többi részén (új nyitások kihagyva `prop_lockout`); `CopyPropRuleBreached` riasztás lő. A kizárás törlödik amikor az UTC nap átfordul (friss alapvonal/csúcs felvétele). Ugyanazt a live-equity szavazást osztja mint fiók-védelem.
- **Végrehajtási jitter** (C11, alapértelmezetten ki) — véletlen `0..N` ms késleltetés minden másolat elhelyezése előtt, hogy de-korreláljon majdnem-azonos megbízás-időbélyegeket a felhasználó **saját** fiókjaiban. **Megfelelőségi fenntartás:** segítség prop-farmok számára amelyek *engedélyezik* a másolatot — **nem** eszköz a másolatot betiltó farm megkerüléséhez; a farmad szabályain belül maradás a te felelősséged.
- **Config zárolás** (C9) — fagyasztd meg a célfióki beállításokat egy ideig (`POST …/destinations/{id}/lock` percekkel). Míg zárolt, a célfióki nem távolítható el (aggregát visszautasít `CopyDestinationConfigLocked`-tal) — szándékos védelem az impulzív változások ellen a drawdown alatt. A zárolás automatikusan lejár az időbélyegénél.
- **Konzisztencia pre-riasztás** (C10) — figyelmeztetés (napi egyszer UTC-ben) amikor a célfióki **napi nyereség** eléri a napi nyitás equity-nek a konfigurált százalékát (`CopyConsistencyThresholdApproaching`), így a prop-farm konzisztencia-szabály tisztelve van *mielőtt* aktiválódna. Nyereség-oldali, független a veszteség-oldali lockout-tól; a prop-szabály-őr napjalapvonalján működik.
- **Megbízástípus-szűrő** — válaszd ki pontosan mely master megbízástípusokat másolj: piaci, piaci-tartomány, limit, stop, stop-limit (`CopyOrderTypes` флагok; alapértelmezetten mind). cMAM-stílus szelektivitás.
- **Copy SL / Copy TP** — tükrözd a master stop-loss / take-profit, vagy kezeld a védelmet függetlenül. Érvényes a **nyitott pozícióra és** a szünetelő megbízásokra is — egy limit/stop/stop-limit másolat elhelyezésre kerül és módosításra kerül a master megbízás SL/TP-jével (felcserélt a **Reverse** alatt), így a védelem a szünetelő feltöltésének a pillanatában rögzítve van, nem csak később.
- **Copy trailing stop**, **mirror részleges zárás**, **mirror scale-in** — mindegyik függetlenül váltható.
- **Copy szünetelő lejárata** (alapértelmezetten bekapcsolva) — tükrözd a master szünetelő megbízásának Good-Till-Date lejárati időbélyegét.
- **Copy master slippage** (alapértelmezetten bekapcsolva) — piaci-tartomány + stop-limit megbízásoknál helyezd el a slave megbízást a master pontos slippage-val-pontban (alapár slave live spot-ból van véve).
- **Őrök**: max drawdown %, napi veszteség-sapka, max másolat-késleltetés, slippage-szűrő (skip másolat ha a slave ár több pippal mozdult el a master bejegyzéstől). **Max másolat-késleltetés** a master esemény valós szerver-időbélyegéhez mérve (`ExecutionEvent.ServerTimestamp`) injektált `TimeProvider`-en keresztül: konfigurált max-lag-nél idősebb jel kihagyva, így stale másolat soha nem kerül el később.
- **SL/TP pontosság-normalizálás** (M6) — másolt stop-loss/take-profit árak lekerekítve a **célfióki** szimbólum pontosságára az amend előtt (a pozíciókon **és** szünetelő-megbízás elhelyezés/amend), így a master finer pontosságnál lévő ár (vagy brókerek közötti számjegy-eltérés) soha nem akadályozza meg a szerver `INVALID_STOPLOSS_TAKEPROFIT`-ét.
- **Elutasítási áramkör-breaker / Follower Guard** (G8) — célfióki `CopyDefaults.RejectionBudget` nyitások sorát elutasító **aktiválva**: nincs új nyitás a hűlési ablakon (CopyDestinationTripped` riasztás lő), elutasítás-viharból való megakadályozás (prop-farm) fiók verésétől. Meglévő pozíciók továbbra is kezelve + zárva míg aktiválva; breaker automatikusan alaphelyzetbe állítódik a hűlés után + sikeres másolat nullázza a számlálót.
- **Lot szanity sapka** (C14) — abszolút max másolat méret és/vagy többszöröse-master sapka. Számított másolat az abszolút saját meghaladása, vagy a `N×` master saját lot méretének meghaladása, **kemény-blokkolva** (felszínre jön `lot_sanity` skip, számolva a `cmind.copy.skipped`-ből) nem helyezve — megvédi a katasztrofális-túlméret osztálytól (0.23-lot master 3 lotra változik minden vevőn keresztül futó szorzón vagy kerekítési hiba által). Mindkét dimenzió alapértelmezetten `0` (ki).

## Reliability & edge cases

A motor úgy építve, hogy a valóság, hogy bármi meghibásodhat bármikor:

- **Slave-szünetelő fill-korrelációs timeout** (C13) — tükrözött slave-szünetelő amelynek master-szünetelő eltűnt (sem pihenő sem frissen kitöltött) törölve az korrelációs timeout után, így a slave-másolat nem tud korrelál-nélkül kitölteni kezelt-nélküli pozícióba (`CopyPendingTimedOut`). A resync is kitakarít order-id-jelölt kitöltött-szünetelő árvákat.
- **Cross-broker szünetelő-fill verseny** — egy slave saját szünetelője feltöltődhet (az ára meglehetett) a kis ablakban mielőtt a master fill/cancel esemény feldolgozódna. Ez egy slave-pozícióval marad jelölt a forrás **megbízás** id-vel, amely a kanonikus zárás/SL-TP útvonalak (forrás-pozíció id-vel kulcsozottak) kimaradnának. A master **kitöltéskor** a korai slave kitöltés kibocsátódik és helyét kanonikus-jelölt piaci másolattal veszi fel — így a célfióki pontosan **egy** másolattal végződik, soha nem duplikált pozícióval; a master **törlésekor** azonnal bezáródik (a master sosem vette volna fel a kereskedést). Mindkettő azonnal hat, nem csak a következő resync-nél. Egy slave-oldali SL/TP találat amely bezár egy másolatot amit a master még tart a forrás-vezérelt és a következő reconcile-en újranyitódik (a motor **master** eseményeket tükröz; ez nem fogyaszt célfióki-oldali végrehajtásokat).
- **Robosztus zárás/lelapítás** (M8) — árva zárása resync-ben, vagy lelapítás őr-megsértéskor, tűri a pozíciónak a bróker által már zárva (`POSITION_NOT_FOUND`): minden zárás függetlenül futtatódik, így egy stale id soha nem abortálja a resync-et vagy hagyja a fiók többi részét lelapítás-nélkül.

- **Indítás már master-ban kereskedésre** — a host az indítást egyeztet + másolatokat nyit meg a master meglévő pozícióihoz.
- **Csatlakozás szakadás / desync** — az újracsatlakozáskor a host egyeztet: megnyit hiányzó másolatokat, bezárja az árvákat, újracímkéz szünetelőket. Nincs duplikált megbízás.
- **Megbízás-elhelyezési hiba** — egy célfiókin való meghibásodás naplózva, soha nem blokkolja más célfiókokat.
- **Egyetlen érvényes token cID-nként** — cTrader érvényteleníti a cID régi hozzáférési tokenét új kibocsátás pillanatában. cMind felcseréli a futó host tokenét **helyben** (re-auth az élő socketen) így a másolás folytatódik az stream ledobása nélkül. Lásd [token lifecycle](token-lifecycle.md).

## Auditability

Minden akció strukturált, forrás-generált log eseményt bocsát ki (`LogMessages`) profil-id, célfióki cID, megbízás/pozíció id-k, + értékek — megbízás elhelyezett/kihagyott (okkal), részleges zárás, alkalmazott védelem, alkalmazott trailing, szünetelő elhelyezett/módosított/törölt, lejárat tükrözve, piaci-tartomány slippage tükrözve, token felcserélve, resync összefoglalás. Ez a megfelelőség és vitafeloldás audit nyomvonala.

A naplók mellett a motor **OpenTelemetry mérőszámokat** bocsát ki az `cMind.Copy` mérőn (a megosztott OTel-csatornában regisztrálva, OTLP / Azure Monitor-hoz exportálva mint a többi): `cmind.copy.latency` (master-esemény → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out minden célfiókra, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (célfiókon jelölt), `cmind.copy.skipped` (okkal jelölt), + `cmind.copy.failed`. Ezek latencia/slippage regresszió mérhető, nem csak látható a log sorban — live suite hasonlítja össze őket költségvetéshez.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — létrehozás (választható célfióki fiók-id-kkel).
- `GET /api/copy/profiles/{id}` — teljes detalj minden célfióki opcióval.
- `POST /api/copy/profiles/{id}/destinations` — célfióki hozzáadása teljes opciókészlettel.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — eltávolítás.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — életciklus.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — méretezési módok, döntés-szűrők, megbízástípus-szűrő, lejárat másolás, piaci-tartomány/stop-limit slippage, SL/TP váltók, részleges zárás, szünetelő módosítás/törlés, start-with-open, connection-lecsatlakozás→desync→resync, helyben token-csere, cross-cID érvénytelenítés. Futtat a `FakeTradingSession`-en, cTrader-hű in-memory szimulátor ellen.
- **Integration** (`tests/IntegrationTests/CopyLive`) — csomópont-affinitás/lease claim, token-verzió propagáció valódi Postgres-en.
- **E2E** (`tests/E2ETests`) — célfióki-opció körút API + UI-n, teljes életciklus.
- **Stress / DST** (`tests/StressTests`) — determinisztikus-szimulációs tesztelés: vetített randomizált terhelések + hiba-injekció (socket flap, megbízás elutasítás, piaci-tartomány elutasítás, token rotáció, csomópont halál) vezetik a `CopyEngineHost`-ot quiescence-hez + konvergencia invariánsokat hasonlítanak. Lásd [testing/stress-testing.md](../testing/stress-testing.md). Ez a suite felfedi + fixálja a valódi indítási versenyt: `OnReconnected` vezetett a kezdeti referencia-terhelés előtt + resync, így a socket flap az indítás alatt futtathatott volna a második resync-et egyidejűleg + korruptálhatta volna a host nem-konkurens állapot szótárát — indítási terhelés + első resync most futtatódnak a `_stateGate` alatt.
- **Live** — valódi cTrader demo fiók; lásd [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Lásd [dev-credentials.md](../testing/dev-credentials.md) az egyetlen hitelesítési fájlhoz amely a live + E2E szintet olvassa.

## Profile controls and destination management

A Start/stop ikonozás gombok minden profil soron (letiltva amikor az akció nem alkalmazható). Forrás- és célfióki fiók **fiók-számukkal**, soha belső id-vel jelenik meg. Profil kattintása **dialógust** nyit a célfióki-fiók kezelésésé (hozzáadás/eltávolítás teljes cél-specifikus beállításokkal).
