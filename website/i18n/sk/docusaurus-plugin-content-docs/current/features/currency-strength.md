# AI macro currency strength & forward outlook

cMind dodáva **AI-assisted, math-deterministic** macro currency-strength engine. Rangúje
konfigurovateľný universe mien — 8 majorov plus emerging-market a exotic currencies — podľa
**aktuálnej** fundamentálnej sily a projektuje **forward directional outlook** pre každý pár cez
zvolený horizont (1M / 3M / 6M / 12M). Každý rank, každý pár bias a každé číslo je
vypočítané čistou deterministickou matematikou v doménovom jadre; LLM iba *zbiera*
forward-looking inputs, ktoré dáta nemôžu publikovať a *vysvetľuje* výsledok v plain English. Nikdy
nevymyslí rank, smer alebo číslo.

> **Čestné obmedzenie.** Fundamenty predpovedajú stredne-dlhodobú hodnotu dobre a krátkodobú
> zle. Zaobchádzajte s týmto ako s pozicionálnym / confluence filtrom, **nie** ako short-term timing signál.
> Čítania blízko high-impact releases (NFP/CPI/centrálna banka) sú noisy. Nie finančné poradenstvo.

## Ako to funguje

1. **Aktuálne fundamenty prichádzajú z Economic Calendar, nie z LLM.** Tvrdé čísla — policy
   sadzby, CPI vs cieľ, HDP, zamestnanosť, obchodná bilancia — a ich **surprise z-scores** sú získavané
   **point-in-time** z [economic calendar](./economic-calendar.md) modulu (FRED/BLS/BEA/ECB a
   centrálne bankové kalendáre). Historická snímka nikdy neunlhne look-ahead.
2. **LLM zbiera iba to, čo kalendár nemôže publikovať** — per mena: **forward** trajektóriu
   (očakávaná policy-rate path v bp, inflation-trend-vs-target, growth momentum) a **geopolitický**
   outlook (risk-on/off, clá, fiškálne/dlh, voľby), plus akékoľvek EM/exotic current dáta, ktoré
   kalendár nemá. Strict JSON, tier-aware validácia, web search zapnutý.
3. **Doména deterministicky počíta ranking a forward maticu.** Každý driver je skórovaný ako
   **within-tier z-score** (takže 50%-inflation exotic nikdy nedeformuje majorov), winsorizovaný,
   weight-súčtový do kompozitu a ranked strongest→weakest so stabilným ISO tie-breakom. Forward layer
   nesie každý kompozit po jeho trajektórii —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — a mapuje každý pár
   projektovaného diferenciálu na **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) s conviction.
4. **LLM vysvetľuje** ranking a top pár volania v plain language.

## Drivere

| Driver | Efekt na silu | Poznámky |
|---|---|---|
| Policy rate & trajektória | Vyššia / hawkish ⇒ silnejšia | Najvyššia váha; divergencia centrálnych bánk hýbe najväčšími medzerami. |
| Inflácia (CPI vs cieľ) | Nad cieľ ⇒ slabšia | Skórované inverzne (purchasing-power drag). |
| HDP rast | Vyšší relatívny rast ⇒ silnejšia | Diferenciál voči panelu. |
| Zamestnanosť | Silnejšie labour ⇒ silnejšia | Živí policy path. |
| Obchodná bilancia / bežný účet | Prebytok ⇒ silnejšia | Štrukturálny dopyt. |
| Policy stance | Hawkish ⇒ silnejšia | Primárny dlhodobý driver. |
| Surprise momentum | Nedávne beats ⇒ silnejšia | Zo surprise z-scores kalendára. |
| Geopolitické / riziko | Risk-off ⇒ safe havens (USD/JPY/CHF) silnejšie | Ohraničený forward risk delta. |
| Real yield / carry *(EM/exotic)* | Pozitívny reálny úrok ⇒ silnejšia | Dominantný EM driver v pokojných režimoch. |
| Externá zraniteľnosť *(EM/exotic)* | Deficity / nízke rezervy / USD dlh ⇒ slabšia | Štrukturálny depreciačný tlak. |
| Terms of trade *(komoditní exportéri)* | Rastúce exportné ceny ⇒ silnejšia | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Politické / inštitucionálne riziko *(EM/exotic)* | Nestabilita ⇒ slabšia | Širší dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

Universe je **deployment-konfigurovateľný** (`App:CurrencyStrength:Universe`) — pridanie meny je
config, nie kód. Každá mena nesie **tier** (`Major` / `EmergingMarket` / `Exotic`), ktorý ladí
váženie, šírku dead-bandu a conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, stredná dôvera.
- **Exotics** — TRY, HUF, CZK, plus USD-pegged HKD/SAR; nízka dôvera, širší dead-band, capped
  conviction. **Pegged / heavily-managed** meny (HKD, SAR, CNH) sú označené, ich trajektória je
  down-weighted a ich pár outlook je clampovaný smerom k `Neutral`, takže peg sa nikdy nečíta ako
  free-floating signál.

Pretože oficiálne EM/exotic štatistiky sú nižšia frekvencia, revidované a niekedy nepriehľadné,
AI-zbierané čísla nesú **per-tier confidence** zobrazenú ako reliability badge.

## Graceful degradation

| Kalendár | AI | Výsledok |
|---|---|---|
| ✅ | ✅ | Plný ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, bez forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-zbierané aktuálne čísla + forward, nižšia dôvera (`AiOnly`). |
| ❌ | ❌ | Žiadna snímka — widget sa skryje a stránka ukáže empty state. |

Aplikácia beží nezmenene v oboch prípadoch. AI je gated na AI key; calendar leg rešpektuje svoj
white-label gate + runtime toggle.

## Použitie

- **Povoliť AI** (Settings → AI) a **zapnúť widget** z vášho vlastného dashboard **Customize** dialogu
  ("Currency strength" — opt-in, skryté predvolene). Widget ukazuje top silné/slabé meny a
  top 3M pár call; odkazuje na plnú stránku.
- **Plná stránka** — `/ai/currency-strength`: selector horizontu (1M/3M/6M/12M), filter tieru
  (All/Majors/EM/Exotics), aktuálny ranking, forward forecast, matrix outlook páru (bias +
  conviction, pegged/low-confidence flagged) a AI narrative. Stlačte **Refresh now** (owner) pre
  regeneráciu. Background worker (`App:CurrencyStrength:RefreshEnabled`, **predvolene `true`**) refreshuje
  podľa schedule tak, aby stránka bola populate out-of-the-box; deployment alebo owner ho vypne (alebo zakáže
  AI / economic-calendar funkciu, ktorú refresher rešpektuje degradáciou na žiadnu snímku).

## Programmatický prístup

Jeden zdieľaný read model (`ICurrencyStrengthQuery`) je dosiahnuteľný tromi spôsobmi:

- **In-app AI** — injectovaný priamo (in-process) do AI funkcií.
- **MCP** — `currency_strength` nástroj (params `horizon`, `tier`) pre AI klientov/agentov.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`,
  zabezpečené rovnakým `CalendarJwt` strojom ako [calendar cBot API](./calendar-cbot-api.md)
  s pridaným **`market:read`** scope. cBot zaregistruje API klienta s `market:read`, vymení jeho
  id + secret za krátkožijúci JWT na `POST /api/calendar/v1/token` a volá endpoints s
  `Bearer` tokenom. Žiadna druhá JWT schéma, žiadne druhé secret — uniknutý token je read-only,
  market-scoped, krátkožijúci a odvolateľný.

Pozrite [calendar cBot API](./calendar-cbot-api.md) pre token flow a copy-paste sample.
