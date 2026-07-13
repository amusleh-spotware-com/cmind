# AI-alapú devizaerősség és forward kilátások

A cMind egy **AI-asszisztált, matematikailag determinisztikus** makro devizaerősségi motort szállít. Rangsorolja a konfigurálható deviza-univerzumot — a 8 fő devizát plusz feltörekvő piaci és egzotikus devizákat — a **jelenlegi** fundamentális erősség alapján, és vetíti a **forward irányú kilátást** minden párra a választott horizonton (1M / 3M / 6M / 12M). Minden rang, minden pár bias és minden szám tisztán determinisztikus matematikával számolódik a domain magban; az LLM csak *összegyűjti* azokat a forward-looking inputokat, amelyeket az adat nem publikál, és *elmagyarázza* az eredményt közérthető angolul. Soha nem talál ki rangot, irányokat vagy számokat.

> **Őszinte korlátozás.** A fundamentumok jól jelzik a közepes és hosszú távú értéket, de rosszul a rövid távút. Kezeld ezt pozicionálási / konfluencia szűrőként, **nem** rövid távú timing jelzésként. A magas hatású adatok (NFP/CPI/központi bank) közelében végzett mérések zajosak. Nem pénzügyi tanácsadás.

## Hogyan működik

1. **A jelenlegi fundamentumok az Gazdasági Naptárból jönnek, nem az LLM-ből.** A kemény számok — politikai kamatok, CPI vs cél, GDP, foglalkoztatás, kereskedelmi mérleg — és ezek **meglepetés z-score-jai** pont-időben vannak forrásolva a [gazdasági naptár](./economic-calendar.md) modulból (FRED/BLS/BEA/ECB és központi banki menetrendek). Egy korábbi pillanatfelvétel soha nem szivárog look-ahead-et.
2. **Az LLM csak azt gyűjti, amit a naptár nem publikálhat** — devizánként: a **forward** trajektóriát (várt politikai kamatpálya bp-ban, infláció-trend-vs-cél, növekedési momentum) és egy **geopolitikai** kilátást (risk-on/off, vámok, fiskális/adósság, választások), plusz bármely EM/egzotikus jelenlegi adat, amivel a naptár nem rendelkezik. Szigorú JSON, tier-aware validáció, web keresés bekapcsolva.
3. **A domain determinisztikusan számolja a rangsort és a forward mátrixot.** Minden driver **within-tier z-score**-ként van pontozva (így egy 50%-os inflációjú egzotikus nem torzítja a fő devizákat), winsorizálva, súlyozott összegként kompozittá alakítva, és rangsorolva legerősebb → leggyengébb irányba stabil ISO tie-break-kel. A forward réteg minden kompozitot a trajektóriája mentén visz: `projected = current + horizonScale · Σ trajectoryDriver·weight` — és minden pár projected differenciálját egy **irányú bias-sá** (▲ felértékelődik / ▬ semleges / ▼ leértékelődik) képezi conviction-nel.
4. **Az LLM elmagyarázza** a rangsort és a top pár hívásokat közérthető nyelven.

## A driverek

| Driver | Hatás az erősségre | Megjegyzések |
|---|---|---|
| Politikai kamat & trajektória | Magasabb / hawkish ⇒ erősebb | Legmagasabb súly; a központi banki divergencia hajtja a legnagyobb különbségeket. |
| Infláció (CPI vs cél) | Cél felett ⇒ gyengébb | Fordítva pontozva (vásárlóerő drag). |
| GDP növekedés | Magasabb relatív növekedés ⇒ erősebb | Differenciál a panel ellen. |
| Foglalkoztatás | Erősebb munkaerő ⇒ erősebb | A politikai pályát táplálja. |
| Kereskedelmi mérleg / folyó fizetési mérleg | Többlet ⇒ erősebb | Strukturális kereslet. |
| Politikai álláspont | Hawkish ⇒ erősebb | Az elsődleges hosszú távú driver. |
| Meglepetés momentum | Legutóbbi beat-ek ⇒ erősebb | A naptár surprise z-score-jaiból. |
| Geopolitikai / kockázat | Risk-off ⇒ biztonságos menedékek (USD/JPY/CHF) erősebb | Korlátozott forward kockázati delta. |
| Reál hozam / carry *(EM/egzotikus)* | Pozitív reál kamat ⇒ erősebb | Domináns EM driver nyugodt rezsimekben. |
| Külső sebezhetőség *(EM/egzotikus)* | Hiányok / alacsony tartalékok / USD adósság ⇒ gyengébb | Strukturális leértékelődési nyomás. |
| Terms of trade *(áru exportőrök)* | Emelkedő export árak ⇒ erősebb | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Politikai / intézményi kockázat *(EM/egzotikus)* | Instabilitás ⇒ gyengébb | Szélesebb dead-band, korlátozott conviction. |

