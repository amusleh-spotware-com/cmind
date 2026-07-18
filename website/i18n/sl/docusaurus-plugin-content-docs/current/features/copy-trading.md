---
description: "Zrcali glavni račun cTrader na enega ali več podredenih računov — čez borzne hiše, čez cID — s kontrolo po ciljih + finančno natančnostjo."
---

# Kopiranje trgovanja

Zrcalite **glavni** račun cTrader na enega ali več **podredenih** računov — čez borzne hiše, čez cID — s kontrolo po ciljih + finančno natančnostjo.

## Pojmi

- **Profil kopiranja** — en glavni (`SourceAccountId`) + en ali več **ciljev**. Življenjski ciklus: `Draft → Running → Paused → Stopped` (`Error` ob napaki). Koren agregata: `CopyProfile` (lastnik `CopyDestination`).
- **Cilj** — en podrejeni račun + popoln nabor pravil, kako se glavni kopira nanj. Vsa konfiguracija po cilju, zato en glavni lahko hkrati napaja konzervativne in agresivne podrejene.
- **Pogon za kopiranje** — delujoči delavec za profil (`CopyEngineHost`). Se naroči na tok izvršitve glavnega, vsak dogodek uporabi za vse cilje.
- **Nadglednik** — `CopyEngineSupervisor`, storitev v ozadju na vsakem vozlišču. Gosti dodeljene profile, samopopravi se čez grupo (glejte [skaliranje](../deployment/scaling.md)).

## Kaj se zrcali

| Dogodek glavnega | Dejanje podrejenega |
|--------------|--------------|
| Pozicija na trgu / pozicija na območju trgovanja odprta | Odpri kopijo velikosti (označeno s ID-om izvirne pozicije) |
| Naročilo v prihodnosti / naročilo s stopnjo / naročilo s stopnjo in zaustavljanjem čakajočo | Postavi ujemajoče se čakajoče naročilo, prenašajoče zaustavitev-ob-izgubi / izkoristek glavnega |
| Sprememba čakajočega naročila | Spremeni čakajoče naročilo podrejenega na mestu (vključno z njegovo zaustavitvijo-ob-izgubi / izkoriščkom) |
| Preklic čakajočega naročila / izteka | Prekliči čakajoče naročilo podrejenega |
| Delna zapora | Zapri enak delež pozicije podrejenega |
| Povečanje volumna | Odpri dodani volumen (možno) |
| Sprememba zaustavitve ob izgubi / izgube z zaostajanjem | Spremeni zaščito pozicije podrejenega |
| Popolna zapora | Zapri kopijo podrejenega |

Vsaka kopija **označena z ID-om izvirne pozicije/naročila**. Po ponovni povezavi gostitelj znova sestavi stanje iz usklajevanja: odpre kopije, ki jih ima glavni, a podrejeni nima, zapre „sirote" podrejenega, ki jih glavni več nima — **brez podvajanja trgovanj**.

## Ustvarjanje profila

**Novi profil** odpre namenske **polno-strani** obrazec (`/copy-trading/new`), ne dialog — nabor možnosti je dovolj velik, da se stran bolje bere na telefonu in namizju. Zbira vse vnaprej: ime profila, izvor (glavni) račun, cilje (podrejene) račune (večsmerni izbor s **Izberi vse** gumbom; izbrani glavni izključen iz seznama podredenih), + celoten nabor možnosti za cilje. **Vsak nadzor ima nasveto**, ki pojasni, kaj počne in kako ga uporabiti. Strukturirani vnosi uporabljajo **pravilne validirane nadzore** — številke/odstotek prek numeričnih polj, načini/smer/filter prek izbirnikov, filter simbola prek seznama dodajanja/odstranjevanja čipov simbola, in mapo simbolov prek tabele dodajanja/odstranjevanja `Vir → Cilj (× množitelj)` vrstic — nikoli besedilo z vejicami. Vsi vnosi **validirani pred shranjevanjem** — manjkajoče ime/izvor/cilj, ne-pozitivni parameter določanja velikosti, negativne/nedosledne meje, izven domet procent izgube, ni vrste naročila omogočene, ali prazen filter simbola se pojavijo kot seznam napak + blokira shranjevanje. Pri ustvarjanju je profil ustvarjen + vsak izbrani podrejeni dodan z izbranimi nastavitvami, nato se stran vrne na seznam za kopiranje trgovanja.

