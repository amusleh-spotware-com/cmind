---
description: "Másolja a master cTrader fiók pozícióit egy vagy több slave fiókra — több broker, több cID — célonként beállítható vezérléssel és pénzügyi szintű egyeztetéssel."
---

# Másolási kereskedelem

Tükrözze a **master** cTrader fiók pozícióit egy vagy több **slave** fiókra — több broker, több cID — célonként beállítható vezérléssel és pénzügyi szintű egyeztetéssel.

## Fogalmak

- **Másolási profil** — egy master (`SourceAccountId`) + egy vagy több **célfiók**. Életciklus: `Draft → Running → Paused → Stopped` (hibánál `Error`). Aggregátumgyök: `CopyProfile` (birtokol `CopyDestination` objektumokat).
- **Célfiók** — egy slave fiók + teljes szabályrendszer a master másolásához. Minden konfiguráció célfiók alapú, így egy master etethet egyszerre konzervatív és agresszív slave fiókokat.
- **Másolási motor gazdagépe** — profil futó feldolgozója (`CopyEngineHost`). Feliratkozik a master végrehajtási streamre, alkalmazva minden eseményt minden célfiókra.
- **Felügyelő** — `CopyEngineSupervisor`, background szolgáltatás minden csomóponton. Üzemeltet hozzárendelt profilokat, öngyógyító működés a fürt szintjén (lásd [scaling](../deployment/scaling.md)).

## Mit tükrözünk

| Master esemény | Slave művelet |
|--------------|--------------|
| Piaci / piaci tartomány pozíciónyitás | Méretezett másolat megnyitása (jelölve a forrás pozícióazonosítóval) |
| Limit / stop / stop-limit függőben lévő megbízás | Megfelelő függőben lévő megbízás helyezése |
| Függőben lévő megbízás módosítása | A tükrözött függőben lévő megbízás módosítása |
| Függőben lévő megbízás törlése / lejárata | A tükrözött függőben lévő megbízás törlése |
| Részleges zárás | A slave pozíciónak ugyanilyen arányú zárása |
| Bejátszás (volumen növekedés) | Az hozzáadott volumen megnyitása (opcionális) |
| Stop-loss / trailing-stop módosítás | A slave pozícióvédelem módosítása |
| Teljes zárás | A slave másolat zárása |

Minden másolat **jelölve a forrás pozícióazonosítóval/megbízásazonosítóval**. Újracsatlakozás után a gazdagép az egyeztetésből felépíti az állapotot: megnyitja azokat a másolatokat, amelyeket a master tart, de a slave hiányzik, zárja a slave "árvákat", amelyeket a master már nem tart — **duplikált kereskedelem nélkül**.

## Profil létrehozása

A Copy Trading oldalon a **New Profile** (Új profil) dialógus összegyűjt mindent előre: profilnév, forrás (master) fiók, célfiók (slave) fiókok (többválasztás **Select all** (Összes kiválasztása) gombbal; kiválasztott master kizárva a slave listából), + teljes célfiok-alapú opcióhalmaz. Minden bemenet **elmentés előtt validálva** — hiányzó név/forrás/cél, nem pozitív méretezési paraméter, negatív/inkonzisztens lot határok, érvénytelen tartományú leszálló %, nincs engedélyezve megbízástípus, üres szimbólumszűrő vagy helytelenül formázott szimbólumleképezés párok felszínre hozottak hibalistáként + blokkolt mentés. Megerősítéskor profil létrehozva + minden kiválasztott slave hozzáadva a választott beállításokkal.

Sorműveletek tiszteletben tartják az életciklust: **Start** csak amikor nem fut, **Stop** + **Pause** csak amikor fut, **Delete** (Törlés) letiltva míg fut + megerősítés kér profil + célfiók eltávolítása előtt.

## Célfiok alapú lehetőségek

Beállítható az Új profil dialógusban, a Copy Trading oldal célfiok-alapú paneljén, vagy `POST /api/copy/profiles/{id}/destinations` útján:

