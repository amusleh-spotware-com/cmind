---
description: "Zrkadlite master cTrader účet na jeden+ slave účtov — cross-broker, cross-cID — s per-destination kontrolou + money-grade rekonciláciou."
---

# Copy trading

Zrkadlite **master** cTrader účet na jeden+ **slave** účtov — cross-broker, cross-cID — s per-destination kontrolou + money-grade rekonciláciou.

## Koncepty

- **Copy profil** — jeden master (`SourceAccountId`) + jeden+ **destinácií**. Životný cyklus: `Draft → Running → Paused → Stopped` (`Error` pri zlyhaní). Aggregate root: `CopyProfile` (vlastní `CopyDestination`).
- **Destinácia** — jeden slave účet + úplná sada pravidiel, ako sa master kopíruje na to. Všetky konfig per-destinácia, takže jeden master živí konzervatívne + agresívne slave naraz.
- **Copy engine host** — bežiaci pracovník na profil (`CopyEngineHost`). Zdieľajú master execution stream, aplikuje každú udalosť na každú destináciu.
- **Supervisor** — `CopyEngineSupervisor`, background služba na každom uzle. Hostuje pridelené profily, samoreparácia naprieč klastrom (pozrite si [scaling](../deployment/scaling.md)).

## Čo sa zrkadlí

| Master udalosť | Slave akcia |
|--------------|--------------|
| Market / market-range pozícia otvorená | Otvorte veľkosť kópie (označená ID zdroja pozície) |
| Limit / stop / stop-limit pending order | Vložte zodpovedajúcu pending order |
| Pending order amend | Zmeňte zrkadlenú pending order v mieste |
| Pending order cancel / expiry | Zrušte zrkadlenú pending order |
| Čiastočné zavretie | Zatvorte rovnaký pomer slave pozície |
| Scale-in (zvýšenie objemu) | Otvorte pridaný objem (opt-in) |
| Stop-loss / trailing-stop zmena | Zmeňte ochranu slave pozície |
| Úplné zavretie | Zatvorte kopiu slave |

Každá kópia **označená ID zdroja pozície/order**. Po opätovnom pripojení host obnovuje stav z rekoncilácie: otvára kópie, ktoré master má ale slave chýba, zatvárajú slave "siroty" master už nemá — **bez duplikácie obchodov**.

## Vytvorenie profilu

Dialóg **Nový profil** na stránke Copy Trading zbiera všetko dopredu: názov profilu, zdroj (master) účet, destinácia (slave) účty (multi-select s tlačidlom **Vybrať všetko**; vybraný master vylúčený z slave zoznamu) + úplný per-destinácia súbor možností nižšie. Všetky vstupy **overené pred uložením** — chýbajúci názov/zdroj/destinácia, non-pozitívne veľkosti param, negatívny/nekonzistentný lot bounds, out-of-range drawdown %, žiadny typ objednávky povolený, prázdny filter symbolov, alebo malformovaná symbol-map páry povrch ako zoznam chýb + blok uložiť. Pri potvrdení sa profil vytvorí a každý vybraný slave sa pridá s vybranými nastaveniami.

Akcie riadkov rešpektujú životný cyklus: **Spustiť** povolené iba keď nie je spustené, **Zastaviť** + **Pozastaviť** iba keď je spustené, **Odstrániť** zakázané počas spustenia + pýta sa potvrdenie pred odstránením profilu + destinácie.

## Per-destinácia možnosti

Nastavte v dialógu Nový profil, na paneli per-destinácia stránky Copy Trading alebo cez `POST /api/copy/profiles/{id}/destinations`:

