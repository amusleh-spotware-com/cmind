---
description: "Zrcali glavni cTrader račun na enega ali več podrejenih računov — cross-broker, cross-cID — s kontrolo na cilj in reconciliacijo denarnega razreda."
---

# Kopiranje trgovanja

Zrcali **glavni** cTrader račun na enega ali več **podrejenih** računov — cross-broker, cross-cID — s kontrolo na cilj in reconciliacijo denarnega razreda.

## Koncepti

- **Profil kopiranja** — en glavni (`SourceAccountId`) + en ali več **ciljev**. Življenjski cikel: `Draft → Running → Paused → Stopped` (`Error` ob napaki). Agregatni koren: `CopyProfile` (ima `CopyDestination`).
- **Cilj** — en podrejeni račun + poln nabor pravil za to, kako se glavni kopira nanj. Vsa konfiguracija na cilj, torej en glavni lahko hrani konzervativnega in agresivnega podrejenega.
- **Gostitelj podvajalnega motorja** — tekoči delavec za profil (`CopyEngineHost`). Se naroči na pretok izvajanja glavnega, uporabi vsak dogodek na vsakem cilju.
- **Supervisor** — `CopyEngineSupervisor`, ozadnji servis na vsakem vozlišču. Vozlišču dodeljeni profili se samozdravijo čez gručo (glej [scaling](../deployment/scaling.md)).

## Kaj se zrcali

| Dogodek glavnega | Dejanje podrejenega |
|------------------|---------------------|
| Tržna / tržna-območje pozicija odprta | Odpri dimenzionirano kopijo (označeno z id izvorne pozicije) |
| Limit / stop / stop-limit čakajoči nalog | Postavi ustrezen čakajoči nalog |
| Sprememba čakajočega naloga | Spremeni zrcaljeni čakajoči nalog na mestu |
| Preklic/potek čakajočega naloga | Prekliči zrcaljeni čakajoči nalog |
| Delno zaprtje | Zapri isto sorazmerje podrejene pozicije |
| Scale-in (povečanje volumna) | Odpri dodani volumen (opt-in) |
| Sprememba stop-loss/trailing-stop | Spremeni zaščito podrejene pozicije |
| Polno zaprtje | Zapri podrejeno kopijo |

Vsaka kopia **označena z id izvorne pozicije/naloga**. Po ponovnem povezovanju gostitelj obnovi stanje iz uskladitve: odpre kopije pozicij, ki jih ima glavni, a manjkajo podrejenemu, zapre podrejenega "siroto", ki ga glavni nima več — **brez podvajanja poslov**.

## Ustvarjanje profila

**Nov profil** dialog na strani kopiranja trgovanja zbere vse naenkrat: ime profila, vir (glavni račun), cilj (podrejeni računi) (multi-select s gumbom **Izberi vse**; izbrani glavni je izvzet iz seznama podrejenih), + poln nabor možnosti na cilj spodaj. Vsi vnosi **validirani pred shranjevanjem** — manjkajoče ime/vir/cilj, nepozitivni dimenzijski parametri, negativni/nesklenjeni meji lota, odstotek izven obsega, nobena vrsta naloga ni omogočena, prazen filter simbolov, ali napačno oblikovani pari simbolov se prikažejo kot seznam napak + blokirajo shranjevanje. Ob potrditvi, profil ustvarjen + vsak izbrani podrejen dodan z izbranimi nastavitvami.

Vrstične akcije spoštujejo življenjski cikel: **Začni** omogočeno samo ko ne teče, **Ustavi** + **Pavziraj** samo ko teče, **Izbriši** onemogočeno med tekom + zahteva potrditev pred odstranitvijo profila + ciljev.

## Možnosti na cilj

Nastavljivo v dialogu Novega profila, na panelu profila kopiranja na strani, ali prek `POST /api/copy/profiles/{id}/destinations`:

