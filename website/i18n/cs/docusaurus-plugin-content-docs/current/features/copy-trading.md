---
description: "Zrcadlení master cTrader účtu na jeden+ slave účtů — across-broker, across-cID — s per-destination kontrolou + money-grade reconciliacją."
---

# Копирование торговли

Zrcadlení **master** cTrader účtu na jeden+ **slave** účtů — across-broker, across-cID — s per-destination kontrolou + money-grade reconciliacją.

## Koncepty

- **Copy profil** — jeden master (`SourceAccountId`) + jeden+ **destinací**. Životní cyklus: `Draft → Running → Paused → Stopped` (`Error` při selhání). Aggregate root: `CopyProfile` (vlastní `CopyDestination`).
- **Destinace** — jeden slave účet + kompletní sada pravidel pro kopírování mastera na něj. Veškerá konfigurace per-destination, takže jeden master může zároveň krmit konzervativní i agresivní slave.
- **Copy engine host** — běžící worker pro profil (`CopyEngineHost`). Přihlašuje se k master execution streamu, aplikuje každou event na každou destinaci.
- **Supervizor** — `CopyEngineSupervisor`, background service na každém uzlu. Hostí přidělené profily, samo se léčí v rámci clusteru (viz [scaling](../deployment/scaling.md)).

## Co se zrcadluje

| Událost master | Akce Slave |
|--------------|--------------|
| Otevření market / market-range pozice | Otevřít kopii o určité velikosti (označenou ID zdrojové pozice) |
| Limit / stop / stop-limit pending order | Umístit odpovídající pending order |
| Úprava pending order | Upravit zrcadlený pending order na místě |
| Zrušení pending order / vypršení | Zrušit zrcadlený pending order |
| Částečné zavření | Zavřít stejný podíl slave pozice |
| Scale-in (zvýšení objemu) | Otevřít přidaný objem (opt-in) |
| Změna stop-loss / trailing-stop | Upravit ochranu slave pozice |
| Úplné zavření | Zavřít slave kopii |

Každá kopie je **označena ID zdrojové pozice/order**. Po opětovném připojení host znovu vytvoří stav z reconcile: otevře kopie, které master drží ale slave chybí, zavře slave "sirotky", které master už nedrží — **bez duplikování obchodů**.

## Vytváření profilu

Dialog **New Profile** na Copy Trading stránce sbírá vše na začátku: název profilu, zdroj (master) účet, destinace (slave) účty (multi-select s tlačítkem **Select all**; vybraný master vyloučen ze slave listu), + kompletní per-destination sadu možností níže. Všechny vstupy jsou **ověřeny před uložením** — chybějící název/zdroj/destinace, ne-kladitý sizing parametr, negativní/nekonzistentní lot bounds, mimo-rozsah drawdown %, žádný povolený typ objednávky, prázdný symbol filter, nebo špatně formátované páry symbol-map se objevují jako seznam chyb + blokují uložení. Po potvrzení se profil vytvoří + každý vybraný slave se přidá s vybranými nastaveními.

Řádkové akce respektují životní cyklus: **Start** je povolen pouze když není spuštěn, **Stop** + **Pause** pouze když je spuštěn, **Delete** je zakázán během běhu + požádá potvrzení před odstraněním profilu + destinací.

## Možnosti per-destination