**Uvoz / izvoz.** Celoten blok nastavitev se lahko **izveže v datoteko JSON** in se ponovno **uvozi** za predpolnjenje obrazca, tako da se uglašavanje lahko ponovno uporabi v profilih brez ponovno tipkanja. Karto simbolov se lahko podobno **izveže / uvozi kot datoteko CSV** (`Source,Destination,VolumeMultiplier`) — pripravite veliko brokersko karto simbolov v preglednici in jo naložite v enem koraku. Isti nadzori simbolov in uvoz/izvoz CSV so prav tako na voljo v dialogu za cilje na strani Kopiranje trgovanja.

Dejanja vrstic spoštujejo življenjski ciklus: **Zagon** omogočen samo, kadar se ne izvaja, **Ustavitev** + **Pavza** samo pri zagonu, **Izbris** onemogočen med zagonom + pred odstranitvijo profila + ciljev vpraša za potrditev.

Pravkar zagnan profil na kratko prikazuje状况 **Starting** (ni zelene *Running*), medtem ko njegov gostitelj nalagalnik referenčne podatke in izvaja prvo resinhronizacijo — še ne zrcali naročil čez cilje. Preide v **Running** trenutka, ko se ta prva resinhronizacija zaključi in motor kopiranje more. Starting se obravnava kot tekoče za nadzore vrstic (Start onemogočen, Ustavitev in živi-dnevniki omogočeni, Uređevanje/Izbris blokiran), zato nagrevane profil ne more biti ponovno zagnan ali urejen med zaglonom. Ogrevalna faza je sledena na mestu na vozlišču, ki gosti profil; profil, ki se nahaja na drugem replikaciji (ali tisti, ki ne more biti gostjen — njegovi izvor/cilja računi niso povezani prek Odprte API) prikazuje seu navaden status.

## Možnosti na cilje

Nastavite na strani Novi profil, v dialogu za cilje na strani Kopiranje trgovanja, ali prek `POST /api/copy/profiles/{id}/destinations`:

- **Določanje velikosti** (`MoneyManagementMode` + parameter): fiksni lot, lot/nominalni množitelj, sorazmerna ravnovesje/lastniški kapital/prosta marža, fiksni tvegani %, fiksna ročica, samodejno-sorazmerno, **tveganje-%-od-zaustavitve** (M7). Plus najmanjše/največje meje lot + prisili-najmanjše-lot. **Tveganje-od-zaustavitve** velikosti cilj, tako da tvega nastavljen procent *njegovega lastnega* ravnovesja, izpeljan iz **razdalje zaustavitve-ob-izgubi glavnega** (`glavni tvega 2% → podrejeni samodejno-tvega 2%`): `loti = ravnovesje×% ÷ (razdalja zaustavitve × velikost pogodbe)`. Glavni odpri **brez** zaustavitve-ob-izgubi nima razdalje za določanje velikosti → uporablja nastavljeno **največji-tvegani-lot** (M7), če je nastavljen, drugače preskoči (`no_stop_loss`) ne ugiba. Sorazmerno-**lastniški kapital**/**prosta marža** velikost iz realnega računa **lastniški kapital** (`ravnovesje + Σ lebdeče P&L`, izpeljan prek odprte cTrader API, ki ne dostavi lastniškega kapitala), ne običajnega ravnovesja — tako da glavni sedi na odprti dobiček/izgubo velikosti kopije pravično. Uporabljena marža ni izpostavljena prek usklajevalnega API, zato se prosta marža obravnava kot lastniški kapital (pošten proxy razpoložljivih sredstev); drugi načini preberejo ravnovesje + preskočijo dodatni revalvacijski krog v povratku.
- **Filter smeri**: oba / samo-dolgi / samo-kratki. **Obrnejo**: preklopi stranko (+ zamenja SL↔TP) za nasprotni kopijo.
- **Upravljaj-samo** (Prezri-nova-trgovanja / Samo-zaprto): zrcali zapore, delne zapore + zaščitne spremembe na že-kopirane pozicije, vendar odpri **nobene** nove pozicije/čakajoče naročile (preskoči `manage_only`). Uporabite za zmanjšanje cilja brez rezanja obstoječih kopij.
- **Sinhroniziranje-odprtega-ob-zagonu** / **Sinhroniziranje-zaprtega-ob-zagonu** (privzeto vključeno): pri **prvi** resinhronizaciji profila, ali odpri kopije za že obstoječe pozicije glavnega, + ali zapri kopije, ki jih je glavni zaprlo medtem ko je profil ustavljen. Oba veljata samo ob zagonu — med tekočim ponovnim povezovanjem se vedno popolnoma usklajevanje, zato se nesinhroniziranost opravlja ne glede na to.
- **Karta simbolov** + **filter simbola** (bellist / blacklist). Vsak vnos karte simbolov ima izbirni **po-simbolni množitelj volumna** (cMAM po-simbolni preklic) skaliranje velikosti kopije za ta simbol na vrhu določanja velikosti cilja (1 = brez spremembe). Celotna karta se uvozi/izveže kot **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; stolpci `Source,Destination,VolumeMultiplier`) — vsaka vrstica validirana prek objektov domenske vrednosti, zato napakovana datoteka ne more izdelati neveljavne karte.
- **Okno trgovalnih ur** (C18) — na-cilje dnevno UTC okno (`start`/`end` minut-dneva, konec-ekskluzivno; `start == end` = celodan). Nove odprtine zunaj okna preskoči (`trading_hours`); okno z `start > end` se zavije čez polnoč (npr. 22:00–06:00). Obstoječe pozicije ostanejo upravljane.
- **Filter izvorne oznake** (C18, cTrader enakovredno filtru čarobne-številke MT) — ko je nastavljen, kopiraj samo trgovanja glavnega, katerih oznaka se **natančno** ujema (npr. trgovanja enega bota, ali samo-ročna oznaka); drugače preskoči (`source_label`). Prazno = kopiraj vse. Prineseno na `ExecutionEvent.SourceLabel` iz izvorne pozicije/naročila glavnega `TradeData.Label`, poštovano tudi pri resinhronizaciji.
- **Zaščita računa** (ZuluGuard / Globalna zaščita računa) — opazuje **živi lastniški kapital** cilja (`ravnovesje + Σ lebdeče P&L`, pollfano vsakih `CopyDefaults.EquityGuardInterval`) proti `StopEquity` dnu in/ali izbirnemu `TakeEquity` stropcu. Na kršitvi uporabi način: **Samo zaprto** (ustavi nove kopije, nadaljuj upravljaj obstoječe), **Zamrznjenost** (ustavi odpiranje), **Prodaš ven** (zapri **vse** kopije na cilju takoj). Ko se sproži, cilj zatrgljiv — brez novih odpiranj, dokler se gostitelj ne ponovno zažene — + `CopyAccountProtectionTriggered` opozorilo dvignjeno. `SellOut` zahteva `StopEquity`; `TakeEquity` mora sedeti nad `StopEquity`. **Brez-garancij opozorilo:** prodaj-ven uporablja tržno izvedbo — kot pri vsakem konkurentu, ne more jamčiti ceno izpolnitve na hitrem/razpkanem trgu.
- **Ravni-vse gumb za paniko** (C8) — `POST /api/copy/profiles/{id}/flatten` takoj zapri **vse** kopirane pozicije na vsakem cilju + zaklene proti novim odpiranjem. Usmerjen čez proces: API postavi zastavico, nadglednik dostavi tekočem gostitelju (ponovno uporabi kanal za zasuk tokena), ki splošči na mestu; zastavica izbrisana, zato se sproži točno enkrat (`CopyFlattenAll` opozorilo). Uporabnik nato pavzira/ustavi profil.
- **Varnostnik pravila za delodajalca** (C7) — uveljavljanje, ki ga vzpodbuja delodajalce uporabniki kopiranja. Na cilje, **dnevna izgubna kapica** (izguba od dnevnega odprtja lastniškega kapitala) in/ali **zaostajajočega-izgube** omejitev (izguba od tekočega vrhunskega lastniškega kapitala), oba v valuti depozita. Na kršitvi cilja **samodejno splošči** (vse kopije zaprte) + **zaključene** ves preostali dan UTC (nove odprtine preskoči `prop_lockout`); `CopyPropRuleBreached` opozorilo sproženo. Zaključki se jasnijo, ko se dan UTC premakne (sveže osnova/vrh prejeti). Deli isto živo-lastniški kapital anketo kot zaščita računa.
- **Izvedba jitter** (C11, privzeto izključeno) — naključni `0..N` ms zamik pred postavljanjem vsake kopije, da se de-korelira skoraj-enake časovne žige naročil čez svojega **lastnega** račune. **Skladnost opozorilo:** pomoč za delodajalce, ki *dovoljujejo* kopiranje — **ne** orodje za izogib delodajalcu, ki ga prepoveduje; ostati v skladu s pravili vašega delodajalca je vaša odgovornost.
- **Zaključek konfiguracije** (C9) — zamrzni nastavitve cilja za obdobje (`POST …/destinations/{id}/lock` s minutami). Med zaklepanjem ne moremo odstraniti cilja (agregat zavrne z `CopyDestinationConfigLocked`) — načrtna varnostna funkcija pred impulzivnimi spremembami med izgubo. Zaklepanje poteče samodejno ob njegovem časovnem žigu.
- **Predopozorilo konsistentnosti** (C10) — opozori (enkrat na dan UTC), kadar **dnevni dobiček** cilja doseže nastavljen procent dnevnega odprtja lastniškega kapitala (`CopyConsistencyThresholdApproaching`), tako da se pravilo skladnosti delodajalca spoštuje *pred* tem, da se sproži. Dobiček-stran, neodvisno od izgube-strani zaklepanja; se teče iz istega dnevnega osnovnega kot varnostnik pravila delodajalca.
- **Filter vrste naročila** — izberite točno, katere vrste naročil glavnega kopirati: tržno, tržno-območje, omejeno, zaustavljeno, zaustavljeno-omejeno (`CopyOrderTypes` zastavice; privzeto vse). Slog cMAM-izbire.
- **Kopiraj SL / Kopiraj TP** — zrcali zaustavitev-ob-izgubi / izkoristek glavnega, ali upravljaj zaščito neodvisno. Velja za **oba** odprtih pozicij **in** mirujočih čakajočih naročil — omejeno/zaustavljeno/zaustavljeno-omejeno kopija je postavljena in spremenjena z zaustavitvijo-ob-izgubi/izkoriščkom izvirnega naročila (zamenjano pod **Obrnejo**), zato je zaščita priložena trenutka, ko se čakajoča izpolni, ne le po tem.
- **Kopiraj zaostajajočo zaustavljanje**, **zrcali delno zaprto**, **zrcali povečanje volumna** — vsaka neodvisno preklapljiva.
- **Kopiraj čakajoče izteke** (privzeto vključeno) — zrcali dobi-do-datuma izteka čakajočega naročila glavnega.
- **Kopiraj zdrs glavnega** (privzeto vključeno) — za tržno-območje + zaustavljeno-omejeno naročila, postavi naročilo cilja z natančnim zdrsom glavnega-v-točkah (osnovna cena vzeta iz živega mesta cilja).
- **Varnostniki**: največja izguba v %, dnevna izgubna kapica, največji zamik kopije, filter zdrsa (preskoči kopijo, če se cena cilja premakne več kot N pik od vnosa glavnega). **Največji zamik kopije** izmerjen proti časovnemu žigu realnega strežnika dogodka glavnega (`ExecutionEvent.ServerTimestamp`) prek injicirane `TimeProvider`: signal starejši od nastavljenega največjega-zaostanka preskoči, tako da se nikoli ne postavi pozna star kopija.
- **Normalizacija natančnosti SL/TP** (M6) — kopirana zaustavitev-ob-izgubi/izkoristek cene zaokrožena na **cilja** simbol natančnost pred spremembo (na pozicijah **in** čakajočih naročilih postavka/sprememba), tako da se cena glavnega pri bolj fini natančnosti (ali brokerska meja neprevlade) nikoli ne sproži `INVALID_STOPLOSS_TAKEPROFIT`.
- **Varnostnik vezja zavrnitve / Varnostnik sleditelja** (G8) — cilj zavrne `CopyDefaults.RejectionBudget` odpiranj v vrsti je **sproži**: nobenih novih odpiranj za hlajenje okno (`CopyDestinationTripped` opozorilo se sproži), zaustavitvi nevihte zavrnitve od kladenja (delodajalca) račun. Obstoječe pozicije ostanejo upravljane + zaprte med sprožitvijo; varnostnik se samodejno ponastavi po hlajenju + uspešna kopija izbriše števec.
- **Razum lot stropca** (C14) — absolutna največja velikost kopije in/ali večkratnik-od-glavnega kapica. Izračunana kopija presegajoči absolutni kapica, ali presegajoči `N×` glavnega lastno lot velikost, **trda-blokada** (površina kot `lot_sanity` preskoči, računana na `cmind.copy.skipped`) ne postavljena — braniči pred katastrofalno-preuporabo razreda (0.23-lot glavni se obrnejo v 3 lote na vsakem prejemniku prek runaway množitelja ali bug zaokroženja). Oba znanja privzeto `0` (izklopljeno).

## Zanesljivost in robni primeri

Motor zgrajen za realnost, da je vse lahko neuspešno kadarkoli:

- **Čakajočega podrejenega zapolnjene-korelacije timeout** (C13) — zrcaljenoga čakajočega podrejenega, katerega čakajoči glavni izginul (niti počivajoči niti sveže izpolnjeni) preklican po korelacijske timeout, tako da kopija podrejenega ne more zapolniti nekoreliranega v upravljano pozicijo (`CopyPendingTimedOut`). Resinhronizacija tudi čisti ID-označeni izpolnjeni-čakajočega siroto.
- **Medborzna čakajoča-zapolnjene-dirka** — čakajočega podrejenega lastnega lahko zapolni (njegova cena je dosežena) v majhnem oknu, preden se glavni dogodek zapolnjenja/preklica obdela. To pusti pozicijo podrejenega označeno s **naročilom** ID izvirnika, ki bi jo kanonične zapore/SL-TP poti (označene po **poziciji** ID izvirnika) zamudile. Na glavnem **zapolnjenju** se zgodnja zapolnjenja podrejenega upokoji in nadomesti z eno kanonično-označeno tržno kopijo — tako da cilj konča z natančno **enim** kopijo, nikoli podvojeno pozicijo; na glavnem **preklicu** je zaprto povsem (glavni ni nikoli vzel trgovanje). Oba delujeta takoj, ne le pri naslednji resinhronizaciji. Zapolnjenje SL/TP na strani podrejenega, ki zapre kopijo, ki jo glavni še drži, je vodeno po izvoru in ponovno odprto pri naslednji usklajevanju (motor zrcali **glavne** dogodke; ne porabi izvršitve na strani cilja).
- **Robustna zapora/splošči** (M8) — zapiranje sirote pri resinhronizaciji, ali splošči pri varnostni prelomu, tolerira pozicijo, ki jo je broker že zaprlo (`POSITION_NOT_FOUND`): vsaka zapora se teče neodvisno, tako da en star ID nikoli ne ustavi resinhronizacije ali zapusti preostalih računov-neraztopljenih.

- **Začetek z glavnim že v trgovanjih** — pri zagonu gostitelj usklajevanje + odpre kopije za obstoječe pozicije glavnega.
- **Povezave pada / nesinhronizacija** — pri ponovni povezavi gostitelj usklajevanje: odpre manjkajoče kopije, zapre sirote, ponovno označi čakajočega. Brez podvojenih naročil.
- **Napaka pri postavki naročila** — napaka na enem cilju zabeležena, nikoli ne blokira drugih ciljev.
- **Edini veljaven žeton na cID** — cTrader razveljavi stari dostopni žeton cID trenutka novega izdanega. cMind zamenja žeton tekočega gostitelja **na mestu** (ponovno-avtentifikacija na živem gnjezdu) tako da kopiranje nadaljuje brez padanja toka. Glejte [življenjski ciklus žetona](token-lifecycle.md).

## Revizija

Vsako dejanje sprožita strukturirane, izvor-generirane dogodke dnevnika (`LogMessages`) s ID-om profila, ciljem cID, ID-jem naročila/pozicije, + vrednostmi — naročilo postavljeno/preskoči (z razlogom), delna zapora, zaščita uporabljena, zaostajanje uporabljeno, čakajoča postavljena/spremenjena/preklicana, izteka zrcaljeno, tržno-območje zdrs zrcaljeno, žeton zaměnjeno, resinhronizacijski povzetek. To je razvidna pot za skladnost + razreševanje sporov.

Ob straneh dnevnika, motor sprožita **OpenTelemetry metrike** na metriki `cMind.Copy` (registrirano v skupni cevovod OTel, izvoženo čez OTLP / v Azure Monitor kot ostalo): `cmind.copy.latency` (glavni-dogodek → despach, ms), `cmind.copy.dispatch.duration` (razširitev na vse cilje, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (označeno s ciljem), `cmind.copy.skipped` (označeno z razlogom), + `cmind.copy.failed`. Te naredijo regresijo latentnosti/zdrsa merljiva, ne samo vidno v liniji dnevnika — živa svet trdimo jih kot proračunski.

## API

- `GET /api/copy/profiles` — seznam.
- `POST /api/copy/profiles` — ustvari (z izbirnim ID-ji cilja računa).
- `GET /api/copy/profiles/{id}` — popolna podrobnost vključno z vsemi možnostmi cilja.
- `POST /api/copy/profiles/{id}/destinations` — dodaj cilj s celotnim naborom možnosti.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — odstrani.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — životinjski ciklus.

## Preizkusi

- **Enota** (`tests/UnitTests/CopyTrading`) — načini velikosti, filtri odločitve, filter vrste naročila, kopija izteka, tržno-območje/zaustavljeno-omejeno zdrs, SL/TP preklapljivi, delna zapora, čakajočega spremeni/prekliči, začetek-z-odprtim, zapora→nesinhronizacija→resinhronizacija, na-mestu zamena žetona, čez-cID neveljavnost. Teče proti `FakeTradingSession`, cTrader-zvesti simulatorja v spominu.
- **Integracija** (`tests/IntegrationTests/CopyLive`) — afiniteta-vozlišča/zahtevek-zakupnine, žeton-različica-propagacija na pravem Postgres-u.
- **E2E** (`tests/E2ETests`) — opcije-cilja-povratni-krog prek API + vmesniku, polni življenjski ciklus.
- **Stress / DST** (`tests/StressTests`) — determinističko-simulacijo testiranja: seeded naključne obremenitve + vbrizgavanje napake (vtičnica valjanje, zavrnitev naročila, tržno-območje zavrnitev, zasuk žetona, smrt vozlišča) pogon `CopyEngineHost` k mirovanju + trdim konvergence invariante. Glejte [testiranje/stress-testiranje.md](../testing/stress-testing.md). Ta svet površina + popravljena realna napaka pri zagonu: `OnReconnected` žičani pred začetkom branja reference + resinhronizacijo, zato je sokoki valjanje med zagonom lahko tekočega drugo resinhronizacijo sočasno + pokvarjena gostitelja ne-sočasna stanja slovarje — zagon branja + prvo resinhronizacija sedaj teče pod `_stateGate`.
- **Živa** — pravi cTrader demo računi; glejte [testiranje/live-copy-trading.md](../testing/live-copy-trading.md).

Glejte [dev-credentials.md](../testing/dev-credentials.md) za samski datoteko pooblaščencev živa + E2E stopnje prebire.
## Nadzori profila in upravljanje ciljev

Zagon/ustavitev so gumbi ikone na vsaki vrstici profila (onemogočeni, kadar dejanje ne velja). Glavni in cilja računi so prikazani z njihovo **številko računa**, nikoli notranjim ID-om. Klik na profil odpre **dialog** upravljanja svojih cilja računov (dodaj/odstrani s celotnim naborom možnosti na cilje).
