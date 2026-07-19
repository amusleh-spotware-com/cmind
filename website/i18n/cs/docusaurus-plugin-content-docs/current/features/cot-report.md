# Commitment of Traders (COT)

cMind dodává zabudovanou zprávu **Commitment of Traders** — týdenní přehled CFTC o tom, kdo je dlouhý a krátký na americkém trhu futures (komerční hedgeři, velcí spekulanti, fondy), s interaktivními historickými grafy, normalizovaným **COT indexem**, ověřeným REST API pro cBots a MCP nástroji pro klienty umělé inteligence. Data pocházejí přímo z **veřejných souborů CFTC Socrata** — bez klíče API, bez agregátoru. Stejně jako ekonomický kalendář jde o oddělený modul, který lze deaktivovat bez vlivu na jádro obchodování.

## Co vám to dá

- **Všechny tři rodiny zpráv, samotné futures a futures + opce dohromady:**
  - **Dědictví (Legacy)** — Nekomerční (velcí spekulanti), Obchodní (hedgeři), Nevykázaní.
  - **Rozčleněné (Disaggregated)** — Producent/Obchodník, Dealeři swapů, Spravované peníze, Další hlášené.
  - **Obchodníci ve finančních futures (TFF)** — Dealer, Asset Manager, Páky Fondy, Další hlášené.
- **Vybraný katalog trhů** — Hlavní páry FX, zlato/stříbro/měď, ropa a zemní plyn, pokladnice, indexy akcií, kryptoměny a hlavní obiloviny/měkké komodity — každá mapovaná na svůj stabilní kód smlouvy CFTC a kde jednoznačně na obchodovatelný symbol (např. Euro FX → `EURUSD`, Zlato → `XAUUSD`).
- **Index COT (0–100)** — kde se aktuální čisté postavení spekulanta nachází v jeho historickém rozsahu (výchozí ~3 roky). Hodnoty blízko extrémů signalizují přeplnění pozicování, které často předchází obrácení; zpráva označuje **dlouhý extrém** (≥80) nebo **krátký extrém** (≤20).
- **Správnost bodu v čase.** Týdenní zpráva je měřena v úterý, ale veřejně se stává až v pátek; každé čtení respektuje tento okamžik vydání, takže signál pozicování backtestovaný nikdy nevidí zprávu dříve, než byla publikována (bez předvídání).

## Používání stránky

Otevřete **Commitment of Traders** z levé navigace. Vyberte **trh**, **typ zprávy** (Dědictví / Rozčleněné / Finanční) a přepínač **Futures + opce** pro přepínání mezi samotným futures a kombinovanou variantou. Stránka zobrazuje:

- **Čisté pozicování v čase** — interaktivní liniový graf čistého postavení (dlouho − krátko) každé kategorie obchodníka v okně historie.
- **Index COT** — liniový graf indexu 0–100 s nejnovějším čtením a jeho extrémním štítkem.
- **Nejnovější snímek** — tabulka dlouho / krátko / čisté / % otevřeného zájmu na kategorii obchodníka, plus celkový otevřený zájem a datum zprávy.

Každý graf má tlačítka panelu nástrojů **přiblížení / oddálení** (a obnovení), a můžete táhnout přes časovou osu pro přiblížení. **Stažení CSV** stahuje úplnou týdenní historii vybraného trhu a typu zprávy jako soubor připravený do tabulky. Použijte **Porovnání trhů** k překrytí několika trhů na jednom grafu — srovnávací grafy znázorňují čistou pozici spekulanta a index COT každého vybraného trhu vedle sebe, takže můžete přečíst mezitržní pozicování na první pohled.

## Jak data tečou

Databáze je mezipaměť. Týdenní pracovník přijímání si vezme šest datových souborů CFTC pro sledované trhy, aktualizuje katalog trhu a připojí každou novou zprávu **idempotentně** (znovuspuštění nikdy nezpůsobí duplikování snímku). Navíc se data **načítají na vyžádání**: poprvé, když je trh požadován, je načten ze zdroje CFTC a uložen, a každý následný požadavek je obsluhován přímo z databáze. Mezipaměť **se osvěžuje, když jsou vydány nové týdenní zprávy** — jakmile je nejnovější uložená zpráva starší než jeden týden, další požadavek transparentně vytáhne a připojí nejnovější data (omezené tak, aby zdroj nikdy není zahlcen). První načtení vyplní několik let historie; výpadek zdroje se degraduje na obsluhu nejlepších mezipaměti dat. Vše běží z balení bez klíče; volitelný token aplikace Socrata pouze zvyšuje limit sazby.

## Konfigurace

Všechny klíče jsou pod `App:Cot` (viz [přepínače funkcí](./feature-toggles.md) a
[nastavení vlastníka bílého štítku](./white-label-owner-settings.md)):

| Klíč | Výchozí | Účel |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Zda se týdenní pracovník přijímání spouští. |
| `PollInterval` | `6h` | Jak často pracovník dotazuje soubory CFTC. |
| `BackfillYears` | `5` | Léta historie vytažená při prvním spuštění. |
| `ReconcileLookbackWeeks` | `4` | Poslední týdny znovu synchronizované každý cyklus, aby se zachytily revize. |
| `SocrataAppToken` | — | Volitelný token, který zvyšuje limit anonymní sazby. |
| `CotIndexLookbackWeeks` | `156` | Týdenní zprávy používané jako rozsah indexu COT (~3 roky). |

## Uzavření

Viditelnost je dvoustupňová brána, totožná s ekonomickým kalendářem: brána bílého štítku na úrovni tvrdé `App:Branding:EnableCot` (úroveň stavby) **a** přepínač funkce v běhu `App:Features:Cot`. Je-li buď vypnutá, odkaz na navigaci, stránka, REST API a MCP nástroje všechny zmizí (API vrací `404`). Protože zdroj dat je bez klíče, neexistuje brána klíče zdroje dat — povoleno znamená viditelné.

## Pro vývojáře

- Doména: `Core.Cot` — agregáty `CotMarket` a `CotReport`, objekt hodnoty `CotPositions`, doménová služba `CotIndexCalculator` a porty `ICotReports` / `ICotSource`.
- Infrastruktura: `Infrastructure.Cot` — parser `CftcSocrataSource` proti korumpci, brána sazby, služba zápisu pouze připojit, strana čtení a týdenní pracovník přijímání (schéma EF `cot`).
- Přístup cBot a AI: [COT cBot API](./cot-cbot-api.md) (REST, JWT `market:read`) a MCP nástroje
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