- **Dimenzioniranje** (`MoneyManagementMode` + parameter): fiksen lot, lot/notional množilnik, sorazmerno bilanco/equity/prosto maržo, fiksen tveganja %, fiksna vzvod, avtomatsko sorazmerno, **tveganja-%-od-stop** (M7). Plus min/max meje lota + prisili-min-lot. **Tveganja-od-stop** dimenzionira cilj tako, da tvega konfiguriran odstotek **svoje** bilance, izpeljan iz **razdalje glavnega stop-loss** (`glavni tvega 2% → podrejeni avtomatsko tvega 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Glavni odprt **brez** stop-loss nima razdalje za dimenzioniranje → uporabi konfiguriran **max-tveganja fallback lot** (M7) če nastavljeno, sicer preskoči (`no_stop_loss`). Sorazmerno-**equity**/**prosto-maržo** dimenzioniranje iz resnične **equity** računa (`balance + Σ floating P&L`, izpeljano iz cTrader Open API ki ne dostavlja equity), ne navadne bilance — torej glavni na odprtem dobičku/izgubi pravilno dimenzionira kopije. Uporabljena marža ni razkrita prek API za uskladitev, torej prosta marža obravnavana kot equity (pošten približek razpoložljivih sredstev); drugi načini berejo bilanco + preskočijo dodatno revalvacijo.
- **Filter smeri**: oba / samo dolg / samo kratek. **Obrni**: obrne stran (+ zamenjaj SL↔TP) za kontrarian copy.
- **Samo-upravljanje** (Ignore-New-Trades / Close-Only): zrcali zaprtja, delna zaprtja + zaščitne spremembe na že kopiranih pozicijah, vendar **ne odpira** novih pozicij/čakajočih nalogov (preskočeno `manage_only`). Uporabi za zmanjšanje cilja brez rezanja obstoječih kopij.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (privzeto vključeno): ob **prvi** uskladitvi profila, ali odpreti kopije za obstoječe pozicije glavnega, + ali zapreti kopije, ki jih je glavni zaprl medtem ko je bil profil ustavljen. Oba veljata samo na začetku — med delovanjem ponovno povezovanje vedno popolnoma uskladi, da se razhajanja popravijo.
- **Zemljevid simbolov** + **filter simbolov** (bela lista / črna lista). Vsak vnos zemljevida simbolov ima izbirno **prilagojen množilnik volumna na simbol** (cMAM nadomestitev na simbol) ki spreminja velikost kopije za ta simbol na vrhu ciljevega dimenzioniranja (1 = brez spremembe). Celoten zemljevid se uvozi/izvozi kot **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; stolpci `Source,Destination,VolumeMultiplier`) — vsaka vrstica validirana skozi domenske vrednostne objekte, torej napačna datoteka ne more proizvesti neveljavnega zemljevida.
- **Časovno okno trgovanja** (C18) — na cilj dnevno UTC okno (`start`/`end` minute dneva, end ekskluziven; `start == end` = ves dan). Nova odprtja zunaj okna preskočena (`trading_hours`); okno z `start > end` se ovije čez polnoč (npr. 22:00–06:00). Obstoječe pozicije ostanejo upravljane.
- **Filter oznake vira** (C18, ekvivalent MT magic-number filtra cTrader) — ko nastavljeno, kopiraj samo trgovine glavnega katerih oznaka se natančno ujema (npr. enega bot-a trgovine, ali samo ročna oznaka); sicer preskočeno (`source_label`). Prazen = kopiraj vse. Nosi se na `ExecutionEvent.SourceLabel` iz pozicije/naloga glavnega `TradeData.Label`, spoštuje se tud pri uskladitvi.
- **Zaščita računa** (ZuluGuard / Global Account Protection) — spremljaj **živo equity** cilja (`balance + Σ floating P&L`, anketirano vsak `CopyDefaults.EquityGuardInterval`) proti tlom `StopEquity` in/ali izbirni strehi `TakeEquity`. Ob prelomu, uporabi način: **CloseOnly** (ustavi nove kopije, upravljaj obstoječe), **Frozen** (ustavi odprtja), **SellOut** (zapri **vse** kopije na cilju takoj). Ko sproženo, cilj zaklenjen — nobena nova odprtja dokler gostitelj ne reštartira — + `CopyAccountProtectionTriggered` alert dvignjen. `SellOut` zahteva `StopEquity`; `TakeEquity` mora biti nad `StopEquity`. **Brez garancije opozorilo:** prodaja uporablja tržno izvršitev — kot vsaka konkurenčna enakratna, ne more garantirati cene izpolnitve v hitrem/tržnem presledku.
- **Panik gumb Zapri-vse** (C8) — `POST /api/copy/profiles/{id}/flatten` takoj zapre **vse** kopirane pozicije na vsakem cilju + zaklene proti novim odprtjem. Usmerjeno cross-process: API nastavi zastavico, supervisor dostavi tekočemu gostitelju (povrne kanal za rotacijo žetona), ki flattenira na mestu; zastavica počistena torej sproži natanko enkrat (`CopyFlattenAll` alert). Uporabnik nato pavziraj/ustavi profil.
- **Varovalka pravil prop firm** (C7) — uveljavljanje, ki ga zahtevajo uporabniki prop firm kopiranj. Na cilj, **dnevna izguba limit** (izguba od odprtja equity dneva) in/ali **sledeča izguba limit** (izguba od tekočega vrha equity), oba v depozitni valuti. Ob prelomu cilj **avtomatsko flatteniran** (vse kopije zaprte) + **zaklenjen** preostanek UTC dneva (nova odprtja preskočena `prop_lockout`); `CopyPropRuleBreached` alert sprožen. Zaklep se počisti ko se UTC dan zavrti (nova osnova/vrh vzeta). Deli isto živo equity anketo kot zaščita računa.
- **Izvršitveni jitter** (C11, privzeto izključeno) — naključni `0..N` ms zamik pred postavitvijo vsake kopije, za dekorelacijo skoraj enakih časovnih žigov naročil čez **lastne** račune uporabnika. **Opomba o skladnosti:** pomoč za prop firme ki *dovoljujejo* kopiranje — **ne** orodje za izogibanje firmi ki prepoveduje; ostajanje znotraj pravil tvoje firme je tvoja odgovornost.
- **Zaklep konfiguracije** (C9) — zamrznitev nastavitev cilja za obdobje (`POST …/destinations/{id}/lock` z minutami). Med zaklenitvijo cilj ne more biti odstranjen (agregat zavrne z `CopyDestinationConfigLocked`) — namerna varovalka proti impulzivnim spremembam med izgubo. Zaklep poteče avtomatsko ob časovnem žigu.
- **Opozorilo konsistentnosti** (C10) — opozori (enkrat na UTC dan) ko **dnevni dobiček** cilja doseže konfiguriran odstotek odprtja equity dneva (`CopyConsistencyThresholdApproaching`), tako da se spoštuje pravilo konsistentnosti prop firme *preden* sproži. Dobičkowa stran, neodvisna od izgubne strani zaklepanja; teče na isti dnevni osnovi kot prop-rule varovalka.
- **Filter vrste naloga** — izberi natančno katere vrste nalogov glavnega kopirati: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` zastavice; privzeto vse). cMAM-stil selektivnost.
- **Kopiraj SL / Kopiraj TP** — zrcali stop-loss / take-profit glavnega, ali upravljaj zaščito neodvisno.
- **Kopiraj trailing stop**, **zrcali delno zaprtje**, **zrcali scale-in** — vsak neodvisno preklopljiv.
- **Kopiraj potek čakajočega** (privzeto vključeno) — zrcali čas poteka Good-Till-Date čakajočega naloga glavnega.
- **Kopiraj slippage glavnega** (privzeto vključeno) — za market-range + stop-limit naloge, postavi podrejen nalog z natančnim slippage-v-točkah glavnega (bazna cena vzeta iz žive spot podrejenega).
- **Varovalke**: max drawdown %, dnevni izguba limit, max copy delay, slippage filter (preskoči copy če se je podrejena cena premaknila več kot N pips od glavnega vstopa). **Max copy delay** merjeno proti realnemu časovnemu žigu strežnika glavnega dogodka (`ExecutionEvent.ServerTimestamp`) prek injiciranega `TimeProvider`: signal starejši od konfiguriranega max-lag preskočen, torej zastarele kopije nikoli ne postavljene pozno (prej zamuda vedno nič + varovalka mrtva).
- **Normalizacija natančnosti SL/TP** (M6) — kopirani stop-loss/take-profit zaokrožen na **ciljevo** natančnost števk simbola pred spremembo, torej natančnejša cena glavnega (ali cross-broker neujemanje števk) nikoli ne sproži strežnikove `INVALID_STOPLOSS_TAKEPROFIT`.
- **Varovalka za zavrnitev / Follower Guard** (G8) — cilj ki zavrne `CopyDefaults.RejectionBudget` odprt zaporedoma je **tript**: nobena nova odprtja za cooldown okno (`CopyDestinationTripped` alert sprožen), ustavi zavrnitveno nevihto ki tolče (prop-firm) račun. Obstoječe pozicije še upravljane + zaprte med triptim; varovalka avtomatsko resetira po cooldown + uspešni copy počisti števec.
- **Zgornja meja lota** (C14) — absolutni max velikost kopije in/ali večkratnik velikosti glavnega. Izračunana kopia ki presega absolutni cap, ali presega `N×` velikost lota glavnega, **težko-blokirana** (prikazano kot `lot_sanity` preskok, štet na `cmind.copy.skipped`) ne postavljena — brani pred katastrofalno-nadmerno velikostjo razreda (glavni 0.23 lota ki postane 3 loti na vsakega prejemnika prek divjega množilnika ali zaokrožitvene hrošča). Obe dimenziji privzeto `0` (izključeno).

