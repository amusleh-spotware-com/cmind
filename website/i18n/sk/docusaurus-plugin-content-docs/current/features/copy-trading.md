---
description: "Zrkadliť hlavný cTrader účet na jeden alebo viac podľahnutých účtov — cez brokery, cez cID — s ovládaním na jednotlivé destinácie + odsúhlasenie na úrovni peňažností."
---

# Kopírovanie obchodovania

Zrkadliť **hlavný** cTrader účet na jeden alebo viac **podľahnutých** účtov — cez brokery, cez cID — s ovládaním na jednotlivé destinácie + odsúhlasenie na úrovni peňažností.

## Koncepty

- **Profil kopírovania** — jeden hlavný (`SourceAccountId`) + jeden alebo viac **destináciami**. Životný cyklus: `Draft → Running → Paused → Stopped` (`Error` pri zlyhaní). Koreňový agregát: `CopyProfile` (vlastní `CopyDestination`).
- **Destinácia** — jeden podľahnutý účet + úplná sada pravidiel pre spôsob kopírovania hlavného na neho. Všetka konfigurácia pre jednotlivé destinácie, takže jeden hlavný účet môže zásobovať konzervatívne aj agresívne podľahnuté účty naraz.
- **Hostiteľ motora kopírovania** — pracovný proces pre profil (`CopyEngineHost`). Prihláša sa na prúd vykonávania hlavného, aplikuje každú udalosť na každú destináciu.
- **Supervízor** — `CopyEngineSupervisor`, služba na pozadí na každom uzle. Hostí pridelené profily, samo sa liečia v rámci klastra (pozri [scaling](../deployment/scaling.md)).

## Čo sa zrkadlí

| Udalosť hlavného | Akcia podľahnutého |
|--------------|--------------|
| Otvorenie pozície na trhu / trhu-rozsahu | Otvor veľkosť kópie (označená identifikátorom zdrojovej pozície) |
| Čakajúca objednávka limit / stop / stop-limit | Umiestnite zodpovedajúcu čakajúcu objednávku s stop-loss / take-profit hlavného |
| Zmena čakajúcej objednávky | Upravte zrkadlenú čakajúcu objednávku na mieste (vrátane jej stop-loss / take-profit) |
| Zrušenie čakajúcej objednávky / vypršanie | Zrušite zrkadlenú čakajúcu objednávku |
| Čiastočné zatvorenie | Zatvorte rovnaký podiel pozície podľahnutého |
| Zvýšenie objemu (zvýšenie objemu) | Otvorte pridaný objem (voliteľne) |
| Zmena stop-loss / trailing-stop | Upravte ochranu pozície podľahnutého |
| Úplné zatvorenie | Zatvorte kópiu podľahnutého |

Každá kópia je **označená identifikátorom zdrojovej pozície/objednávky**. Po opätovnom pripojení hostiteľ obnoví stav z odsúhlasenia: otvorí kópie, ktoré hlavný drží, ale podľahnutý mu chýba, zatvorí „sirotkov" podľahnutého, ktorých už hlavný nemá — **bez duplikovania obchodov**.

## Vytvorenie profilu

**Nový profil** otvára vyhradenú **celostrankový** formulár (`/copy-trading/new`), nie dialóg — sada možností je dostatočne veľká na to, aby sa stránka lepšie čítala na telefóne aj na počítači. Zbiera všetko dopredu: názov profilu, zdroj (hlavný) účet, destinácie (podľahnuté účty) (viacnásobný výber s tlačidlom **Vybrať všetko**; zvolený hlavný vylúčený zo zoznamu podľahnutých), + úplná sada možností na jednotlivé destinácie. **Každý ovládací prvok má vysvetľujúci tooltip** popisujúci, čo robí a ako ho používať. Štruktúrované vstupy používajú **správne overené ovládací prvky** — čísla/percentá prostredníctvom číselných polí, režimy/smer/filter prostredníctvom výberov, filter symbolov prostredníctvom zoznamu pridávania/odstránenia symbolov čipu a mapu symbolov prostredníctvom tabuľky pridávania/odstránenia riadkov `Zdroj → Destinácia (× násobiteľ)` — nikdy textový blob oddelený čiarkami. Všechny vstupy sú **overené pred uložením** — chýbajúci názov/zdroj/destinácia, nepozitívny parameter veľkosti, negatívny/nekonzistentný rozsah veľkosti skupiny, mimo rozsahu percento poklesu, bez povolených typov objednávok alebo prázdny filter symbolov sa vyskytu ako zoznam chýb + blokovať uloženie. Pri vytváraní sa vytvorí profil + do každého vybraného podľahnutého sa pridá s vybranými nastaveniami, potom sa stránka vráti na zoznam Kopírovania obchodovania.