- **Veľkosť** (`MoneyManagementMode` + parameter): pevný lot, lot/notional multiplier, proporcionálny balance/equity/free-margin, pevné riziko %, pevný leverage, auto-proporcionálne, **risk-%-from-stop** (M7). Plus min/max lot bounds + force-min-lot. **Risk-from-stop** veľkosť destinácia tak riziká konfigurovaný percento *jej vlastného* balance, vypočítaný z **master's stop-loss distance** (`master riziká 2% → slave auto-riziká 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master otvorený **bez** stop-loss nemá vzdialenosť na veľkosť voči → používa nakonfigurované **max-risk fallback lot** (M7), ak je nastaveného, inak preskočené (`no_stop_loss`) nie hádal. Proporcionálne **equity**/**free-margin** veľkosť vypnutý reálny účet **equity** (`balance + Σ floating P&L`, odvodený za cTrader Open API, ktorý neposkytuje equity), nie plaintext balance — takže master sedenie na otvorenom zisku/strate veľkosť kópie správne. Používaný margin neexponovaný rekoncilácia API, takže free-margin považované za equity (čestný dostupný-funds proxy); ďalšie režimy čítajú balance + preskočiť extra revaluation round-trip.
- **Filter smeru**: obaja / iba dlhé / iba krátke. **Obrátenie**: pretočiť stranu (+ swap SL↔TP) na contrarian kópiu.
- **Manage-only** (Ignore-New-Trades / Close-Only): zrkadlo zatvárajú, čiastočné zatvárajú + ochrana zmeny na již kopírovaných pozícií, ale otvorenie **no** nové pozície/pending objednávky (preskočené `manage_only`). Používajte na veternú destináciu bez rezania existujúcich kópií.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (štandardne na): na profil **prvý** resync, či otvorené kópie na master's pre-existujúce pozície, + či zatvoriť kópie master zatvorené kým je profil zastavený. Obaja aplikujú iba na start — mid-run reconnect vždy plne zmierovania, takže desync obnovuje bez ohľadu.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Každá symbol-map entrada nesie voliteľný **per-symbol objem multiplier** (cMAM per-symbol override) škálovací veľkosť kópie na ten symbol na top destinácia veľkosť (1 = bez zmeny). Celá mapa importuje/exportuje ako **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; stĺpce `Source,Destination,VolumeMultiplier`) — každý riadok overený cez doménové objekty hodnôt, takže malformovaný súbor nemôže vyprodukuje neplatnú mapu.
- **Trading-hours window** (C18) — per-destinácia denný UTC okno (`start`/`end` minúty-denného, koniec exkluzívny; `start == end` = všetko-denný). Nové otvára mimo okna preskočené (`trading_hours`); okno s `start > end` obchádza cez polnoc (napr. 22:00–06:00). Existujúce pozície zostávajú spravované.
- **Source-label filter** (C18, cTrader ekvivalent MT magic-number filtru) — keď je nastavený, kopírujte iba master obchody, ktorých label sa zhoduje **presne** (napr. obchody jedného bota, alebo iba manuálny label); inak preskočené (`source_label`). Prázdne = kopírujte všetky. Prenesené na `ExecutionEvent.SourceLabel` z master pozície/order `TradeData.Label`, vážené na resync tiež.
- **Ochrana účtu** (ZuluGuard / Global Account Protection) — sledovať destinácia **live equity** (`balance + Σ floating P&L`, hlasovať každý `CopyDefaults.EquityGuardInterval`) voči `StopEquity` podlahy a/alebo voliteľný `TakeEquity` strop. Na porušení, aplikujte režim: **CloseOnly** (zastaviť nové kópie, udržiavať existujúce), **Frozen** (zastaviť otváranie), **SellOut** (zavrite **všetky** kópie na destináciu okamžite). Raz vypálené, destinácia zatváramý — žiadne nové otvára kým host reštartuje — + `CopyAccountProtectionTriggered` upozornenie zvýšené. `SellOut` vyžaduje `StopEquity`; `TakeEquity` musí sedieť nad `StopEquity`. **Žiadna záruka caveat:** sell-out používa market execution — ako každý konkurent ekvivalent, nemôže zaručiť cenu vyplnenia v rýchlo/gapped trhu.
- **Flatten-All panic tlačidlo** (C8) — `POST /api/copy/profiles/{id}/flatten` okamžite zatvárajú **všetky** kopírované pozície na každej destináci + zámok voči novým otvoreniam. Smerované cross-process: API nastaví vlajku, supervisor dodá bežnému hostiteľu (opätovne používajúce token-rotation kanál), ktorý v mieste spľošťuje; vlajka vyčistená, takže vypáli presne raz (`CopyFlattenAll` upozornenie). Používateľ potom pozastaví/zastaví profil.
- **Prop-firm rule guard** (C7) — enforcement prop-firm kopier používatelia žiadajú. Per destinácia, **daily-loss cap** (strata z denného otvárania equity) a/alebo **trailing-drawdown** limit (strata z bežiaceho peak equity), obaja v vkladnej mene. Na porušení destinácia **auto-flatten** (všetka kópia zatvorená) + **uzamknutá** zvyšok UTC deňa (nové otvárajú preskočené `prop_lockout`); `CopyPropRuleBreached` upozornenie trvá. Lockout vyčisťuje keď UTC deň valcuje nad (fresh baseline/peak prevzatý). Zdieľa rovnakú live-equity anketu ako ochrana účtu.
- **Execution jitter** (C11, štandardne vypnutý) — náhodný `0..N` ms oneskorenie pred umiestnením každej kópie, na de-correlate takmer identické poradie časov naprieč používateľ **vlastný** účtov. **Compliance caveat:** pomoc pre prop firmy, ktoré *dovoľujú* kopírovanie — **nie** nástroj vyhnúť firme, ktorá to zakazuje; ostať v pravidlách svojej firmy je vaša zodpovednosť.
- **Config lock** (C9) — zmraziť destinácia nastavenia na obdobie (`POST …/destinations/{id}/lock` s minútami). Zatiaľ čo je zamknutý, destinácia nemôže byť odstránená (agregát odmieta s `CopyDestinationConfigLocked`) — zámyselný strážnik voči impulzívnym zmenám počas drawdown. Lock sa automaticky vypršiava na jeho timestamp.
- **Consistency pre-alert** (C10) — upozorniť (raz za UTC deň) keď destinácia **daily profit** dosiahne nakonfigurovaný percent denného otvárania equity (`CopyConsistencyThresholdApproaching`), takže prop-firm pravidlo konzistentnosti rešpektované *pred* tým, ako to trvá. Profit-side, nezávislý na loss-side lockout; beží do rovnakého dňa baseline ako prop-rule guard.
- **Order-type filter** — vyberte presne ktorí master typy objednávky na kopírovanie: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` príznaky; štandardne všetky). cMAM-style selectivity.
- **Copy SL / Copy TP** — zrkadlo master's stop-loss / take-profit, alebo spravovať ochranu nezávisle.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — každá nezávisle prepínateľný.
- **Copy pending expiry** (štandardne na) — zrkadlo master pending order good-Till-Date expiry timestamp.
- **Copy master slippage** (štandardne na) — pre market-range + stop-limit objednávky, miesto slave order s master's presný slippage-in-points (base cena prevzatá z slave live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (preskočiť kópiu, ak sa cena slave posunula mimo N pips z master vstupu). **Max copy delay** meraný voči master event real server timestamp (`ExecutionEvent.ServerTimestamp`) cez injektovaný `TimeProvider`: signál starší ako nakonfigurovaný max-lag preskočené, takže stará kópia nikdy nie je umiestnená neskoro (doteraz oneskorenie vždy nula + guard mŕtvy).
- **SL/TP precision normalizácia** (M6) — kopírované stop-loss/take-profit ceny zaokrúhlené na **destinácia** symbol's digit presnosti pred amend, takže master cena na jemnejšej presnosti (alebo cross-broker digit neshoda) nikdy neha trip server `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — destinácia odmieta `CopyDefaults.RejectionBudget` otvára za sebou je **tripped**: žiadne nové otvára na chladiacu dobu (`CopyDestinationTripped` upozornenie trvá), zastavenie rejection bouřkou z kladín (prop-firm) účet. Existujúce pozície stále spravované + zatvorené kým je tripped; breaker auto-resets po cooldown + úspešný kópia vyčisťuje počítadlo.
- **Lot sanity strop** (C14) — absolútny max veľkosť kópie a/alebo multiple-of-master čiapka. Vypočítaný kópia prekračujúc absolútny strop, alebo prekračujúc `N×` master's vlastný lot veľkosť, **hard-blocked** (povrch ako `lot_sanity` preskočiť, počítané na `cmind.copy.skipped`) nie umiestnený — bránich voči katastrofálnej-oversize triedy (0.23-lot master otáčajúc na 3 loty na každý prijímač cez runway multiplier alebo bug zaokrúhlenia). Obaja rozmery štandardne `0` (vypnutý).

