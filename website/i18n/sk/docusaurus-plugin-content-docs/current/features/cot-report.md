# Commitment of Traders (COT)

cMind dodáva vstavané správu **Commitment of Traders** — týždenný rozpis CFTC kto je v dlhej a krátkej pozícii
na amerických trhoch s futures (komerční hedgeri, veľkí špekuanti, fondy), s interaktívnymi historickými grafmi,
normalizovaným **indexom COT**, autentifikovaným REST API pre cBots a nástrojmi MCP pre klientov AI. Údaje pochádzajú
priamo z **verejných množín údajov CFTC Socrata** — žiadny kľúč API, žiadny agregátor. Ako ekonomický kalendár ide
o samostatný modul, ktorý sa dá vypnúť bez vplyvu na obchodné jadro.

## Čo to poskytuje

- **Všetky tri rodiny správ, iba futures a futures + opcie spolu:**
  - **Legacy** — Non-Commercial (veľkí špekuanti), Commercial (hedgeri), Non-Reportable.
  - **Disaggregated** — Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.
  - **Traders in Financial Futures (TFF)** — Dealer, Asset Manager, Leveraged Funds, Other Reportables.
- **Kurátorom vybraný katalóg trhov** — forex páry, zlato/striebro/meď, ropa & zemný plyn, dlhopisy,
  indexy akcií, kryptomeny a hlavné obilia/mäkké komodity — každý zmapovaný na svoj stabilný kód
  kontraktu CFTC a kde je to jednoznačné, na obchodovateľný symbol (napr. Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **Index COT (0–100)** — kde sa aktuálna čistá pozícia špekuanta nachádza v rámci svojho historického
  rozsahu (predvolený ~3-ročný lookback). Čítania blízko extrémov signalizujú zaplnené pozície, ktoré často
  predchádzajú zvratu; správa označí **dlhú extrémnú** (≥80) alebo **krátku extrémnú** (≤20).
- **Presnosť v bode v čase.** Týždenná správa sa meria v utorok, ale stáva sa verejnou až v piatok;
  každé čítanie rešpektuje ten moment zverejnenia, takže signál pozicií v backtest nikdy nevidí správu
  skôr, ako bola zverejnená (bez look-ahead).

## Používanie stránky

Otvorte **Commitment of Traders** z ľavej navigácie. Vyberte **trh**, **typ správy** (Legacy /
Disaggregated / Financial) a prepnite **Futures + opcie**, aby ste prepínali medzi samými futures
a kombinovanou variantou. Stránka zobrazuje:

- **Čisté pozície v čase** — interaktívny čiarový graf čistej pozície (dlhá − krátka) každej kategórie
  obchodníka v okne histórie.
- **Index COT** — čiarový graf indexu 0–100 s posledným čítaním a jeho menej extrémnym označením.
- **Najnovší snímok** — tabuľka dlhá / krátka / čistá / % otvorený záujem na kategóriu obchodníka, plus
  celkový otvorený záujem a dátum správy.

Každý graf má tlačidlá panela nástrojov na **zväčšenie / zmenšenie** (a resetovanie) a môžete prenášať cez os času, aby ste zväčšili. **Exportovať CSV** stiahne úplnú týždennú históriu vybraného trhu a typu správy ako súbor pripravený na tabuľku. Použite **Porovnať trhy** na prekrytie niekoľkých trhov na jednom grafe — porovnávacie grafy vykresľujú čistú pozíciu špekulantov vybraného trhu a index COT vedľa seba, aby ste mohli čítať pozície na viacerých trhoch na prvý pohľad.

## Ako tok údajov

Databáza je vyrovnávacia pamäť. Týždenný pracovník príjmu ťahá šesť množín údajov CFTC pre sledované trhy, aktualizuje katalóg trhov a pripája každú novú správu **idempotentne** (opätovné spustenie nikdy neduplikuje snímok). Okrem toho sú údaje **načítané na požiadanie**: prvýkrát, keď sa požaduje trh, sa načítava zo zdroja CFTC a uloží, a každé následné požiadavky sa obsluhujú priamo z databázy. Vyrovnávacia pamäť sa **obnovuje s vydaním nových týždenných správ** — akonáhle je najnovšia uložená správa staršia ako týždeň, nasledujúce požiadavky transparentne vytiahnu a pripoja najnovšie údaje (regulované tak, aby zdroj nikdy nebol preplavený). Prvé načítanie wstecz vypĺňa niekoľko rokov histórie; výpadok zdroja zhoršuje podávanie najlepších údajov z vyrovnávacej pamäte. Všetko funguje z krabice bez kľúča; voliteľný token aplikácie Socrata iba zvýši ľahostajnosť sadzby.

## Konfigurácia

Všetky kľúče sa nachádzajú v `App:Cot` (pozri [prepínače funkcií](./feature-toggles.md) a
[nastavenia vlastníka white-label](./white-label-owner-settings.md)):

| Kľúč | Predvolené | Účel |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Či pracovník príjmu týždňov beží. |
| `PollInterval` | `6h` | Ako často pracovník hlasuje množiny údajov CFTC. |
| `BackfillYears` | `5` | Rokov histórie ťahaných pri prvom spustení. |
| `ReconcileLookbackWeeks` | `4` | Posledné týždne znovu synchronizované každý cyklus, aby sa zachytili revízie. |
| `SocrataAppToken` | — | Voliteľný token, ktorý zvyšuje anonymný limit sadzby. |
| `CotIndexLookbackWeeks` | `156` | Týždenné správy použité ako rozsah indexu COT (~3 roky). |

## Gating

Viditeľnosť je dvoustupňová brána, identická s ekonomickým kalendárom: tuhá brána white-label
`App:Branding:EnableCot` (úroveň zostavenia) **a** prepínač funkcií runtime `App:Features:Cot`. Ak je
jeden vypnutý, odkaz na navigáciu, stránka, REST API a nástroje MCP všetko zmizne (API vráti `404`).
Pretože zdroj údajov nemá kľúč, neexistuje brána kľúča zdroja údajov — povolené znamená viditeľné.

## Pre vývojárov

- Doména: `Core.Cot` — agregáty `CotMarket` a `CotReport`, objekt hodnoty `CotPositions`, doménová
  služba `CotIndexCalculator` a porty `ICotReports` / `ICotSource`.
- Infraštruktúra: `Infrastructure.Cot` — parsér proti korrupcii `CftcSocrataSource`, brána sadzby, služba
  zápisu iba pri hromadení, strana čítania a týždenný pracovník príjmu (schéma EF `cot`).
- Prístup cBot & AI: [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) a nástroje MCP
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
