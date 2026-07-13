# Siła waluty makro AI i perspektywa forward

cMind wysyła **asystowany przez AI, matematycznie-deterministyczny** silnik siły waluty makro. Ranguje
konfigurowalny wszechświat walut — 8 głównych plus wschodzące rynki i egzotyczne waluty — według
**bieżącej** fundamentalnej siły i projektuje **perspektywę forward kierunkową** dla każdej pary w
wybranym horyzoncie (1M / 3M / 6M / 12M). Każdy ranking, każda stronniczość pary i każda liczba jest
obliczana czystą matematyką deterministyczną w domenie; LLM tylko *zbiera* forward-looking inputy które
dane nie mogą opublikować i *wyjaśnia* wynik w zwykłym angielskim. Nigdy nie wymyśla rankingu, kierunku
ani liczby.

> **Uczciwe ograniczenie.** Fundamenty prognozują wartość średnio-do-długoterminową dobrze i wartość
> krótkoterminową słabo. Traktuj to jako filtr pozycjonowania / confluence, **nie** sygnał krótkoterminowy.
> Odczyty blisko wpływowych wydań (NFP/CPI/central-bank) są zaszumione. Nie jest poradą finansową.

## Jak to działa

1. **Bieżące fundamenty pochodzą z Kalendarza Ekonomicznego, nie z LLM.** Twarde liczby — stopy polityki,
   CPI vs cel, PKB, zatrudnienie, bilans handlowy — i ich **z-score'i niespodzianki** są źródłem
   **point-in-time** z modułu [kalendarza ekonomicznego](./economic-calendar.md) (FRED/BLS/BEA/ECB i
   harmonogramy banków centralnych). Historyczna migawka nigdy nie przecieka przyszłość.
2. **LLM zbiera tylko to co kalendarz nie może opublikować** — per waluta: **forward** trajektoria
   (oczekiwana ścieżka stopy polityki w bp, trend inflacji-vs-cel, momentum wzrostu) i **geopolityczna**
   perspektywa (risk-on/off, taryfy, fiskalne/dług, wybory), plus każda bieżąca figura EM/egzotyczna
   którą kalendarz brakuje. Surowy JSON, walidacja aware-do-tier, web search on.
3. **Domena oblicza ranking i macierz forward deterministycznie.** Każdy driver jest zdobywany jako
   **within-tier z-score** (tak 50%-inflacja egzotyczna nigdy nie zniekształca główne), winsorized,
   weight-summed do composite, i rankowany strongest→weakest z stabilnym tie-break ISO. Forward warstwa
   niesie każdy composite wzdłuż jego trajektorii —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — i mapuje każdą różnicę pary
   do **kierunkowego biasu** (▲ aprecjacja / ▬ neutralny / ▼ deprecjacja) z conviction.
4. **LLM wyjaśnia** ranking i top pair calls w zwykłym języku.

## Draiery

| Driver | Efekt na siłę | Notatki |
|---|---|---|
| Stopa polityki & trajektoria | Wyższa / jastrzębia ⇒ silniejsza | Najwyższa waga; rozbieżność banków centralnych napędza największe luki. |
| Inflacja (CPI vs cel) | Powyżej celu ⇒ słabsza | Zdobywane odwrotnie (drag na siłę nabywczą). |
| Wzrost PKB | Wyższy względny wzrost ⇒ silniejsza | Różnicowy vs panel. |
| Zatrudnienie | Silniejszy rynek pracy ⇒ silniejsza | Zasiła ścieżkę polityki. |
| Bilans handlowy / rachunek bieżący | Nadwyżka ⇒ silniejsza | Strukturalny popyt. |
| Stanowisko polityki | Jastrzębie ⇒ silniejsza | Główny długoterminowy driver. |
| Momentum niespodzianki | Niedawne przebicia ⇒ silniejsza | Z z-score'ów niespodzianki kalendarza. |
| Geopolityczna / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) silniejsze | Ograniczone forward risk delta. |
| Rzeczywisty yield / carry *(EM/exotic)* | Pozytywna rzeczywista stopa ⇒ silniejsza | Dominujący EM driver w spokojnych reżimach. |
| Zagrożenie zewnętrzne *(EM/exotic)* | Deficyty / niske rezerwy / dług USD ⇒ słabsza | Strukturalna presja deprecjacji. |
| Warunki handlu *(eksporterzy surowców)* | Rosnące ceny eksportu ⇒ silniejsza | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Risk polityczny / instytucjonalny *(EM/exotic)* | Niestabilność ⇒ słabsza | Szerszy dead-band, capped conviction. |