## Spoľahlivosť & okrajové prípady

Engine postavený na realite, že nič nemôže zlyhať kedykoľvek:

- **Slave-pending fill-correlation timeout** (C13) — zrkadlené slave pending ktorého master pending zmizla (ani odpočívajúci ani čerstvo naplnený) zrušené po korelácia timeout, takže slave kópia nemôže vyplniť nekoreláciou do nespravovanej pozície (`CopyPendingTimedOut`). Resync tiež čistí order-id-labelled filled-pending sirota.
- **Robust close/flatten** (M8) — zavretie siroty na resync, alebo spľošťovanie na strážca porušenie, toleruje polohu broker už zatvorené (`POSITION_NOT_FOUND`): každý blízko beží nezávisle, takže jeden stály id nikdy neabortuje resync alebo ponechá zvyšok účtu un-flattened.
- **Spustiť s master už v obchodoch** — na spustiť host zmierovania + otvára kópie na master's existujúce pozície.
- **Connection drops / desync** — na reconnect host zmierovania: otvárajú chýbajúce kópie, zatvárajú siroty, re-labels pendings. Žiadne duplikátne objednávky.
- **Order placement failure** — zlyhanie na jeden destinácia protokolovaný, nikdy neblokuje ďalšie destinácie.
- **Single valid token per cID** — cTrader invalidates cID old access token moment nový vydaný. cMind vymeňuje bežný host token **v mieste** (re-auth na live socket) takže kopírovanie pokračuje bez prelomenia stream. Pozrite si [token lifecycle](token-lifecycle.md).

