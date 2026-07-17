---
description: "Zrcadlení hlavního účtu cTrader na jeden nebo více vedlejších účtů — cross-broker, cross-cID — s ovládáním na jednotlivé cíle a finanční-kvalitní smírením."
---

# Kopírování obchodů

Zrcadlení **hlavního** účtu cTrader na jeden nebo více **vedlejších** účtů — cross-broker, cross-cID — s ovládáním na jednotlivé cíle a finanční-kvalitní smírením.

## Koncepce

- **Profil kopírování** — jeden hlavní (`SourceAccountId`) + jeden nebo více **cílů**. Životní cyklus: `Draft → Running → Paused → Stopped` (`Error` při selhání). Agregační kořen: `CopyProfile` (vlastní `CopyDestination`).
- **Cíl** — jeden vedlejší účet + kompletní sada pravidel pro způsob kopírování hlavního na něj. Veškerá konfigurace na jednotlivé cíle, takže jeden hlavní účet může vytvářet konzervativní i agresivní kopie současně.
- **Hostitel kopírování** — běžící worker pro profil (`CopyEngineHost`). Odebírá stream spuštění hlavního, aplikuje každou událost na všechny cíle.
- **Supervizor** — `CopyEngineSupervisor`, služba na pozadí na každém uzlu. Hostuje přiřazené profily, vlastní léčení v celém clusteru (viz [škálování](../deployment/scaling.md)).

## Co se zrcadlí

| Hlavní událost | Akce vedlejšího |
|--------------|--------------|
| Otevřená pozice na trhu / trž-ním rozsahu | Otevřít velikostnou kopii (označenou id zdrojové pozice) |
| Pending order limit / stop / stop-limit | Umístit odpovídající pending order |
| Úprava pending orderu | Upravit zrcadlený pending order na místě |
| Zrušení pending orderu / vypršení | Zrušit zrcadlený pending order |
| Částečné zavření | Zavřít stejný podíl vedlejší pozice |
| Scale-in (zvýšení objemu) | Otevřít přidaný objem (opt-in) |
| Změna stop-loss / trailing-stop | Upravit ochranu vedlejší pozice |
| Úplné zavření | Zavřít vedlejší kopii |

Každá kopie **označena zdrojovým id pozice/orderu**. Po znovupřipojení hostitel znovu sestaví stav ze smíření: otevře kopie, které hlavní drží, ale vedlejší chybí, zavře vedlejší "osamělé", které hlavní již nedrží — **bez duplicitní obchodů**.

## Vytvoření profilu

**Nový profil** otevírá dedikovaný **formulář přes celou stránku** (`/copy-trading/new`), nikoli dialog — sada možností je dostatečně velká na to, aby se stránka lépe četla na telefonu i desktopu. Sběrá vše předem: název profilu, zdrojový (hlavní) účet, cílové (vedlejší) účty (multi-select s tlačítkem **Vybrat vše**; vybraný hlavní je vyloučen ze seznamu vedlejších), + úplná sada možností na jednotlivé cíle. **Pouze účty propojené přes cTrader Open API jsou volitelné** jako hlavní nebo cíl — kopírování umisťuje objednávky přes Open API, takže ručně přidaný (pouze cID) účet nemůže kopírovat a není uveden v seznamu; pokud nejsou propojeny žádné účty, stránka zobrazí upozornění odkazující na Obchodní účty. Módy velikosti, směr a filtr symbolů se **vykreslují jako etikety pro lidi** s **vysvětlením s odrážkami na mode** na podpoře správy peněz. **Každý prvek nese nápovědu** vysvětlující, co dělá a jak jej používat. Strukturované vstupy používají **správné ověřené prvky** — čísla/procenta pomocí numerických polí, módy/směr/filtr pomocí výběrů, filtr symbolu pomocí seznamu přidávání/odebírání symbolů a mapování symbolů pomocí tabulky přidávání/odebírání řádků `Zdroj → Cíl (× násobitel)` — nikdy textový soubor oddělený čárkami. Veškeré vstupy **ověřeny před uložením** — chybějící název/zdroj/cíl, ne-kladný parametr velikosti, záporné/nekonzistentní limity lotu, mimo-rozsahové procento drawdownu, žádný typ orderu povolen, nebo prázdný filtr symbolu se zobrazují jako seznam chyb + zablokují uložení. Při vytváření je profil vytvořen + každý vybraný vedlejší přidán se zvolenými nastavením, poté se stránka vrátí na seznam Kopírování obchodů.

