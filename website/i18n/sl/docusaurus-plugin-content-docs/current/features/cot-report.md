# Commitment of Traders (COT)

cMind ima vgrajeno **Commitment of Traders** poročilo — tedenski CFTC pregled, kdo je dolg in kratek na
ameriškem trgu termina (komercialnih varovalcev, velikih špekulantov, skladov), z interaktivnimi
zgodovinskimi grafikoni, normalizirano **COT indeksom**, avtenticirano REST API za cBote in MCP orodjem
za AI odjemalce. Podatki prihajajo neposredno iz **javnih CFTC Socrata datasetov** — brez API ključa,
brez agregatorja. Tako kot ekonomski koledar je to ločljiv modul, ki se lahko onemogoči brez vpliva na
jedro trgovanja.

## Kaj vam daje

- **Vse tri druž ine poročil, samo termini in termini+opcije skupaj:**
  - **Dediščina** — Nekomercialni (veliki špekulanti), Komercialni (varovalci), Nespročljivi.
  - **Razčlenjeno** — Proizvajalec/Trgovec, Zamenjalni trgovci, Upravljane denarnice, Drugi
    poročljivi.
  - **Trgovci v finančnih terminih (TFF)** — Posrednik, Upravljalec sredstev, Izravnave z vzvodom,
    Drugi poročljivi.
- **Kurirani katalog trga** — FX par, zlato/srebro/baker, surova nafta in naravni plin, zakladne
  vrednostne papirje, indekse delnic, kriptovalute in glavne žita/mehke komodite — vsaka kartirana v
  svoj stabilen CFTC čip kode in, kjer je nedvoumno, v trgovljiv simbol (npr. Euro FX → `EURUSD`,
  Zlato → `XAUUSD`).
- **COT indeks (0–100)** — kjer se trenutni položaj špekulanta nahaja znotraj njegovega zgodovinskega
  razpona (privzeto ~3-letni pregled). Odčitki blizu ekstremov označujejo preplnjeno pozicioniranje,
  ki pogosto preidhaja razveljavitvi; poročilo označuje **dolgo ekstrem** (≥80) ali **kratek ekstrem**
  (≤20).
- **Točnost na točko v času.** Tedensko poročilo se meri v torek, vendar je javno dostopno šele
  naslednji petek; vsak prebir spoštuje ta trenutek izdaje, zato signalirana pozicija, preverjena v
  preteklem času, nikoli ne vidi poročila, preden je bilo objavljeno (brez pogleda naprej).

## Uporaba strani

Odprite **Commitment of Traders** iz levega menuja. Izberite **trg**, **tip poročila** (Dediščina /
Razčlenjeno / Finančno) in vklopite **Termini + opcije** za preklapljanje med samo terminimi in
kombiniranimi variantami. Stran prikazuje:

- **Neto pozicioniranje skozi čas** — interaktivni črtni grafikon neto položaja vsake kategorije
  trgovcev (dolgo − kratko) čez okno zgodovine.
- **COT indeks** — črtni grafikon indeksa 0–100 s trenutnim odčitkom in njegovo označbo ekstremuma.
- **Sveža slika** — tabela dolgo / kratko / neto / % odprtega interesa na kategorijo trgovca, skupaj
  z skupnim odprtim interesom in datumom poročila.

Vsak grafikon ima gumbe za orodja **povečave / pomanjšave** (in reset), čez časovno os lahko povlečete za povečavo. **Izvoz CSV** presneme polno tedensko zgodovino izbranega trga in tipa poročila kot datoteko, pripravljeno za preglednico. Uporabite **Primerjaj trge** za prekrivanje več trgov na eno risbo — grafikoni primerjave narišejo čisto pozicioniranje spekulanta in COT indeks v vsako izbrano trgov drug ob drugega, tako da lahko berete presečno tržno pozicioniranje na prvo pogled.

## Kako podatki tečejo

Podatkovne zbirke je predpomnilnik. Tedenski radnik vbilanja potegne šest CFTC datasetov za sledene trge, vstavlja katalog trga in dodaja vsako novo poročilo **idempotentno** (ponovni zagon nikoli ne podvoji sliko). Poleg tega se podatki **nalagajo na zahtevo**: prvi krat, ko se zahteva trg, se pridobi iz vira CFTC in shrani, vsaka naslednja zahteva pa se služi direktno iz podatkovne zbirke. Predpomnilnik se **osvežuje, ko izidejo nova tedenskega poročila** — ko je najnovejše shranjeno poročilo starejše kot en teden, naslednja zahteva transparentno potegne in doda najnovejše podatke (omejene, da se vir nikoli ne prezgosti). Prvi nanos vnaprej napolni več let zgodovine; izpad vira se poslabša na služ s popolnoma shranjenimi podatki. Vse se izvaja iz škatlice brez ključa; izbirni Socrata ključ aplikacije le dviguje omejitev hitrosti.

## Konfiguracija

Vsi ključi so pod `App:Cot` (glejte [stikala za značilnosti](./feature-toggles.md) in
[nastavitve lastnika bela etiketa](./white-label-owner-settings.md)):

| Ključ | Privzeto | Namen |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Ali se tedenski vbiralnik izvaja. |
| `PollInterval` | `6h` | Kako pogosto vbiralnik spodnavlja CFTC datasetov. |
| `BackfillYears` | `5` | Leta zgodovine, prispevana pri prvem zagonu. |
| `ReconcileLookbackWeeks` | `4` | Nedavni tedni ponovno usklajeni v vsakem ciklu za ujemanje popravkov. |
| `SocrataAppToken` | — | Izbirni ključ, ki zvišuje anonimno omejitev hitrosti. |
| `CotIndexLookbackWeeks` | `156` | Tedenski izveštaji, ki se uporabljajo kot obseg COT-indeksa (~3 leta). |

## Vrata

Vidnost je dvostopenjska vrata, enaki ekonomskemu koledarju: zgradna vrata oznake `App:Branding:EnableCot`
(raven gradnje) **in** dinamični toggle `App:Features:Cot`. S katerim koli zaprtem se povezava na nav,
stran, REST API in MCP orodja razčistijo (API vrne `404`). Ker je vir podatkov brez ključa, ni vrata za
ključ vira podatkov — omogočeno pomeni vidno.

## Za razvijalce

- Domena: `Core.Cot` — agregatov `CotMarket` in `CotReport`, predmeta vrednosti `CotPositions`,
  storitve domene `CotIndexCalculator` in vrat `ICotReports` / `ICotSource`.
- Infrastruktura: `Infrastructure.Cot` — razčistilnik korupcije `CftcSocrataSource`, vrata stopnje,
  servis pisanja samo append, stran branja in tedenski delavec vbilanja (EF shema `cot`).
- cBot & AI dostop: [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) in MCP orodja
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