## Auditovateľnosť

Každá akcia emituje štruktúrovaný, zdrojom generovaný log event (`LogMessages`) s ID profilu, destinácia cID, order/pozícia ids, + hodnoty — objednávka umiestnené/preskočené (s dôvodom), čiastočné zavretie, ochrana aplikovaná, trailing aplikovaná, pending umiestnené/zmenené/zrušené, expiry zrkadlené, market-range slippage zrkadlené, token vymenené, resync shrnutie. Toto je audit trail na compliance + dispute resolution.

Spolu s log engine emituje **OpenTelemetry metriky** na `cMind.Copy` meter (registrovaný v zdieľanom OTel pipeline, exportované cez OTLP / do Azure Monitor ako zvyšok): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out na všetky destinácie, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (označený destináciou), `cmind.copy.skipped` (označený dôvodom), + `cmind.copy.failed`. Tieto robia latencia/slippage regresiu merateľný, nie len viditeľný v log linke — live suite tvrdení ich voči rozpočtu.

## API

- `GET /api/copy/profiles` — zoznam.
- `POST /api/copy/profiles` — vytvoriť (s voliteľným ID destinácia účtu).
- `GET /api/copy/profiles/{id}` — úplný detail incl. každou destináciou možnosti.
- `POST /api/copy/profiles/{id}/destinations` — pridať destinácia s úplným súboru možností.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — odstrániť.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — životný cyklus.

## Testy

- **Jednotka** (`tests/UnitTests/CopyTrading`) — veľkosti režimy, rozhodnutie filtre, order-type filter, expiry kópia, market-range/stop-limit slippage, SL/TP prepínače, čiastočné zavretie, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Beží voči `FakeTradingSession`, cTrader-faithful in-memory simulator.
- **Integrácia** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagácia na reálny Postgres.
- **E2E** (`tests/E2ETests`) — destinácia-option round-trip cez API + UI, úplný životný cyklus.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testovanie: osemené randomizovaný workload + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) jednotka `CopyEngineHost` na quiescence + assert convergence invariants. Pozrite si [testing/stress-testing.md](../testing/stress-testing.md). Táto suite povrch + pevný reálny startup race: `OnReconnected` zapojené pred iniciálnym reference-load + resync, takže socket flap počas spusti mohol spustiť druhé resync súbežne + korumpovať host je non-concurrent stav slovníkov — spustenie zaťaženia + prvý resync teraz beh pod `_stateGate`.
- **Live** — reálne cTrader demo účty; pozrite si [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Pozrite si [dev-credentials.md](../testing/dev-credentials.md) pre jeden credential súbor live + E2E tier čítané.