## Zanesljivost in robni primeri

Motor zgrajen za realnost, kjer lahko karkoli kadar koli propade:

- **Korelacijski timeout čakajoče podrejenega** (C13) — zrcaljena podrejena čakajoča katere glavna čakajoča izginila (ne počiva niti svežnje napolnjena) preklicana po korelacijskem timeoutu, torej podrejena kopia ne more napolniti nekorreltirano v neupravljano pozicijo (`CopyPendingTimedOut`). Uskladitev prav tako čisti id-naloga označeno napolnjeno-čakajočo siroto.
- **Robustno zaprtje/flatten** (M8) — zapiranje sirote na uskladitvi, ali flatteniranje ob prelomu varovalke, tolerira da je pozicija broker že zaprta (`POSITION_NOT_FOUND`): vsako zaprtje teče neodvisno, torej ena zastarela id nikoli ne prekine uskladitve ali pusti preostanek računa ne-flatteniranega.

- **Začetek z glavnim že v poslih** — ob začetku gostitelj uskladi + odpre kopije za obstoječe pozicije glavnega.
- **Pad povezave / razhajanje** — ob ponovnem povezovanju gostitelj uskladi: odpri manjkajoče kopije, zapri sirote, ponovno označi čakajoče. Brez podvojenih naročil.
- **Neuspeh postavitve naročila** — neuspeh na enem cilju beležen, nikoli ne blokira drugih ciljev.
- **En veljaven žeton na cID** — cTrader razveljavi star dostopovni žeton cID v trenutku ko se izda nov. cMind zamenja tekočega gostitelja žeton **na mestu** (re-auth na živem vtiču) tako da kopiranje nadaljuje brez padca pretoka. Glej [življenjski cikel žetona](token-lifecycle.md).