- **Méretezés** (`MoneyManagementMode` + paraméter): fix lot, lot/notional szorzó, arányos egyenleg/equity/szabad margó, fix kockázat %, fix tőkeáttétel, auto-arányos, **risk-%-from-stop** (M7). Plus min/max lot határok + force-min-lot. **Risk-from-stop** méretez a célfiókot úgy, hogy az a saját egyenlegének konfigurált százalékát kockáztassa, a **master stop-loss távolságából** levezetett (`master kockáztat 2% → slave auto-kockáztat 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master nyitás **stop-loss nélkül** nincs távolsága a méretezéshez → a konfigurált **max-risk fallback lot** (M7) használja, ha be van állítva, egyébként kihagyva (`no_stop_loss`). Arányos-**equity**/**szabad margó** méretez a valós fiók **equity**-jén (`egyenleg + Σ lebegő P&L`, a cTrader Open API-ból levezetett, amely nem szállít equity-t), nem tiszta egyenleg — így a master nyitott profit/veszteségen ül a másolatokat helyesen méretez. Felhasznált margó nincs kitéve az egyeztetési API-n, így a szabad margó equity-ként kezelendő (becsült rendelkezésre álló pénzügyi proxy); egyéb módok egyenleget olvasnak + kihagynak az extra revaluálási kört.
- **Irányszűrő**: mindkettő / csak long / csak short. **Reverse** (Fordított): oldalváltás (+ SL↔TP csere) ellentétes másolathoz.
- **Manage-only** (Csak kezelés / Close-Only): tükröz zárásokat, részleges zárásokat + védelemmódosításokat már másolat pozíciókon, de megnyit **nem** új pozíciókat/függőben lévő megbízásokat (kihagyott `manage_only`). Használja a célfiók csökkentéséhez meglévő másolatok vágása nélkül.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (alapértelmezés bekapcsolt): a profil **első** szinkronizációján, hogy másolatokat nyitunk-e a master meglévő pozícióihoz, + hogy zárunk-e másolatokat, amelyeket a master zárta, amíg profil megállt volt. Mindkettő csak indításkor érvényes — futó közben újracsatlakozás mindig teljes szinkronizálást végez, így az aszinkronizálódás függetlenül helyreáll.
- **Szimbólumleképezés** + **szimbólumszűrő** (fehérlista / fekete lista). Minden szimbólumleképezés-bejegyzés opcionális **szimbólumonkénti volumenszorzót** (cMAM szimbólumonkénti felülbírálat) hordoz, amely méretezi a másolat méretét az adott szimbólum esetén a célfiók méretezésén felül (1 = nincs változás). Az egész leképezés **CSV** formátumban importálható/exportálható (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; oszlopok `Source,Destination,VolumeMultiplier`) — minden sor domain értékobjektumokon keresztül validálva, így a helytelenül formázott fájl nem állíthat elő érvénytelen leképezést.
- **Kereskedési órák ablaka** (C18) — célfiok alapú napi UTC ablak (`start`/`end` perc-a-napban, vég kizáró; `start == end` = egész nap). Ablakán kívüli új nyitások kihagyottak (`trading_hours`); ablak `start > end` értékkel éjfél után fordul (pl. 22:00–06:00). Meglévő pozíciók maradnak kezelve.
- **Forrás-címke szűrő** (C18, cTrader megfelelője MT magic-number szűrőnek) — ha beállított, másolja csak a master kereskedelmét, amelynek a címkéje **pontosan** megegyezik (pl. egy bot kereskedelmei, vagy csak kézi címke); egyébként kihagyott (`source_label`). Üres = minden másolja. A master pozícióazonosítójának `TradeData.Label` értékén keresztül szállítva, szinkronizáláson is tiszteletben tartva.
- **Fiók védelme** (ZuluGuard / Global Account Protection) — figyeld a célfiók **élő equity**-jét (`egyenleg + Σ lebegő P&L`, szavazott minden `CopyDefaults.EquityGuardInterval`) az `StopEquity` emelet + opcionális `TakeEquity` mennyezet ellen. A megsértés esetén alkalmazzuk az üzemmódot: **CloseOnly** (állj meg új másolatoknál, tartsd kezelve a meglévőket), **Frozen** (állj meg nyitásnál), **SellOut** (zárjuk **minden** másolatot a célfiók-ból azonnal). Miután megindul, a célfiók lakatlanított — nincs új nyitás amíg a gazdagép újraindul — + `CopyAccountProtectionTriggered` riasztás emelésse. `SellOut` igényel `StopEquity`-t; `TakeEquity`-nek a `StopEquity` fölött kell lenni. **Garancia nélküli figyelmeztető:** az eladás piaci végrehajtást használ — mint minden verseny megfelelője, nem garantálhat kitöltési árat gyors/közvetített piacon.
- **Flatten-All pánikgomb** (C8) — `POST /api/copy/profiles/{id}/flatten` azonnal zárja meg **minden** másolat pozícióját minden célfiók-ból + zárollja az új nyitások ellen. Keresztfolyamaton irányított: az API zaszlót állít, a felügyelő szállít a futó gazdagéphez (újrahasznosító token-forgatási csatorna), amely rendben megsimít; zaszló törlödve úgy tüz pontosan egyszer (`CopyFlattenAll` riasztás). A felhasználó ezután szünetelteti/leállítja a profilt.
- **Prop-firm szabályvédelem** (C7) — prop-firm másoló felhasználók kértek erősítést. Célfiok alapú, **napi veszteségkorlát** (a nap nyitó equity-jéből való veszteség) + opcionális **trailing-drawdown** (a futó csúcs equity-ből való veszteség) korlát, mindkettő letéti pénznemben. A megsértés célfiók **auto-kilapított** (minden másolat zárva) + **kizárt** az UTC nap többi részéből (új nyitások kihagyottak `prop_lockout`); `CopyPropRuleBreached` riasztás tüzel. A kizárás az UTC nap végén tisztázódik (friss alapérték/csúcs szállítva). Megosztja ugyanazon az élő-equity szavazattal, mint a fiók védelme.
- **Végrehajtás jitter** (C11, alapértelmezés kikapcsolt) — véletlen `0..N` ms késleltetés minden másolat helyezése előtt, a visszautalások korrelációja a felhasználó **saját** fiókjain keresztül. **Megfelelőségi figyelmeztető:** segítség prop vállalatoknak, amelyek *engedélyezik* másolást — **nem** eszköz a letiltó cégek elkerülésére; a cég szabályai betartása az Ön felelőssége.
- **Konfiguráció zárolása** (C9) — fagyasszon meg célfiók beállításokat (egy időtartamra) (`POST …/destinations/{id}/lock` percekkel). Zárolt, célfiók nem távolítható el (aggregátum elutasít `CopyDestinationConfigLocked` értékkel) — szándékos védelem az impulzív változások ellen a csökkenés alatt. Zárolás automatikusan lejár az időbélyegzésénél.
- **Konzisztencia előriasztás** (C10) — figyelmeztess (naponta egyszer UTC alapon), amikor a célfiók **napi nyeresége** eléri a nap nyitó equity-jének konfigurált százalékát (`CopyConsistencyThresholdApproaching`), így a prop-firm konzisztencia szabály tiszteletben tartja *mielőtt* megindul. Nyereség oldali, független veszteség-oldali zárásáról; fut az ugyanazon napi alapértékből, mint a prop-szabály védelme.
- **Megbízástípus szűrő** — válassza pontosan, mely master megbízástípusokat másolja: piaci, piaci tartomány, limit, stop, stop-limit (`CopyOrderTypes` zaszlók; alapértelmezés mind). cMAM-stílus szelektivitás.
- **Másolat SL / Másolat TP** — tükrözze a master stop-loss / take-profit, vagy kezelje a védelmet függetlenül.
- **Másolat trailing stop**, **tükrözött részleges zárás**, **tükrözött bejátszás** — mindegyik egymástól függetlenül váltható.
- **Másolat függőben lévő lejárata** (alapértelmezés bekapcsolt) — tükrözze a master függőben lévő megbízás Good-Till-Date lejárati időbélyegzését.
- **Másolat master csúszása** (alapértelmezés bekapcsolt) — piaci-tartomány + stop-limit megbízások esetén helyezze a slave megbízást a master pontos csúszáspont értékével (alapár a slave élő spotjáról).
- **Őrök**: max leszálló %, napi veszteségkorlát, max másolat késleltetés, csúszásszűrő (másolás kihagyása, ha slave ár a master bejegyzésből N pip-nél többet mozgott). **Max másolat késleltetés** a master esemény valódi szerver időbélyegzése ellen mérve (`ExecutionEvent.ServerTimestamp`) az injektált `TimeProvider` segítségével: az aláírás régebbi, mint a konfigurált max-lag kihagyott, így az elavult másolat soha nem helyezhető késve.
- **SL/TP pontosság normalizálása** (M6) — másolat stop-loss/take-profit árak kerekítve a **célfiók** szimbólumának pontosságához módosítás előtt, így a master ár finomabb pontosságban (vagy broker közötti pont eltérés) soha nem okoz szervernek `INVALID_STOPLOSS_TAKEPROFIT`.
- **Elutasítás áramkör-szakadó / Follower Guard** (G8) — célfiók elutasít `CopyDefaults.RejectionBudget` nyitásokat egymás után **kiváltódva**: nincsenek új nyitások a hűtési ablakban (`CopyDestinationTripped` riasztás tüzel), megakadályozva az elutasítás viharát a (prop-firm) fiók kalapálásából. Meglévő pozíciók maradnak kezelve + zárva a kiváltódás alatt; szakadó auto-alaphelyzetez a hűtési után + sikeres másolat kitörlődik a számlálóból.
- **Lot józanság mennyezete** (C14) — abszolút max másolat mérete + több-mint-master korlát. Számított másolat meghaladva az abszolút korlátot, vagy meghaladva `N×` master saját lot méretét, **kemény-blokkolt** (felszínre hoztva mint `lot_sanity` kihagyás, számlált `cmind.copy.skipped` értéken) nem helyezve — véd a katasztrófális túlméretezés osztály ellen (0.23-lot master 3 lotba fordulva minden vevőn futó szorzó vagy kerekítési hiba útján). Mindkét dimenzió alapértelmezés `0` (kikapcsolt).

## Megbízhatóság és határesetei

A motor valóságra épül, hogy bármi meghiúsulhat bármikor:

- **Slave-függőben lévő betöltés-korrelációs időtúllépés** (C13) — tükrözött slave függőben lévő, amelynek master függőben lévő eltűnt (sem nem pihen, sem nem frissen töltődik) törlödve a korrelációs időtúllépés után, így a slave másolat nem tölthető fel korreláció nélküli kezeletlen pozícióba (`CopyPendingTimedOut`). Szinkronizálás is megtisztítja az order-id-jelölt kitöltött-függőben lévő árva.
- **Robosztus zárás/kilapítás** (M8) — árva zárása szinkronizációnál vagy flökkítés az őr megsértésnél, tűri a pozíciót a broker már zárva (`POSITION_NOT_FOUND`): minden zárás függetlenül fut, így egy elavult id soha nem szakítja meg a szinkronizálást vagy hagyja a többi fiók kilapítatlant.

- **Indítás a master már kereskedelmekben** — indulásnál a gazdagép szinkronizál + másolatokat nyit a master meglévő pozícióihoz.
- **Csatlakozási kimaradás / aszinkronizálódás** — újracsatlakozás a gazdagép szinkronizál: megnyitja a hiányzó másolatokat, zárja az árvákat, újcímkézi a függőben lévő köztörvényadókat. Nincs duplikált megbízás.
- **Megbízás helyezési kudarc** — kudarc egy célfiók-on naplózva, soha nem blokkol más célfiók-okat.
- **Egyetlen érvényes token per cID** — cTrader érvényteleníti a cID régi hozzáférési tokenjét amikor új kibocsátódik. cMind felcseréli a futó gazdagép tokenját **helyben** (újra-hitelesítés élő socketből), így a másolás folytatódik a stream eldobása nélkül. Lásd [token lifecycle](token-lifecycle.md).

## Auditálhatóság

Minden művelet sűrű, forrás-generált napló eseményt bocsát ki (`LogMessages`) profil azonosító, célfiók cID, megbízás/pozícióazonosítók, + értékek — megbízás helyezett/kihagyott (ok), részleges zárás, védelem alkalmazva, trailing alkalmazva, függőben lévő helyezett/módosított/törölt, lejárat tükrözve, piaci-tartomány csúszás tükrözve, token felcserélve, szinkronizálási összefoglalás. Ez az audit nyomvonal megfelelőséghez + vitás felbontáshoz.

A naplók mellett a motor **OpenTelemetry metrikákat** bocsát ki a `cMind.Copy` méteren (regisztrálva a közös OTel vezetékbe, exportálva OTLP / Azure Monitor-hoz mint többi): `cmind.copy.latency` (master-esemény → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out minden célfiók-hoz, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (jelölt célfiok-al), `cmind.copy.skipped` (jelölt ok szerint), + `cmind.copy.failed`. Ezek teszik a késleltetés/csúszás regresszió mérhetővé, nem csak láthatóvá a napló sorban — élő test asztért tette be őket a költségvetés ellen.

## API

- `GET /api/copy/profiles` — lista.
- `POST /api/copy/profiles` — létrehozás (opcionális célfiók fiókazonosítók).
- `GET /api/copy/profiles/{id}` — teljes részletek minden célfiók opciójával.
- `POST /api/copy/profiles/{id}/destinations` — célfiók hozzáadása az opció halmazzel.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — eltávolítás.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — életciklus.

## Tesztek

- **Egység** (`tests/UnitTests/CopyTrading`) — méretezés módok, döntésszűrők, megbízástípus szűrő, lejárat másolat, piaci-tartomány/stop-limit csúszás, SL/TP váltók, részleges zárás, függőben lévő módosítás/törlés, indítás-nyitott, szétkapcsolódás→aszinkronizálódás→szinkronizálás, helyben token csere, cID közötti érvénytelenítés. A `FakeTradingSession`, cTrader-hű memória-beli szimulátor ellen fut.
- **Integráció** (`tests/IntegrationTests/CopyLive`) — csomópont-affinitás/lízing igénylés, token-verzió terjedés valódi Postgres-en.
- **E2E** (`tests/E2ETests`) — célfiok-opció körút az API + UI-n, teljes életciklus.
- **Stressz / DST** (`tests/StressTests`) — determinisztikus-szimulációs tesztelés: vetett véletlenített terhelés + hibainjektor (socket flap, megbízás elutasítás, piaci-tartomány elutasítás, token forgatás, csomópont halál) hajtja a `CopyEngineHost` nyugalomra + esztergonyt az egyezés invariánsokon. Lásd [testing/stress-testing.md](../testing/stress-testing.md). Ez az ízületlet szabad + javított valódi indulási versenyhelyzet: `OnReconnected` vezetékelt az eredeti referencia-terhelés + szinkronizálás előtt, így socket flap indítás alatt tudna futtat másik szinkronizálást párhuzamosan + korrupt a gazdagép nem-konkurrens állapotszótárainak — indulási terhelés + első szinkronizálás most futnak `_stateGate` alatt.
- **Élő** — valódi cTrader demo fiókok; lásd [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Lásd [dev-credentials.md](../testing/dev-credentials.md) egyéni hitelesítő fájlhoz az élő + E2E szintek beolvasásához.