Nastaveno v dialogu New Profile, na per-destination panelu Copy Trading stránky, nebo pomocí `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parametr): fixed lot, lot/notional multiplier, proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Navíc min/max lot bounds + force-min-lot. **Risk-from-stop** nastaví destinaci tak, aby riskovala konfigurovaný procent *její vlastní* bilance, odvozený z **master's stop-loss vzdálenosti** (`master riskuje 2% → slave auto-riskuje 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master open **bez** stop-loss nemá žádnou vzdálenost pro sizing → používá konfigurovaný **max-risk fallback lot** (M7) pokud je nastaven, jinak přeskočen (`no_stop_loss`). Proportional-**equity**/**free-margin** velikost na základě skutečné **equity** účtu (`balance + Σ floating P&L`, odvozeno z cTrader Open API které neposílá equity), ne prosté bilance — takže master s otevřeným ziskem/ztrátou správně nastaví kopie. Použitá marže není vystavena reconcile API, takže free-margin se behanduje jako equity (čestný dostupný-prostředky proxy); ostatní módy čtou bilanci + přeskakují extra revaluační round-trip.
- **Direction filter**: obě / long-only / short-only. **Reverse**: flip side (+ swap SL↔TP) pro contrarian kopii.
- **Manage-only** (Ignore-New-Trades / Close-Only): zrcadlují zavírání, částečné zavírání + změny ochrany na již zkopírovaných pozicích, ale otevírají **žádné** nové pozice/pending ordery (přeskočeno `manage_only`). Použijte k postupnému zavírání destinace bez řezání existujících kopií.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (výchozí on): při prvním resync profilu, zda otevřít kopie pro pre-existující pozice mastera, + zda zavřít kopie, které master zavřel během přerušení profilu. Obojí se aplikuje pouze na začátku — mid-run reconnect vždy plně reconciliuje, takže desync se zotaví bez ohledu.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Každý symbol-map záznam nese volitelný **per-symbol volume multiplier** (cMAM per-symbol override) nastavující kopii pro tento symbol na top of destination's sizing (1 = bez změny). Celá mapa importuje/exportuje jako **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; sloupce `Source,Destination,VolumeMultiplier`) — každý řádek ověřen prostřednictvím domain value objects, takže špatně formátovaný soubor nemůže vytvořit neplatnou mapu.
- **Trading-hours window** (C18) — per-destination denní UTC okno (`start`/`end` minut-dne, end vyloučeno; `start == end` = den celý). Nová otevření mimo okno přeskočena (`trading_hours`); okno s `start > end` se obtéká přes půlnoc (např. 22:00–06:00). Existující pozice zůstávají spravovány.
- **Source-label filter** (C18, cTrader ekvivalent MT magic-number filtru) — když je nastaveno, kopírujte pouze master obchody, jejichž label se shoduje **přesně** (např. obchody jednoho bota, nebo pouze manual label); jinak přeskočeno (`source_label`). Prázdné = kopírovat vše. Přenášeno na `ExecutionEvent.SourceLabel` z master pozice/order `TradeData.Label`, respektováno i na resync.
- **Account protection** (ZuluGuard / Global Account Protection) — sledujte **live equity** destinace (`balance + Σ floating P&L`, hlasitost každých `CopyDefaults.EquityGuardInterval`) proti `StopEquity` podlaze a/nebo volitelné `TakeEquity` stropu. Při porušení použijte mód: **CloseOnly** (zastavit nové kopie, zachovat správu existujících), **Frozen** (zastavit otevírání), **SellOut** (zavřít **každou** kopii na destinaci okamžitě). Po aktivaci je destinace uzamčena — bez nových otevření, dokud se host restartuje — + `CopyAccountProtectionTriggered` alert je vyvolán. `SellOut` vyžaduje `StopEquity`; `TakeEquity` musí sedět nad `StopEquity`. **Žádná-záruka caveat:** sell-out používá market execution — jako každý konkurentův ekvivalent, nemůže zaručit cenu vyplnění v rychlém/gapped trhu.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` okamžitě zavře **každou** zkopírovanou pozici na každé destinaci + zamkne proti novým otevřením. Směrováno cross-process: API nastaví flag, supervizor jej doručí běžícímu hostu (znovu používá token-rotation kanál), který na místě zplošťuje; flag je vymazán, takže se spustí přesně jednou (`CopyFlattenAll` alert). Uživatel pak pozastaví/zastaví profil.
- **Prop-firm rule guard** (C7) — vynucování, které si žádají uživatelé prop-firm copytrader. Per destinace, **daily-loss cap** (ztráta z denní otevírací equity) a/nebo **trailing-drawdown** limit (ztráta z běžící peak equity), obojí v měně depozita. Při porušení je destinace **auto-zplošťena** (každá kopie zavřena) + **uzamčena** zbytek UTC dne (nová otevření přeskočena `prop_lockout`); `CopyPropRuleBreached` alert je vyvolán. Lockout se vymaže když se UTC den vyválí (nový baseline/peak převzat). Sdílí stejný live-equity poll jako account protection.
- **Execution jitter** (C11, výchozí off) — náhodné `0..N` ms zpoždění před umístěním každé kopie, aby se de-korelovaly téměř identické order timestamps v rámci uživatelova **vlastního** účtů. **Compliance caveat:** pomoc pro prop firmy, které *dovolují* kopírování — **ne** nástroj k obcházení firmy, která to zakazuje; zůstávání v rámci pravidel vaší firmy je vaše odpovědnost.
- **Config lock** (C9) — zmrazit nastavení destinace na dobu (`POST …/destinations/{id}/lock` s minutami). Pokud je zamčeno, destinace nemůže být odstraněna (agregace odmítne s `CopyDestinationConfigLocked`) — záměrná ochrana proti impulsivním změnám během drawdownu. Lock vyprší automaticky na jeho časové značce.
- **Consistency pre-alert** (C10) — upozornit (jednou za UTC den) když **denní zisk** destinace dosáhne konfigurovaného procenta denní otevírací equity (`CopyConsistencyThresholdApproaching`), takže se pravidlo consistency prop-firmu respektuje *předtím*, než se spustí. Profit-side, nezávisle na loss-side lockout; běží z stejného day baseline jako prop-rule guard.
- **Order-type filter** — zvolte přesně které master order types kopírovat: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; výchozí všechny). cMAM-styl selektivity.
- **Copy SL / Copy TP** — zrcadlujte master's stop-loss / take-profit, nebo spravujte ochranu nezávisle.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — každý nezávisle toggleable.
- **Copy pending expiry** (výchozí on) — zrcadlujte master pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (výchozí on) — pro market-range + stop-limit ordery, umístěte slave order s master's přesnou slippage-in-points (base cena převzata z live slave spotu).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (přeskočit kopii pokud se slave cena posunula za N pips od master entry). **Max copy delay** měřeno proti master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) pomocí injected `TimeProvider`: signál starší než konfigurovaný max-lag je přeskočen, takže staré kopie nejsou nikdy umístěny pozdě (dříve byla zpoždění vždy nula + guard mrtvý).
- **SL/TP precision normalization** (M6) — zkopírované stop-loss/take-profit ceny zaokrouhleny na **destinace** symbol's digit precision před amendm, takže master cena na jemnější precision (nebo cross-broker digit mismatch) nikdy neobjedná server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — destinace odmítající `CopyDefaults.RejectionBudget` otevření v řadě je **spuštěna**: bez nových otevření pro cooldown window (`CopyDestinationTripped` alert se spustí), zastavení rejection bouře z mladého (prop-firm) účtu. Existující pozice zůstávají spravovány + zavřeny během trip; breaker se auto-resetuje po cooldown + úspěšná kopie vymaže counter.
- **Lot sanity ceiling** (C14) — absolutní max size kopie a/nebo multiple-of-master cap. Vypočítaná kopie překročující absolutní cap, nebo překročující `N×` master's vlastní lot velikost, je **tvrdě-zablokována** (povrchovaná jako `lot_sanity` skip, počítáno na `cmind.copy.skipped`) neum — brání katastrofálně-přehnané třídě (0.23-lot master se obrací na 3 loty na každém receiveru přes runaway multiplier nebo rounding bug). Oba rozměry výchozí `0` (off).

## Spolehlivost & edge cases

Engine je postaven pro realitu, že cokoliv může selhat kdykoliv:

- **Slave-pending fill-correlation timeout** (C13) — zrcadlený slave pending, jehož master pending zmizel (ani nespočívá, ani čerstvě vyplněn) zrušen po correlation timeout, takže slave kopie nemůže vyplnit nekorelován do nespravované pozice (`CopyPendingTimedOut`). Resync také čistí order-id-labelled filled-pending sirotka.
- **Robust close/flatten** (M8) — zavírání sirotka na resync, nebo zplošťování na guard breach, toleruje pozici broker už zavřel (`POSITION_NOT_FOUND`): každé zavření běží nezávisle, takže jedno staré id nikdy neabortuje resync nebo nenechává zbytek účtu un-zplošťen.

- **Start s master už v obchodech** — na start host reconciliuje + otevírá kopie pro existující master pozice.
- **Connection drops / desync** — na reconnect host reconciliuje: otevírá chybějící kopie, zavírá sirotky, znovu označuje pendingní. Bez duplikovaných objednávek.
- **Order placement failure** — selhání na jedné destinaci je zaznamenáno, nikdy neblokuje ostatní destinace.
- **Single valid token per cID** — cTrader zneplatňuje cID's starý access token okamžik nový vydaný. cMind swapuje běžícího hostu's token **na místě** (re-auth na live socket) aby se kopírování pokračovalo bez vypnutí streamu. Viz [token lifecycle](token-lifecycle.md).

## Auditovatelnost

Každá akce emituje strukturovanou, source-generated log event (`LogMessages`) s profile id, destination cID, order/position ids, + hodnoty — order umístěný/přeskočený (s důvodem), partial close, ochrana aplikovaná, trailing aplikovaný, pending umístěný/pozměňovaný/zrušený, expiry zrcadlený, market-range slippage zrcadlený, token swapnutý, resync shrnutí. Toto je audit trail pro compliance + dispute resolution.

Spolu s logy engine emituje **OpenTelemetry metrics** na `cMind.Copy` meter (registrován v shared OTel pipeline, exportován přes OTLP / do Azure Monitor jako zbytek): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out ke všem destinacím, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged by reason), + `cmind.copy.failed`. Ty dělají latency/slippage regression měřitelnou, ne jen viditelnou v log line — live suite ji tvrdí proti rozpočtu.

## API

- `GET /api/copy/profiles` — seznam.
- `POST /api/copy/profiles` — vytvoř (s volitelným destination account ids).
- `GET /api/copy/profiles/{id}` — plný detail včetně každé destinace možnosti.
- `POST /api/copy/profiles/{id}/destinations` — přidej destinaci s kompletní sadu možností.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — odstraň.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Testy

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing módy, decision filtry, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidace. Běží proti `FakeTradingSession`, cTrader-věrný in-memory simulátor.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagace na reálném Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip prostřednictvím API + UI, plný lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testování: seedované randomizované workloady + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) pohánějí `CopyEngineHost` ke klidnosti + tvrdí convergence invarianty. Viz [testing/stress-testing.md](../testing/stress-testing.md). Tato sada na povrch a opravy reálné startup race: `OnReconnected` zapojeny před initial reference-load + resync, takže socket flap během startup mohl spustit druhou resync souběžně + poškodit host's non-concurrent state slovníky — startup load + první resync nyní běží pod `_stateGate`.
- **Live** — reálné cTrader demo účty; viz [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Viz [dev-credentials.md](../testing/dev-credentials.md) pro single credentials soubor live + E2E tiers přečítej.