## Tierelt univerzum (fő + EM + egzotikus)

Az univerzum **telepítés-konfigurálható** (`App:CurrencyStrength:Universe`) — deviza hozzáadása konfiguráció, nem kód. Minden deviza egy **tier-t** hordoz (`Major` / `EmergingMarket` / `Exotic`), amely hangolja a súlyozást, a dead-band szélességet és a conviction capet:

- **Fő devizák** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (kamat-szint vezérelte).
- **Feltörekvő piacok** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Skandináv NOK/SEK); carry + kockázat + külső sebezhetőség súlyozva fel, közepes magabiztosság.
- **Egzotikusok** — TRY, HUF, CZK, plusz USD-pegged HKD/SAR; alacsony magabiztosság, szélesebb dead-band, korlátozott conviction. **Pegged / erősen kezelt** devizák (HKD, SAR, CNH) meg vannak jelölve, a trajektóriájuk le van súlyozva, és a pár kilátásuk `Neutral` felé van clampolva, így egy peg soha nem olvasódik szabadon úszó jelzésként.

Mivel az hivatalos EM/egzotikus statisztikák alacsonyabb frekvenciájúak, revideáltak és néha átlátszatlanok, az AI-által összegyűjtött számok egy **per-tier magabiztosságot** mutatnak megbízhatósági badge-ként.

## Graceful degradáció

| Naptár | AI | Eredmény |
|---|---|---|
| ✅ | ✅ | Teljes rangsort + forward projekció + narratíva (`CalendarAndAi`). |
| ✅ | ❌ | Csak naptár-alapú jelenlegi rangsort, nincs forward projekció (`CalendarOnly`). |
| ❌ | ✅ | AI-által összegyűjtött jelenlegi számok + forward, alacsonyabb magabiztosság (`AiOnly`). |
| ❌ | ❌ | Nincs pillanatfelvétel — a widget el van rejtve és az oldal üres állapotot mutat. |

Az alkalmazás változatlanul fut mindkét esetben. Az AI az AI kulcsra van kapcsolva; a naptár lába tiszteletben tartja a saját white-label gate-jét + runtime toggle-jét.

## Használata

- **Kapcsold be az AI-t** (Beállítások → AI) és **kapcsold be a widgetet** a saját dashboard **Testreszabás** dialogjából ("Devizaerősség" — opcionális, alapértelmezés szerint rejtett). A widget a legerősebb/leggyengébb devizákat és a top 3M pár hívást mutatja; a teljes oldalra linkel.
- **Teljes oldal** — `/ai/currency-strength`: horizont választó (1M/3M/6M/12M), tier szűrő (Mind/Major/EM/Egzotikus), a jelenlegi rangsort, a forward előrejelzést, a pár-kilátás mátrixot (bias + conviction, pegged/alacsony-magabiztosság megjelölve), és az AI narratívát. Nyomd meg a **Frissítés most**-ot (tulajdonos) az újrageneráláshoz. Egy háttér worker (`App:CurrencyStrength:RefreshEnabled`, **alapértelmezés `true`**) ütemezetten frissít, így az oldal a dobozból ki van népesítve; egy telepítés vagy a tulajdonos kikapcsolja (vagy letiltja az AI / gazdasági naptár funkciót, amit a refresher tiszteletben tart a degradációval a nincs-pillanatfelvétel állapotra).

## Programozott hozzáférés

Egy megosztott olvasási modell (`ICurrencyStrengthQuery`) háromféleképpen érhető el:

- **In-app AI** — közvetlenül injektálva (folyamaton belül) az AI funkciókba.
- **MCP** — a `currency_strength` eszköz (paraméterek `horizon`, `tier`) AI kliensek/ügynökök számára.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, biztosítva a **megosztott** `CalendarJwt` machinery által, mint a [naptár cBot API](./calendar-cbot-api.md)-nál, egy hozzáadott **`market:read`** scope-val. Egy cBot regisztrál egy API klienst `market:read`-kel, beváltja az id + secret-jét egy rövid életű JWT-re `POST /api/calendar/v1/token`-nél, és hívja a végpontokat egy `Bearer` tokennel. Nincs második JWT séma, nincs második titok — egy kiszivárgott token csak olvasható, market-scoped, rövid életű és visszavonható.

Lásd a [naptár cBot API](./calendar-cbot-api.md)-t a token flow és egy másolható minta érdekében.
