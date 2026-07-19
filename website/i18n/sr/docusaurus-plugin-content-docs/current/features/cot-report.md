# Commitment of Traders (COT)

cMind isporučuje ugrađeni **Commitment of Traders** izveštaj — nedeljni CFTC pregled ko je dug i kratak na
tržištu američkih fučersa (komercijalni hedžeri, veliki špekulanti, fondovi), sa interaktivnim
istorijskim grafikonima, normalizovanim **COT indeksom**, autentifikovanim REST API-jem za cBots i MCP
alatima za AI klijente. Podaci dolaze direktno iz **CFTC javnog Socrata datasetima** — bez API ključa,
bez agregatora. Kao i ekonomski kalendar, to je odvojen modul koji se može onemogućiti bez uticaja na
trgovinski tor.

## Šta vam daje

- **Sve tri породине izveštaja, samo fučersi i fučersi+opcije kombinovani:**
  - **Nasleđe** — Nekomercijalci (veliki špekulanti), Komercijalni (hedžeri), Neizvještavajući.
  - **Rasčlanjeno** — Proizvođač/Trgovac, Zamjenski trgovci, Upravljana novac, Ostali izvještavajući.
  - **Trgovci u finansijskim fučersima (TFF)** — Diljer, Upravitelj sredstava, Polužne sredstva, Ostali
    izvještavajući.
- **Kurirani katalog tržišta** — FX parovi, zlato/srebro/bakar, sirova ulja i prirodni gas, trezori,
  indeksi kapitala, kriptovalute i glavna žita/meka roba — svaka mapirana na njenu stabilnu CFTC
  šifru ugovora i, gde je nedvosmisleno, na trgovljivu simbol (npr. Euro FX → `EURUSD`, Zlato →
  `XAUUSD`).
- **COT indeks (0–100)** — gde se trenutna neto pozicija špekulanta nalazi u svom istorijskom rasponu
  (zadana vrednost ~3-godišnji pregled). Očitavanja blizu krajnjih vrednosti signaliziraju gužvu
  pozicioniranje koje često prethodi preokretu; izveštaj označava **dugačak ekstrem** (≥80) ili
  **kratak ekstrem** (≤20).
- **Tačnost u tački vremena.** Nedeljni izveštaj se meri u utorak, ali postaje javno dostupan tek
  sledećeg petka; svaki pročitaj čini čast tom trenutku izdavanja, tako da backtestovana signala
  pozicioniranja nikad ne vide izveštaj pre nego što je objavljen (bez gleda unapred).

## Korišćenje stranice

Otvorite **Commitment of Traders** iz levog navigacijskog okvira. Odaberite **tržište**, **vrsta
izveštaja** (Nasleđe / Rasčlanjeno / Finansijsko) i prebacite **Fučersi + opcije** da se prebacujete
između samo fučersa i kombinovane varijante. Stranica prikazuje:

- **Neto pozicioniranje tokom vremena** — interaktivni linijski grafikon neto pozicije svake kategorije
  trgovaca (dugačka − kratka) preko prozora istorije.
- **COT indeks** — linijski grafikon indeksa 0–100, sa najnovijom očitavanjem i oznakom njegovog
  ekstremuma.
- **Najnovija slika** — tabela duga / kratka / neto / % otvorene kamate po kategoriji trgovca, plus
  ukupna otvorena kamatna stopa i datum izveštaja.

Svaki grafikon ima dugmadi alatne trake za **uvećanje/umanjenje** (i reset), i možete vuči preko vremenske ose za zumiranje. **Izvoz CSV** preuzima punu nedeljnu istoriju odabranog tržišta i vrstu izveštaja kao datoteku spremnu za tabele. Koristite **Poredi tržišta** da preklapate nekoliko tržišta na jednom grafikonu — grafikoni poređenja crtaju čist položaj spekulanta i COT indeks svakog odabranog tržišta jedan pored drugog, tako da možete čitati pozicioniranje između tržišta na prvi pogled.

## Kako podaci tečaju

Baza podataka je keš. Nedeljni radnik unoša izvlači šest CFTC skupova podataka za praćena tržišta, upsertuje katalog tržišta i prilagođava svaki novi izveštaj **idempotentno** (ponovno pokretanje nikad ne umnožava snimak). Pored toga, podaci se **učitavaju na zahtev**: prvi put kada se traži tržište, izvlači se iz CFTC izvora i pohranjuje, a svaki kasnije zahtev se služi direktno iz baze podataka. Keš se **osvežava kako se objavljuju nova nedeljni izveštaji** — kada je najnoviji pohranjen izveštaj stariji od nedelje, sledeći zahtev transparentno izvlači i prilagođava najnovije podatke (smanjeno tako da izvor nikada nije zamoren). Prvi učitak unazad popunjava nekoliko godina istorije; izvor kvarija degradira na služ sa najboljim keširanih podataka. Sve se pokreće iz kutije bez ključa; opcioni Socrata aplikacijski ključ samo podiže ograničenje stope.

## Konfiguracija

Svi ključevi žive ispod `App:Cot` (vidite [prekidače funkcija](./feature-toggles.md) i
[postavke vlasnika bele etikete](./white-label-owner-settings.md)):

| Ključ | Podrazumevano | Svrha |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Da li nedeljni radnik unoša radi. |
| `PollInterval` | `6h` | Koliko često radnik ankete CFTC dataset. |
| `BackfillYears` | `5` | Godina istorije povučene pri prvom pokretanju. |
| `ReconcileLookbackWeeks` | `4` | Nedavne nedelje ponovno usklađene u svakom ciklu kako bi se uhvatile izmene. |
| `SocrataAppToken` | — | Opcioni ključ koji podiže anonimno ograničenje stope. |
| `CotIndexLookbackWeeks` | `156` | Nedeljni izveštaji korišćeni kao COT-indeks raspon (~3 godine). |

## Zaključavanje

Vidljivost je dvostepeno zaključavanje, identično ekonomskom kalendaru: build-nivo hard zaključavanja
`App:Branding:EnableCot` **i** runtime toggle-a `App:Features:Cot`. Sa bilo kojim isključenim, nav
link, stranica, REST API i MCP alati sve nestanu (API vraća `404`). Pošto je izvor podataka bez
ključa, nema zaključavanja ključa izvora podataka — omogućeno znači vidljivo.

## Za programere

- Domen: `Core.Cot` — agregatima `CotMarket` i `CotReport`, objektom vrednosti `CotPositions`,
  servisom domena `CotIndexCalculator` i portima `ICotReports` / `ICotSource`.
- Infrastruktura: `Infrastructure.Cot` — anti-korupcijskim parserom `CftcSocrataSource`, stenom rate,
  upisanim servisom samo za prilog, čitanom stranom i nedeljnim radnikom unoša (EF šema `cot`).
- cBot & AI pristup: [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) i MCP alata
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