**Import / export.** Celý blok nastavení lze **exportovat do souboru JSON** a znovu **importovat** pro předvyplnění formuláře, takže vyladěné nastavení lze použít znovu v profilech bez opětovného psaní. Mapování symbolů lze rovněž **exportovat / importovat jako soubor CSV** (`Source,Destination,VolumeMultiplier`) — připravte si velkou mapu symbolů makléře v tabulkovém procesoru a načtěte ji v jednom kroku. Stejné ovládání symbolů a import/export CSV jsou k dispozici také v dialogu cíle na stránce Kopírování obchodů.

Akce řádků respektují životní cyklus: **Spustit** povoleno pouze pokud není spuštěno, **Zastavit** + **Pozastavit** pouze pokud je spuštěno, **Smazat** zakázáno během chodu + žádá potvrzení před odebráním profilu + cílů.

## Možnosti na jednotlivé cíle

Nastavte na stránce Nový profil, v dialogu cíle na stránce Kopírování obchodů, nebo přes `POST /api/copy/profiles/{id}/destinations`:

- **Velikost** (`MoneyManagementMode` + parametr): fixní lot, lot/nominální násobitel, proporční bilance/equity/volná marže, fixní riziko %, fixní páka, automaticky proporční, **riziko-%-z-stop** (M7). Plus min/max limity lotu + vynucení-min-lotu. **Riziko-z-stop** nastaví velikost cíle tak, aby riskoval konfigurované procento *jeho vlastní* bilance, odvozené z **vzdálenosti stop-loss hlavního** (`hlavní riskuje 2% → vedlejší automaticky riskuje 2%`): `loty = bilance×% ÷ (vzdálenostStop × smluvníVelikost)`. Hlavní otevřená **bez** stop-loss nemá vzdálenost pro určení velikosti → používá konfigurovaný **maximální-riziko fallback lot** (M7) je-li nastaven, jinak přeskočen (`no_stop_loss`) není hádán. Proporční-**equity**/**volná-marže** určují velikost z reálné **equity** účtu (`bilance + Σ plovoucí P&L`, odvozené z cTrader Open API, která neukazuje equity), ne pouhé bilance — takže hlavní sedící na otevřeném zisku/ztrátě má správné velikosti kopií. Použitá marže není k dispozici přes API smíření, takže volná marže se považuje za equity (čestný proxy dostupných prostředků); ostatní módy čtou bilanci + přeskakují další kolo revalorizace.
- **Filtr směru**: oba / jen dlouhé / jen krátké. **Obrácení**: převrátit stranu (+ vyměnit SL↔TP) pro kontrastující kopii.
- **Spravovat-pouze** (Ignorovat-nové-obchody / Pouze-zavírání): zrcadlit zavírání, částečné zavírání + změny ochrany na již zkopírovaných pozicích, ale otevřít **žádné** nové pozice/pending ordery (přeskočeny `manage_only`). Použijte k postupnému uzavírání cíle bez ořezávání stávajících kopií.
- **Sync-otevřeno-na-start** / **Sync-zavřeno-na-start** (standardně zapnuto): při **prvním** smíření profilu, zda otevřít kopie pro předem existující pozice hlavního, + zda zavřít kopie, které hlavní zavřel, když byl profil zastaven. Oba se používají pouze na začátku — mid-run znovupřipojení vždy plně smíruje, takže se desynchronizace obnoví bez ohledu.
- **Mapování symbolů** + **filtr symbolů** (seznam povolených / seznam zakázaných). Každý záznam mapování symbolů nese volitelný **násobitel objemu na symbol** (cMAM přepis na symbol) měřítko velikosti kopie pro daný symbol nad určitostí velikosti cíle (1 = bez změny). Celé mapování importuje/exportuje jako **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; sloupce `Source,Destination,VolumeMultiplier`) — každý řádek je ověřován přes objekty domény, takže zformátovaný soubor nemůže vytvořit neplatné mapování.
- **Okno obchodovacích hodin** (C18) — denní okno UTC na cíl (`start`/`end` minut dne, konec exkluzivní; `start == end` = celodenní). Nová otevření mimo okno přeskočena (`trading_hours`); okno s `start > end` přechází přes půlnoc (např. 22:00–06:00). Stávající pozice zůstávají spravovány.
- **Filtr zdrojového popisku** (C18, ekvivalent cTrader filtru magic-number v MT) — pokud je nastaven, kopírovat pouze hlavní obchody, jejichž popisek odpovídá **přesně** (např. obchody jednoho bota, nebo popis pouze manuálních); jinak přeskočeny (`source_label`). Prázdné = kopírovat vše. Přenesen na `ExecutionEvent.SourceLabel` z `TradeData.Label` pozice/orderu hlavního, respektován také při smíření.
- **Ochrana účtu** (ZuluGuard / Globální ochrana účtu) — sledovat **živou equity** cíle (`bilance + Σ plovoucí P&L`, průzkum každý `CopyDefaults.EquityGuardInterval`) proti domu `StopEquity` a/nebo volitelný strop `TakeEquity`. Při porušení aplikujte mód: **CloseOnly** (zastavit nové kopie, udržovat stávající), **Frozen** (zastavit otevírání), **SellOut** (zavřít **každou** kopii na cíli okamžitě). Jakmile se aktivuje, cíl se uzamkne — žádné nové otevření, dokud se hostitel nerestartuje — + je zvýšena výstraha `CopyAccountProtectionTriggered`. `SellOut` vyžaduje `StopEquity`; `TakeEquity` musí být nad `StopEquity`. **Bez záruk**: prodej používá tržní spuštění — jako každá konkurenční ekvivalent, nemůže zaručit cenu vyplnění na rychlém/mezerinkovém trhu.
- **Flatten-All tlačítko paniky** (C8) — `POST /api/copy/profiles/{id}/flatten` okamžitě zavře **každou** kopírovanou pozici na každém cíli + zamkne proti novým otevřením. Směrováno cross-procesem: API nastaví příznak, supervizor doručí spuštěnému hostiteli (opět používá kanál rotace tokenu), který zarovnává na místě; příznak vymazán, takže se aktivuje přesně jednou (`CopyFlattenAll` výstraha). Uživatel pak pozastaví/zastaví profil.
- **Prop-firma guard pravidel** (C7) — vynucování, které si žádají uživatelé kopírování prop-firm. Na cíl, **denní limit ztráty** (ztráta z otevření equity na den) a/nebo **trailing-drawdown** limit (ztráta z běžící špičkové equity), oboje v měně vkladu. Při porušení se cíl **automaticky zarovná** (každá kopie zavřena) + **uzamkne** zbytek UTC dne (nová otevření přeskočena `prop_lockout`); `CopyPropRuleBreached` výstraha se aktivuje. Uzamčení se vymaže, když se UTC den otočí (bere se nový základ/špička). Sdílí stejný live-equity průzkum jako ochrana účtu.
- **Jitter spuštění** (C11, standardně vypnuto) — náhodné zpoždění `0..N` ms před umístěním každé kopie, aby se dekorelovat skoro identické časové značky objednávky mezi vlastními **vlastními** účty uživatele. **Soulad upozornění**: pomoc pro prop-firmy, které *povolují* kopírování — **ne** nástroj k vyhnutí se firmě, která to zakazuje; dodržování pravidel vaší firmy je vaše zodpovědnost.
- **Zámek konfiguraci** (C9) — zmrazit nastavení cíle na dobu (`POST …/destinations/{id}/lock` s minutami). Během uzamčení se cíl nemůže odebrat (agregace odmítá pomocí `CopyDestinationConfigLocked`) — úmyslný guard proti impulzivním změnám během drawdownu. Zámek automaticky vyprší v jeho časové značce.
- **Předalerta konzistence** (C10) — upozornit (jednou za UTC den), když denní zisk cíle dosáhne konfigurovaného procenta otevření equity na den (`CopyConsistencyThresholdApproaching`), takže se pravidlo konzistence prop-firmy respektuje *před* jeho aktivací. Ziskový-straně, nezávislý na straně ztráty; běží na stejném denním základu jako guard pravidel prop-firmy.
- **Filtr typu orderu** — zvolte přesně, které hlavní typy objednávek kopírovat: tržní, rozsah trhu, limit, stop, stop-limit (`CopyOrderTypes` příznaky; standardně všechny). cMAM-styl selektivity.
- **Kopírovat SL / Kopírovat TP** — zrcadlit stop-loss / take-profit hlavního, nebo spravovat ochranu nezávisle.
- **Kopírovat trailing stop**, **zrcadlit částečné zavření**, **zrcadlit scale-in** — každé nezávisle přepínaný.
- **Kopírovat pending vypršení** (standardně zapnuto) — zrcadlit hlavní pending orderu Good-Till-Date časové značce vypršení.
- **Kopírovat slippage hlavního** (standardně zapnuto) — pro market-range + stop-limit ordery, umístit vedlejší order s přesným slippagem hlavního-v-pipech (základní cena vzata ze živého spotu vedlejšího).
- **Guardy**: maximum drawdown %, denní limit ztráty, maximum copy zpoždění, filtr slippagu (přeskočit kopii, pokud se vedlejší cena posunula za N pipech od vstupu hlavního). **Maximum copy zpoždění** měřeno proti reálné časové značce serveru hlavní události (`ExecutionEvent.ServerTimestamp`) přes injektovaný `TimeProvider`: signál starší než konfigurované maximum zpoždění přeskočen, takže zastaralá kopie není nikdy umístěna později (dříve byla zpoždění vždy nula + guard mrtvý).
- **SL/TP precizní normalizace** (M6) — zkopírované stop-loss/take-profit ceny zaokrouhleny na **cílový** symbol číselné presnosti před úpravou, takže hlavní cena s jemnější presností (nebo cross-broker číselné nesoulad) nikdy neutopí server `INVALID_STOPLOSS_TAKEPROFIT`.
- **Okruh breaker odmítnutí / Follower Guard** (G8) — cíl odmítající `CopyDefaults.RejectionBudget` otevření za sebou je **spuštěn**: žádné nové otevření po dobu cooldown okna (`CopyDestinationTripped` výstraha se aktivuje), zastavování odmítnutí bouře z kladívání (prop-firma) účtu. Stávající pozice jsou stále spravovány + zavřeny během spuštění; breaker se automaticky resetuje po cooldown + úspěšné kopii vymaže počítadlo.
- **Sanitární strop lotu** (C14) — absolutní maximum velikosti kopie a/nebo vícenásobek-hlavního cap. Vypočtená kopie překročující absolutní cap, nebo překročující `N×` vlastní lot velikost hlavního, je **tvrdě zablokován** (zobrazen jako `lot_sanity` přeskočit, počítáno na `cmind.copy.skipped`) není umístěn — chrání před katastrofálním-nadměrným třídou (0.23-lot hlavní obrácený v 3 lotech na každého příjemce přes runaway násobitel nebo zaokrouhlovací chybu). Oba rozměry výchozí `0` (vypnuto).

## Spolehlivost a okrajové případy

Engine postavený pro realitu, že cokoli může selhat kdykoli:

- **Pending fill-correlation timeout vedlejšího** (C13) — zrcadlený vedlejší pending, jehož hlavní pending zmizel (ani nerotí ani se čerstvě nevyplnil), zrušen po correlation timeout, takže vedlejší kopie nemůže vyplnit nekorelované do neřízené pozice (`CopyPendingTimedOut`). Smíření také čistí order-id-labeled vyplnění-pending osamělého.
- **Robustní zavírání/zarovnání** (M8) — zavírání osamělého při smíření, nebo zarovnání na guard porušení, snáší pozici, kterou makléř již zavřel (`POSITION_NOT_FOUND`): každé zavření běží nezávisle, takže jedno zastaralé id nikdy nepřeruší smíření nebo neponechá zbytek účtu nezarovnaný.

- **Spustit s hlavním již v obchodech** — při spuštění hostitel smíruje + otevírá kopie pro existující pozice hlavního.
- **Pokles připojení / desynchronizace** — při znovupřipojení hostitel smíruje: otevírá chybějící kopie, zavírá osamělé, znovu označuje pendings. Žádné duplicitní ordery.
- **Selhání umístění orderu** — selhání na jednom cíli zaznamenáno, nikdy neblokuje ostatní cíle.
- **Jediný platný token na cID** — cTrader zneplatní starý přístupový token cID, jakmile je vydán nový. cMind zaměňuje token běžícího hostitele **na místě** (re-autentifikace na živé soketu) tak, aby kopírování pokračovalo bez vypuštění streamu. Viz [životní cyklus tokenu](token-lifecycle.md).

## Auditability

Každá akce vyzařuje strukturované, generované ze zdroje logistické události (`LogMessages`) s id profilu, cID cíle, id orderu/pozice, + hodnoty — order umístěn/přeskočen (s důvodem), částečné zavření, aplikovaná ochrana, aplikovaný trailing, pending umístěn/upraven/zrušen, expiraci zrcadlena, slippage rozsahu trhu zrcadlen, token zaměněn, souhrn smíření. Toto je auditní stopa pro soulad + řešení sporů.

Spolu s logem engine vyzařuje **OpenTelemetry metriky** na měřidlo `cMind.Copy` (registrováno v sdíleném OTel kanálu, exportováno přes OTLP / do Azure Monitor jako zbytek): `cmind.copy.latency` (hlavní-událost → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out na všechny cíle, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (označeno podle cíle), `cmind.copy.skipped` (označeno podle důvodu), + `cmind.copy.failed`. Ty činí regresí latence/slippagu měřitelnou, ne jen viditelnou v logistickém řádku — živá souprava tvrdá je proti rozpočtu.

## API

- `GET /api/copy/profiles` — seznam.
- `POST /api/copy/profiles` — vytvořit (s volitelným id účtu cíle).
- `GET /api/copy/profiles/{id}` — úplný detail inkl. každá volba cíle.
- `POST /api/copy/profiles/{id}/destinations` — přidat cíl s úplnou soupravou možností.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — odebrat.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — životní cyklus.

## Testy

- **Jednotka** (`tests/UnitTests/CopyTrading`) — módy velikosti, filtry rozhodnutí, filtr typu orderu, expirace kopie, slippage rozsahu trhu/stop-limit, SL/TP přepínače, částečné zavření, pending úprava/zrušení, start-s-otevřením, disconnect→desync→resync, in-place token swap, cross-cID zneplatnění. Běží proti `FakeTradingSession`, věrný simulátor cTrader v paměti.
- **Integrace** (`tests/IntegrationTests/CopyLive`) — afinita uzlu/nárok leasingu, propagace verze tokenu na skutečné Postgres.
- **E2E** (`tests/E2ETests`) — round-trip možnosti cíle přes API + UI, úplný životní cyklus.
- **Stres / DST** (`tests/StressTests`) — deterministic-simulation testování: seed náhodné pracovní zátěže + fault injection (socket flap, odmítnutí objednávky, odmítnutí rozsahu trhu, rotace tokenu, smrt uzlu) pohon `CopyEngineHost` ke klidu + tvrdí konvergence invarianty. Viz [testování/stress-testing.md](../testing/stress-testing.md). Tato souprava surfaced + fixed real startup race: `OnReconnected` zapájena před počáteční referenční zátěž + resync, takže socket flap během spuštění by mohla spustit druhé resync souběžně + poškodit non-concurrent státní slovníky hostitele — spuštění zátěž + první resync nyní běží pod `_stateGate`.
- **Živá** — skutečné cTrader demo účty; viz [testování/live-copy-trading.md](../testing/live-copy-trading.md).

Viz [dev-credentials.md](../testing/dev-credentials.md) pro jednoduchý soubor pověření živé + E2E tiers čtení.

## Ovládání profilu a správa cíle

Spustit/zastavit jsou tlačítka ikon na každém řádku profilu (zakázáno, když akce neplatí). Zdrojový a cílový účty jsou zobrazeny jejich **číslem účtu**, nikdy interním id. Kliknutí na profil otevírá **dialog** pro správu jeho cílových účtů (přidat/odebrat s úplnými nastavením na jednotlivé cíle).
