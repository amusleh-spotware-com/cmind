---
description: "cMind spedisce un motore macro currency-strength AI-assisted e math-deterministic. Classifica un universo configurabile di valute per forza fondamentale attuale e proietta una forward directional outlook per ogni coppia su un orizzonte scelto."
---

# AI macro currency strength & forward outlook

cMind spedisce un motore macro currency-strength **AI-assisted, math-deterministic**. Classifica un
universo configurabile di valute — le 8 majors più valute emergenti ed esotiche — per
**current** forza fondamentale, e proietta una **forward directional outlook** per ogni coppia su un
orizzonte scelto (1M / 3M / 6M / 12M). Ogni rank, ogni bias di coppia e ogni numero è computato
da pura matematica deterministica nel domain core; il LLM solo *raccoglie* gli input forward-looking che i
dati non possono pubblicare e *spiega* il risultato in inglese semplice. Non inventa mai un rank, una
direzione o un numero.

> **Limite onesto.** I fondamentali predicono bene il valore medio-lungo termine e male quello a breve termine.
> Trattalo come un filtro di posizionamento / confluenza, **non** come un segnale di timing a breve termine.
> Le letture vicino a rilasci high-impact (NFP/CPI/banca centrale) sono noisy. Non è consulenza finanziaria.

## Come funziona

1. **I fondamentali attuali vengono dal Calendario Economico, non dal LLM.** I numeri hard — tassi di policy,
   CPI vs target, PIL, occupazione, bilancia commerciale — e i loro **surprise z-scores** sono sourced
   **point-in-time** dal modulo [calendario economico](./economic-calendar.md) (FRED/BLS/BEA/ECB e
   central-bank schedules). Uno snapshot storico non leak mai look-ahead.
2. **Il LLM raccoglie solo ciò che il calendario non può pubblicare** — per valuta: la traiettoria
   **forward** (policy-rate path atteso in bp, trend-inflazione-vs-target, momentum di crescita) e una
   **geopolitical outlook** (risk-on/off, tariffs, fiscal/debt, elections), più qualsiasi figura EM/exotic
   attuale che il calendario manca. JSON strict, validation tier-aware, web search on.
3. **Il domain computa il ranking e la matrice forward deterministicamente.** Ogni driver è scored come
   uno **within-tier z-score** (così un exotic 50%-inflation non distorce mai i majors), winsorized,
   weight-summed in un composite, e ranked strongest→weakest con un stable ISO tie-break. Il layer forward
   porta ogni composite lungo la sua traiettoria —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — e mappa il differenziale proiettato
   di ogni coppia a un **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) con una conviction.
4. **Il LLM spiega** il ranking e i top pair calls in linguaggio semplice.

## I driver

| Driver | Effetto su forza | Note |
|---|---|---|
| Tasso di policy & traiettoria | Più alto / hawkish ⇒ più forte | Peso più alto; divergenza banche centrali guida i gap più grandi. |
| Inflazione (CPI vs target) | Sopra target ⇒ più debole | Scored inversamente (purchasing-power drag). |
| Crescita PIL | Crescita relativa più alta ⇒ più forte | Differenziale vs il panel. |
| Occupazione | Lavoro più forte ⇒ più forte | Alimenta il policy path. |
| Bilancia commerciale / conto corrente | Surplus ⇒ più forte | Domanda strutturale. |
| Policy stance | Hawkish ⇒ più forte | Il driver primario a lungo termine. |
| Surprise momentum | Beat recenti ⇒ più forte | Dagli surprise z-scores del calendario. |
| Geopolitico / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) più forti | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Tasso reale positivo ⇒ più forte | Driver EM dominante in regimi calm. |
| Vulnerabilità esterna *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ più debole | Pressione strutturale di deprezzamento. |
| Termini di scambio *(esportatori commodity)* | Prezzi export in rialzo ⇒ più forte | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Rischio politico / istituzionale *(EM/exotic)* | Instabilità ⇒ più debole | Dead-band più ampia, conviction capped. |

## Tiered universe (majors + EM + exotics)

L'universo è **configurabile al deployment** (`App:CurrencyStrength:Universe`) — aggiungere una valuta è config,
non codice. Ogni valuta porta un **tier** (`Major` / `EmergingMarket` / `Exotic`) che tuna
pesatura, larghezza dead-band e conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, confidence media.
- **Exotics** — TRY, HUF, CZK, più USD-pegged HKD/SAR; confidence bassa, dead-band più ampia, conviction capped.
  **Pegged / heavily-managed** currencies (HKD, SAR, CNH) sono flagged, la loro traiettoria è down-weighted,
  e la loro pair outlook è clampata verso `Neutral` così un peg non è mai letto come un segnale
  free-floating.

Perché le statistiche EM/exotic ufficiali sono lower-frequency, riviste e a volte opaque, le figure
AI-gathered portano una **per-tier confidence** mostrata come reliability badge.

## Graceful degradation

| Calendario | AI | Risultato |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Solo ranking attuale da calendario, no forward projection (`CalendarOnly`). |
| ❌ | ✅ | Figure attuali AI-gathered + forward, confidence più bassa (`AiOnly`). |
| ❌ | ❌ | Nessuno snapshot — il widget nasconde e la pagina mostra uno stato vuoto. |

L'app funziona invariata in ogni caso. AI è gated sulla AI key; la gamba del calendario rispetta il suo
stesso white-label gate + runtime toggle.

## Usarlo

- **Abilita AI** (Settings → AI) e **accendi il widget** dal tuo dialogo **Customize** del dashboard
  ("Currency strength" — opt-in, hidden per default). Il widget mostra le top valute strong/weak e la top
  pair call a 3M; link alla pagina completa.
- **Pagina completa** — `/ai/currency-strength`: un selettore orizzonte (1M/3M/6M/12M), un filtro tier
  (All/Majors/EM/Exotics), il ranking attuale, la previsione forward, la matrice pair-outlook (bias +
  conviction, pegged/low-confidence flagged), e la narrative AI. Premi **Refresh now** (owner) per
  rigenerare. Un background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refresha su
  uno schedule così la pagina è popolata out of the box; un deployment o l'owner lo spegne (o disabilita la
  AI / economic-calendar feature, che il refresher onora degradando a no snapshot).

## Accesso programmatico

Un shared read model (`ICurrencyStrengthQuery`) è raggiungibile in tre modi:

- **In-app AI** — iniettato direttamente (in-process) nelle AI features.
- **MCP** — il tool `currency_strength` (params `horizon`, `tier`) per AI clients/agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured
  dalla **stessa** machinery `CalendarJwt` come la [calendar cBot API](./calendar-cbot-api.md) con un
  aggiunto scope **`market:read`**. Un cBot registra un API client con `market:read`, scambia il suo
  id + secret per un JWT short-lived a `POST /api/calendar/v1/token`, e chiama gli endpoint con un
  token `Bearer`. Nessuno secondo scheme JWT, nessuna seconda secret — un token leakato è read-only,
  market-scoped, short-lived e revocable.

Vedere la [calendar cBot API](./calendar-cbot-api.md) per il flusso token e un sample copy-paste.