**Import / export.** Celý blok nastavení je možné **exportovať do súboru JSON** a znovu **importovať** na vopred vyplnenie formulára, takže sa ladenie dá znova používať v profiloch bez opätovného zadávania. Mapu symbolov je možné podobne **exportovať / importovať ako súbor CSV** (`Zdroj,Destinácia,NásobiteľObjemu`) — pripravte veľkú mapu symbolu brokera v tabuľke a zaťahajte ju v jednom kroku. Rovnaké ovládače symbolov a import/export CSV sú k dispozícii aj v dialógu destinácie na stránke Kopírovania obchodovania.

Akcie riadkov rešpektujú životný cyklus: **Spustiť** povolené iba keď nespustené, **Stop** + **Pauza** iba keď spustené, **Vymazať** zakázané pri spustenom + spýta sa na potvrdenie pred odstránením profilu + destináciami.

Novo spustený profil krátko ukazuje stav **Starting** (nie zelený *Running*) kým jeho hostiteľ načítava referenčné údaje a spúšťa prvú resynchrónizáciu — zatiaľ nezrkadľuje objednávky naprieč destináciami. Prepína na **Running** v momente, keď sa táto prvá resynchrónizácia dokončí a motor môže kopírovať. Starting sa považuje za spustený pre ovládače riadkov (Spustiť zakázané, Stop a live-logs povolené, Úpravu/Vymazanie blokované), takže ohrev profilu nemôže byť znovu spustený ani upravený počas spustenia. Fáza ohrevu je sledovaná v procese na uzle, ktorý profil hostuje; profil hostovaný na ďalšej replika (alebo ten, ktorý nemôže byť hostovaný — jeho zdrojové/destináciné účty nie sú prepojené cez Open API) ukazuje svoj prostý stav.

## Možnosti na jednotlivé destinácie

Nastavte na stránke Nový profil, v dialógu destinácie na stránke Kopírovania obchodovania alebo prostredníctvom `POST /api/copy/profiles/{id}/destinations`:

- **Veľkosť** (`MoneyManagementMode` + parameter): pevná partia, partia/nominálny násobiteľ, proporcionálny zostatok/equity/voľná maržna, pevné riziko %, pevné pákovanie, automatické proporcionálne, **riziko-%-z-stop** (M7). Plus min/max hranice partií + vynútiť min-partiu. **Riziko-z-stop** veľkosti destinácie tak, aby riskovala nakonfigurované percento *jej vlastného* zostatku, odvodené z **vzdialenosti stop-loss hlavného** (`hlavný riskuje 2% → podľahnutý automaticky-riskuje 2%`): `partia = zostatok×% ÷ (stopVzdialenosť × veľkosť zmluvy)`. Hlavný otvor **bez** stop-loss nemá vzdialenosť na veľkosť — používa nakonfigurovanú **maximálnu-riziko fallback partiu** (M7) ak je nastavená, inak preskočenú (`no_stop_loss`) nie hádaí. Proporcionálny-**equity**/**voľná-maržna** veľkosť z real účtu **equity** (`zostatok + Σ plávajúci P&L`, odvodený na cTrader Open API, ktorý neposúva equity), nie jednoduchý zostatok — takže hlavný sedí na otvorenom zisku/strate veľkosti kópie správne. Využitá maržna nie je vystavená zmierovacej API, takže voľná-maržna sa považuje za equity (čestný proxy dostupných fondov); ostatné režimy čítajú zostatok + preskočia extra kolo prehodnotenia.
- **Filter smeru**: oboje / iba dlhé / iba krátke. **Obratiť**: otočte stranu (+ vymeňte SL↔TP) na kontráriu kopírovania.
- **Spravovať-iba** (Ignorovať-nové-obchody / Iba-Zatvoriť): zrkadliť zatvorenia, čiastočné zatvorenia + zmeny ochrany na už skopírovaných pozíciách, ale otvorte **bez** nových pozícií/čakajúcich objednávok (preskočené `manage_only`). Používajte na zníženie destinácie bez odsúhlasenia existujúcich kópií.
- **Sync-Otvoriť-na-spustení** / **Sync-Zatvorené-na-spustení** (predvolene zapnuté): pri **prvej** resynchrónizácii profilu, či sa majú otvoriť kópie pre už existujúce pozície hlavného, + či sa majú zatvoriť kópie, ktoré hlavný zatvoril, kým bol profil zastavený. Obe sa vzťahujú iba na spustenie — mid-run opätovné pripojenie vždy plne odsúhlasí, takže sa desync zotavuje bez ohľadu na to.
- **Mapa symbolov** + **filter symbolov** (biela listina / čierna listina). Každá položka mapy symbolov má voliteľný **násobiteľ objemu na symbol** (cMAM overridden na symbol) škálujúci veľkosť kópie pre tento symbol na vrchole veľkosti destinácie (1 = bez zmeny). Celá mapa import/export ako **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; stĺpce `Zdroj,Destinácia,NásobiteľObjemu`) — každý riadok overený cez doménové objekty hodnoty, takže deformovaný súbor nemôže vytvoriť neplatnú mapu.
- **Okno obchodných hodín** (C18) — na destináciu denne UTC okno (`start`/`end` minút dňa, koniec exkluzívny; `start == end` = celodenné). Nové otvory mimo okna preskočené (`trading_hours`); okno so `start > end` obalí minulosť (napr. 22:00–06:00). Existujúce pozície zostanú spravované.
- **Filter značky zdroja** (C18, ekvivalent cTrader čarovného čísla filtru) — ak je nastavená, skopírujte iba obchody hlavného, ktorých značka sa **presne zhoduje** (napr. obchody jedného bota alebo iba ručne značka); inak preskočené (`source_label`). Prázdne = skopírujte všetko. Niesené na `ExecutionEvent.SourceLabel` z pozície/objednávky hlavného `TradeData.Label`, počas resync tiež čestne.
- **Ochrana účtu** (ZuluGuard / Globálna ochrana účtu) — sledujte **live equity** destinácie (`zostatok + Σ plávajúci P&L`, hlasovaný každých `CopyDefaults.EquityGuardInterval`) proti `StopEquity` podlahu a/alebo voliteľnému stropu `TakeEquity`. Pri porušení, použite režim: **CloseOnly** (zastavte nové kópie, pokračujte v spravovaní existujúcich), **Frozen** (zastavte otvárame), **SellOut** (zatvorte **každú** kópiu na destináciu okamžite). Po spustení je destinácia zatváraná — bez nových otvorov, kým sa hostiteľ nerestartuje — + `CopyAccountProtectionTriggered` upozornenie vznesené. `SellOut` vyžaduje `StopEquity`; `TakeEquity` musí sedieť nad `StopEquity`. **Záruka bez záruky:** sell-out používa trhové vykonávanie — ako ekvivalent každého konkurenta, nemôže zaručiť výplnú cenu v rýchlom/zelezo trhu.
- **Flatten-All tlačidlo paniky** (C8) — `POST /api/copy/profiles/{id}/flatten` okamžite zatvorí **každú** skopírovanú pozíciu na každej destinácii + zámky proti novým otvorom. Smerovatý cez proces: API nastaví príznak, supervízor dodá spustený hostiteľ (s opätovným použitím kanála rotácie tokenu), ktorý vyplní na mieste; príznak vymazaný tak sa spúšťa presne raz (`CopyFlattenAll` upozornenie). Používateľ potom pozastaví/zastaví profil.
- **Stráž pravidiel própfirmy** (C7) — enforcement vlastníkov kopírovačov própfirmy žiadajú. Na destináciu, **denná strata cap** (strata z denného otváracieho equity) a/alebo **zaostávajúci pokles** limit (strata z bežného vrcholového equity), oba v mene vkladu. Pri porušení destinácia **automaticky zflattená** (každá kópia zatvorená) + **uzamknutá** zvyšok UTC dňa (nové otvory preskočené `prop_lockout`); `CopyPropRuleBreached` upozornenie sa spúšťa. Zámka lock sa vymaže, keď sa UTC deň prevráti (čerstvá základňa/vrchol vzatý). Zdieľa rovnaký live-equity poll ako ochrana účtu.
- **Chvenie vykonávania** (C11, predvolene vypnuté) — náhodné `0..N` ms oneskorenie pred umiestnením každej kópie, aby sa de-skoreľovala blízko zhodných operácií objednávok cez **vlastný** účty používateľa. **Varovanie o súlade:** pomôcka vlastníkom firiem, ktorí *povolenia* kopírovania — **nie** nástroj na evasión firmy, ktorá ju zakazuje; zostať v rámci pravidiel vašej firmy je vaša zodpovednosť.
- **Zámka konfigurácie** (C9) — zámrznutie nastavení destinácie počas obdobia (`POST …/destinations/{id}/lock` s minútami). Pokiaľ je uzamknutá, destinácia nemôže byť odstránená (agregát odmieta s `CopyDestinationConfigLocked`) — úmyselný strážny prvok proti impulzívnym zmenám počas poklesu. Zámka vyprší automaticky na jeho časovú pečiatku.
- **Pre-výstraha konzistentnosti** (C10) — upozorniť (raz za UTC deň), keď **denný zisk** destinácie dosiahne nakonfigurované percento denného otváracieho equity (`CopyConsistencyThresholdApproaching`), takže sa pravidlo konzistentnosti apropfirmy rešpektuje *pred* tým, ako to spustí. Zisk-strana, nezávislý od straty-strany zámky; beží z rovnakej dennej základne ako strážca vlastného pravidla.
- **Filter typu objednávky** — zvoľte presne, ktoré typy hlavných objednávok skopírovať: trh, trh-rozsah, limit, stop, stop-limit (`CopyOrderTypes` príznaky; predvolene všetko). Selektívnosť štýlu cMAM.
- **Kopíruj SL / Kopíruj TP** — zrkadliť stop-loss / take-profit hlavného, alebo spravovať ochranu nezávisle. Vzťahuje sa na **obe** otvorené pozície **a** odpočívajúce čakajúce objednávky — limit/stop/stop-limit kópia je umiestnená a upravená s SL/TP hlavnej objednávky (vymenené pod **Obratiť**), takže ochrana je pripojená v momente, keď čakajúci vyplní, nie iba potom.
- **Kopíruj trailing stop**, **zrkadliť čiastočné zatvorenie**, **zrkadliť zvýšenie objemu** — každá nezávisle prepínateľná.
- **Kopíruj expíráciu čakajúceho** (predvolene zapnuté) — zrkadliť timestamp expírácie Good-Till-Date čakajúcej objednávky hlavného.
- **Kopíruj slippage hlavného** (predvolene zapnuté) — pre objednávky trhu-rozsahu + stop-limit, umiestnite objednávku podľahnutého s presným slippage-v-bodoch hlavného (základná cena vzatá z live miesta podľahnutého).
- **Stráže**: maximálny pokles %, denná strata cap, maximálne oneskorenie kopírovania, filter slippage (preskočte kopíruj, ak sa cena podľahnutého pohla za N pipsy od záznamu hlavného). **Maximálne oneskorenie kopírovania** merané proti real server timestamp hlavnej udalosti (`ExecutionEvent.ServerTimestamp`) cez injekovaný `TimeProvider`: signál starší ako nakonfigurované maximum-lag preskočený, takže starý copy nikdy nie je umiestnený neskoro (predtým oneskorenie vždy nula + stráž mŕtvy).
- **SL/TP presnosť normalizácia** (M6) — kopírovaná stop-loss/take-profit ceny zaokrúhlené na **destináciu** symbol je presnosť pred zmeny (na pozíciách **a** umiestnení čakajúcej objednávky/úprave), takže cena hlavného v jemnejšej presnosti (alebo cez-broker digit neshoda) nikdy nespúšťa server `INVALID_STOPLOSS_TAKEPROFIT`.
- **Obvod breaker odmietnutia / Sledovateľ Stráž** (G8) — destinácia odmietajúca `CopyDefaults.RejectionBudget` otvorí v rade je **spustená**: bez nových otvorov pre chladiace okno (`CopyDestinationTripped` upozornenie spúšťa), zastavenie storm odmietnutia z kladiva (vlastné firmáciou) účtu. Existujúce pozície stále spravované + zatvorené počas trip; breaker automaticky reset po cooldown + úspešný copy vymaže počítadlo.
- **Veľkosť partia zdravý strop** (C14) — absolútny maximálny rozmach kópie a/alebo viacnásobný-z-master cap. Vypočítaná kópia presahujúca absolútny cap, alebo presahujúca `N×` vlastnej veľkosti hlavného partiu, **hard-blokovaná** (povrch ako `lot_sanity` skip, počítaná na `cmind.copy.skipped`) nie je umiestnená — obrana proti katastrofálnej oversize triede (0,23-lot hlavný prevrátený do 3 partií na každý prijímač cez runaway násobiteľ alebo zaokrúhľovacia chyba). Obe dimenzie predvolene `0` (vypnuté).

## Spoľahlivosť a prípadne hraničných

Motor postavený na realite, že čokoľvek môže zlyhať kedykoľvek:

- **Čakajúci čakajúci čakajúci timeout korelácií** (C13) — zrkadlená čakajúca objednávka podľahnutého, ktorej čakajúca hlavného zmizla (ani nespočívajúca ani čerstvých vyplnené) zrušená po korelácií timeout, takže kopíruj podľahnutého nemôže vyplniť nekorelovaného do nespravovanej pozície (`CopyPendingTimedOut`). Resync tiež čistokupuje objednávka-id-označený vyplnený čakajúci sirota.
- **Krížiko-broker čakajúci-fill race** — vlastný čakajúci podľahnutého môže vyplniť (jeho cena zasiahnutá) v malom okne pred spracovaním hlavného fill/cancel udalosti. To zanecháva podľahnutej pozícii označená zdrojom **objednávka** id, ktorú kanonické close/SL-TP cesty (keyed podľa zdrojového **pozície** id) by sa zmeškal. Na hlavného **vyplnení** sa skorý podľahnutý fill vyradí a nahradí jedným kanonicky-označený market kópie — tak destinácia končí s presne **jednej** kópie, nikdy zdvojenú pozícií; na hlavného **zrušení** sa zatvorí outright (hlavný nikdy nevzal obchod). Obaja pôsobiť okamžite, nie iba na ďalšej resync. Vlastný podľahnutý SL/TP hit, ktorý zatvorí kópiu, ktorú hlavný stále drží, je zdrojom-riadené a znovu otvorené na ďalšom odsúhlasení (motor zrkadlí **hlavné** udalosti; nepotrebuje destináciu-strana vykonávania).
- **Robustný close/flatten** (M8) — zatvorenie siroty na resync, alebo vyplnenie na guard breach, toleruje pozíciu brokeruje už zatváran (`POSITION_NOT_FOUND`): každý close beží nezávisle, takže jeden starý id nikdy neprerúši resync alebo nechá zvyšok účtu nevyplnený.

- **Spustenie s hlavným už v obchodoch** — na spustenie hostiteľ odsúhlasí + otvorí kópie pre existujúce pozície hlavného.
- **Pripojenie kapsy / desync** — na opätovnom pripojení hostiteľ odsúhlasí: otvorí chýbajúce kópie, zatvorí sirotkov, znovu označí čakajúcich. Žiadne duplicitné objednávky.
- **Zlyha umiestnenie objednávky** — zlyha na jednej destinácie zaznamenané, nikdy neblokuje iné destinácie.
- **Jeden platný token na cID** — cTrader zneplatní starý prístupový token cID v momente, keď je vydaný nový. cMind vymeňuje bežný hostiteľ token **na mieste** (re-auth na live socket) takže kopírovanie pokračuje bez pádu prúdu. Pozri [token lifecycle](token-lifecycle.md).

## Auditovateľnosť

Každá akcia vyžaruje štruktúrovanú, zdrojom generovanú udalosť denníka (`LogMessages`) s id profilu, cID destinácie, objednávka/pozícia ids, + hodnoty — objednávka umiestnená/preskočená (s dôvodom), čiastočné zatvorenie, ochrana aplikovaná, zaostávajúci aplikovaný, čakajúci umiestnený/upravený/zrušený, expírácia zrkadlená, trhový-rozsah slippage zrkadlený, token vymenený, resync zhrnutie. Toto je audit trail na súlad + riešenie sporov.

Súbežne s logom, motor vyžaruje **OpenTelemetry metriky** na `cMind.Copy` meter (registrovaný v zdieľanom OTel pipeline, exportovaný cez OTLP / do Azure Monitor ako zvyšok): `cmind.copy.latency` (hlavný-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out na všetky destinácie, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (označený podľa destinácie), `cmind.copy.skipped` (označený podľa dôvodu), + `cmind.copy.failed`. Tieto sú oneskorenie/slippage regresiu merateľný, nie len viditeľný v riadku denníka — live suite tvrdí ich proti rozpočtu.

## API

- `GET /api/copy/profiles` — zoznam.
- `POST /api/copy/profiles` — vytvorenie (s voliteľnými ids destináciách účtov).
- `GET /api/copy/profiles/{id}` — úplný detail vrátane každého nastavenia destinácie.
- `POST /api/copy/profiles/{id}/destinations` — pridajte destináciu s úplným súborom možností.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — odstránenie.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — životný cyklus.

## Testy

- **Jednotka** (`tests/UnitTests/CopyTrading`) — režimy veľkosti, filtre rozhodovania, filter typu objednávky, expírácia kopie, market-range/stop-limit slippage, SL/TP prepínače, čiastočné zatvorenie, čakajúci amend/cancel, start-s-otvoreným, disconnect→desync→resync, in-place token swap, cross-cID invalidácia. Spustí proti `FakeTradingSession`, cTrader-verné in-memory simulátor.
- **Integrácia** (`tests/IntegrationTests/CopyLive`) — node-afinita/lease claim, propagácia verzií tokenov na real Postgres.
- **E2E** (`tests/E2ETests`) — destinácia-možnosti round-trip cez API + UI, úplný životný cyklus.
- **Stres / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomizované workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) jednotka `CopyEngineHost` na quiescence + tvrdí convergenciu invarianty. Pozri [testing/stress-testing.md](../testing/stress-testing.md). Táto suite povrch + oprava real startup race: `OnReconnected` zapojený pred počiatočným referenčným zaťažením + resync, takže socket flap počas spustenia by mohol spustiť druhý resync súbežne + poškodiť non-súbežný stav hostiteľa slovníky — startup zaťaženie + prvý resync teraz spustí pod `_stateGate`.
- **Live** — real cTrader demo účty; pozri [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Pozri [dev-credentials.md](../testing/dev-credentials.md) pre jeden identifikačný súbor live + E2E tiers čítajú.

## Ovládanie profilov a správa destinácie

Spustenie/zastavenie sú ikony tlačidla na každom riadku profilu (zakázané, keď sa akcia nevzťahuje). Zdrojový a destináciu účtov sú zobrazené ich **číslo účtu**, nikdy interný id. Kliknutím na profil otvára **dialóg** na správu jeho destináciách účtov (pridávanie/odstránenie s úplnými per-destináciu nastaveniami).