## Sledljivost

Vsako dejanje oddaja strukturiran, vir-generiran dnevniški dogodek (`LogMessages`) s profil id, cilj cID, id naročila/pozicije, + vrednosti — naročilo postavljeno/preskočeno (z razlogom), delno zaprtje, zaščita uporabljena, trailing uporabljen, čakajoč postavljen/spremenjen/preklican, potek zrcaljen, slippage zrcaljen, žeton zamenjan, uskladitev povzeta. To je sled za skladnost + razreševanje sporov.

Poleg dnevnikov, motor oddaja **OpenTelemetry metrike** na `cMind.Copy` metru (registriran v skupni OTel pipline, izvožen prek OTLP / v Azure Monitor kot ostalo): `cmind.copy.latency` (glavni-dogodek → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out na vse cilje, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (označeno po cilju), `cmind.copy.skipped` (označeno po razlogu), + `cmind.copy.failed`. To naredi latentnost/slippage regresijo merljivo, ne samo vidno v dnevniški vrstici — živi suite trdi proti proračunu.

## API

- `GET /api/copy/profiles` — seznam.
- `POST /api/copy/profiles` — ustvari (z izbirnimi id-ji ciljnih računov).
- `GET /api/copy/profiles/{id}` — polna podrobnost incl. vsaka možnost cilja.
- `POST /api/copy/profiles/{id}/destinations` — dodaj cilj s polnim naborom možnosti.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — odstrani.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — življenjski cikel.

## Testi

- **Enote** (`tests/UnitTests/CopyTrading`) — dimenzioniranje, odločitveni filtri, filter vrste naloga, potek, slippage market-range/stop-limit, SL/TP preklopniki, delno zaprtje, sprememba/preklic čakajočega, začetek z odprtimi, disconnect→razhajanje→resync, zamenjava žetona na mestu, cross-cID razveljavitev. Teče proti `FakeTradingSession`, cTrader-veren in-memory simulator.
- **Integracija** (`tests/IntegrationTests/CopyLive`) — vozlišče-afiniteta/zahtevek lease, propagacija različice žetona na resničnem Postgres.
- **E2E** (`tests/E2ETests`) — round-trip možnosti cilja prek API + UI, poln življenjski cikel.
- **Stress / DST** (`tests/StressTests`) — deterministično simulacijsko testiranje: sejani naključni delovni obremenitvi + vbrizgane napake (socket flap, zavrnitev naročila, zavrnitev market-range, rotacija žetona, smrt vozlišča) poganje `CopyEngineHost` v stanje tišine + trdijo konvergenčne invariante. Glej [testing/stress-testing.md](../testing/stress-testing.md). Ta suite je razkrila + popravila realno start-up race: `OnReconnected` napeljan pred initial reference-load + prvo resync, torej socket flap med start-up je lahko tekel drug resync sočasno + pokvaril ne-concurrent state dictionary gostitelja (`_symbolDetails`, `_sourceVolumes`) — popravljeno: start-up load + prvi resync zdaj tečeta pod `_stateGate`.
- **Live** — realni cTrader demo računi; glej [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Glej [dev-credentials.md](../testing/dev-credentials.md) za enotno datoteko poverilnic live + E2E tiers bereta.