## Wszechświat tiered (główne + EM + egzotyczne)

Wszechświat jest **konfigurowalny przez deployment** (`App:CurrencyStrength:Universe`) — dodawanie
waluty to config, nie kod. Każda waluta niesie **tier** (`Major` / `EmergingMarket` / `Exotic`) który
tunes wagi, szerokość dead-band i conviction cap:

- **Główne** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Rynki wschodzące** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  zagrożenie zewnętrzne weighted up, średnia confidence.
- **Egzotyczne** — TRY, HUF, CZK, plus USD-pegged HKD/SAR; niska confidence, szerszy dead-band, capped
  conviction. **Pegged / heavily-managed** waluty (HKD, SAR, CNH) są flagged, ich trajektoria jest
  down-weighted, i ich pair outlook jest clamped ku `Neutral` tak peg jest nigdy czytany jako free-floating
  sygnał.

Ponieważ oficjalne statystyki EM/exotic są lower-frequency, revised i czasem opaque, AI-zebrane
figury niosą **per-tier confidence** pokazane jako reliability badge.

## Graceful degradation

| Kalendarz | AI | Wynik |
|---|---|---|
| ✅ | ✅ | Pełny ranking + forward projekcja + narracja (`CalendarAndAi`). |
| ✅ | ❌ | Ranking tylko z kalendarza, brak forward projekcji (`CalendarOnly`). |
| ❌ | ✅ | AI-zebrane bieżące figury + forward, niższa confidence (`AiOnly`). |
| ❌ | ❌ | Brak migawki — widget ukrywa się i strona pokazuje pusty stan. |

Aplikacja uruchamia się niezmieniona w obu wypadkach. AI jest gated na klucz AI; kalendarz leg respektuje
swoje white-label gate + runtime toggle.

## Używanie go

- **Włącz AI** (Settings → AI) i **włącz widget** z twojego własnego dashboard **Customize** dialogu
  ("Currency strength" — opt-in, ukryty domyślnie). Widget pokazuje top silne/słabe waluty i top 3M
  pair call; linkuje do pełnej strony.
- **Pełna strona** — `/ai/currency-strength`: selector horyzontu (1M/3M/6M/12M), filter tier
  (All/Majors/EM/Exotics), bieżący ranking, forward forecast, macierz pair-outlook (bias +
  conviction, pegged/low-confidence flagged), i AI narracja. Naciśnij **Refresh now** (owner) aby
  regenerować. Background worker (`App:CurrencyStrength:RefreshEnabled`, **domyślnie `true`**) odświeża
  na harmonogramie tak strona jest populowana out of the box; deployment lub owner wyłącza to (lub
  wyłącza funkcję AI / economic-calendar, którą refresher honoruje poprzez degradację do brak migawki).

## Dostęp programowy

Jeden shared read model (`ICurrencyStrengthQuery`) jest osiągalny trzema sposobami:

- **In-app AI** — injected bezpośrednio (in-process) do funkcji AI.
- **MCP** — narzędzie `currency_strength` (parametry `horizon`, `tier`) dla klientów/agentów AI.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, 
  zabezpieczony przez **ten sam** mechanizm `CalendarJwt` jak [calendar cBot API](./calendar-cbot-api.md) 
  z dodanym **`market:read`** scope. cBot rejestruje klienta API z `market:read`, wymienia jego
  id + secret na krótkotrwały JWT na `POST /api/calendar/v1/token`, i wywołuje endpoints z
  `Bearer` token. Brak drugiego scheme JWT, brak drugiego sekretu — wyciek tokenu jest read-only,
  market-scoped, krótkotrwały i revocable.

Zobacz [calendar cBot API](./calendar-cbot-api.md) dla token flow i copy-paste sample.
