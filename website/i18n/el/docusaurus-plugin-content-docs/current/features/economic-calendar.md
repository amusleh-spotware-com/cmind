---
description: "Το οικονομικό ημερολόγιο cMind — πρόγραμμα releases, actuals, forecasts, αναθεωρήσεις και ένα data-driven impact model από πρωτεύουσες αρχές, με point-in-time correctness και ≥10 χρόνια ιστορίας."
---

# Economic calendar

Το cMind αποστέλλει το **δικό του** οικονομικό ημερολόγιο — πρόγραμμα releases, actuals, forecasts,
αναθεωρήσεις και ένα data-driven impact model — από **πρωτεύουσες αρχές** (κεντρικές τράπεζες
και εθνικές στατιστικές υπηρεσίες), με **μηδενική εξάρτηση** από ForexFactory, FXStreet,
Investing.com ή οποιονδήποτε aggregator. Είναι point-in-time correct, διατηρεί ≥10 χρόνια
ιστορίας, και είναι συνδεδεμένο σε συναλλαγές, το δημόσιο API, το MCP, τα cBots, το AI, τα
alerts και τα backtests. Είναι μια αποσυνδεδεμένη μονάδα: μπορεί να απενεργοποιηθεί με μηδενική
επίδραση στον trading core.

> **Κατάσταση.** Ο domain core (impact model, country→symbol mapping, news-window policy,
> point-in-time revision chains, two-tier gating) **και** η persistence (το `calendar` Postgres
> schema, το append-only read/write side, ο FRED connector και ο config-gated ingestion worker)
> είναι υλοποιημένα και tested (unit + Testcontainers integration). Το JWT REST API, τα MCP
> tools και το UI προσγειώνονται στις επόμενες φάσεις rollout που περιγράφονται παρακάτω.

## Τι το κάνει διαφορετικό

Οι επαναλαμβανόμενες καταγγελίες έναντι των κορυφαίων ημερολογίων έγιναν οι σχεδιαστικοί μας
περιορισμοί:

- **Χωρίς σιωπηρές αλλαγές impact-rating.** Το impact rating μας είναι **ντετερμινιστικό,
  με εκδόσεις και ελεγμένο**. Κάθε αλλαγή είναι μια καταγεγραμμένη αναθεώρηση με timestamp —
  ποτέ σιωπηλή αντικατάσταση. Ένας χρήστης μπορεί να δει ακριβώς *γιατί* ένα event είναι High.
- **Ένα UTC anchor ανά event.** Κάθε event αγκυρώνεται σε μια μοναδική UTC στιγμή από το επίσημο
  πρόγραμμα της πρωτεύουσας πηγής· η δική της timezone αποθηκεύεται, και η per-user απόδοση
  χρησιμοποιεί explicit IANA timezone με DST χειρισμένο από τη βάση δεδομένων ζωνών — ποτέ
  χειροκίνητο ±1h toggle.
- **Πλήρεις αλυσίδες αναθεωρήσεων, παντού.** Η αρχική τιμή και κάθε αναθεώρηση είναι first-class,
  εκτεθειμένα ταυτόσημα μέσω του API, του MCP και των cBot surfaces.
- **≥10 χρόνια ιστορίας, χωρίς τοίχο.** Απεριόριστο εύρος περιήγησης· κανένα 60-day cap, κανένα
  registration gate.
- **Point-in-time by construction.** Κάθε γεγονός φέρει `KnownAt` (πότε *εμείς* το μάθαμε) και
  `EffectiveAt` (η στιγμή του event). "Το ημερολόγιο όπως έμοιαζε τη στιγμή T" είναι ένα
  first-class query, οπότε ένα backtested news rule συμπεριφέρεται ακριβώς όπως το live —
  χωρίς look-ahead από τη χρήση αναθεωρημένων τιμών στο ιστορικό.

## Το impact model

Το impact score είναι μια καθαρή, ντετερμινιστική συνάρτηση στο `[0, 100]`, ομαδοποιημένη σε
Low / Medium / High / Critical. Οι εισροές της είναι μόνο δεδομένα γνωστά τη στιγμή της
βαθμολόγησης (χωρίς future leak):

- **Series prior** — ένα baseline βάρος ανά κατηγορία δείκτη (μια απόφαση επιτοκίου υπερτερεί
  του CPI, που υπερτερεί μιας μικρής έρευνας).
- **Realized-volatility footprint** — η διάμεσος απόλυτη απόδοση των πρωτευόντων επηρεασμένων
  συμβολαίων στο παράθυρο μετά τα *προηγούμενα* releases της σειράς: "αυτό το release ιστορικά
  κινεί την τιμή τόσο πολύ."
- **Surprise sensitivity** — πόσο ισχυρά το απόλυτο surprise (ένα z-score) ιστορικά
  συσχετίστηκε με την post-release κίνηση.

Το score συνδυάζει αυτά με σταθερά βάρη και σφραγίζει ένα `ImpactModelVersion`. Η επανυπολογισμός
είναι μια explicit, logged λειτουργία που παράγει μια **νέα αναθεώρηση** — ποτέ μετάλλαξη —
οπότε το score είναι πάντα αναπαραγώγιμο από τις εισροές του.

## Country → currency → symbol mapping

Η πιο συχνά αναφερόμενη algo integration ευαισθησία λύνεται μία φορά, ως καθαρή συνάρτηση: μια
χώρα αντιστοιχίζεται στο νόμισμά της (κάθε μέλος ευρωζώνης fan in στο EUR), και ένα νόμισμα
αντιστοιχίζεται στα watchlist σύμβολα που το αναφέρουν σε οποιοδήποτε σκέλος. Έτσι **το EURUSD
επηρεάζεται και από EU και από US events**· το XAUUSD εκτίθεται στο USD· το US500 αντιστοιχίζεται
στο USD. Αυτό οδηγεί το news filter, την επίλυση affected-symbols και το blackout math.

## News-window policy

Ένα `NewsWindowRule` είναι `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`.
Μια μοναδική, shared, pure υλοποίηση απαντά "είναι η στιγμή T μέσα σε ένα blackout για το
σύμβολο S;" — χρησιμοποιείται από το cBot news filter, το copy-trade pause και το AI risk
guard, οπότε δεν μπορούν ποτέ να αποκλίνουν. Σε αβεβαιότητα η blackout απάντηση προεπιλέγει
την διαμορφωμένη συντηρητική τιμή (fail-closed by default) ώστε ένα κενό δεδομένων να μη
σιωπηλά πρασινίζει τις συναλλαγές μέσω ενός high-impact release.

## Point-in-time & αναθεωρήσεις

Τα actuals, τα forecasts και τα impact scores είναι **append-only**. Κάθε event έχει μια
διατεταγμένη αλυσίδα αναθεωρήσεων, μονότονη στο `KnownAt`:

- `Scheduled` — το event πρωτοπρογραμματίστηκε (prior impact, χωρίς actual).
- `Released` — έφτασε η πρώτη τυπωμένη actual τιμή.
- `Revised` — έφτασε μια αργότερη αναθεωρημένη τιμή.
- `Rescheduled` — η πηγή μετακίνησε τη στιγμή του release (ελεγμένο, alertable).
- `Rescored` — το impact score επανυπολογίστηκε υπό νέα model version.

Το query `as of` μια παρελθούσα στιγμή επιστρέφει ακριβώς την αναθεώρηση που ήταν γνωστή τότε
— η εγγύηση που εξαλείφει το look-ahead σε backtested news rules.

## Forecast / consensus

Η δημοσκόπηση διάμεσου οικονομολόγων **δεν** δημοσιεύεται ελεύθερα από τις πρωτεύουσες πηγές —
είναι η proprietary value-add των aggregator, και εμείς δεν την κατασκευάζουμε. Το event schema
φέρει ένα nullable `Forecast`· μια deployment μπορεί να συνδέσει μια licensed consensus feed
μέσω της προαιρετικής πόρτας `IForecastProvider` (bring-your-own-key, off by default). Οι
προηγούμενες τιμές και οι αναθεωρήσεις προέρχονται πάντα από την επίσημη πηγή.

## Πηγές δεδομένων

Δύο αποσυνδεδεμένες στρώσεις, όλες πρωτεύουσες — ποτέ aggregator:

- **Πρόγραμμα / χρονισμός:** FRED release calendar· εθνικές στατιστικές υπηρεσίες (BLS, BEA,
  Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan)· ημερολόγια συνεδριάσεων
  κεντρικών τραπεζών (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Πραγματικές τιμές:** FRED (με vintage dates για αναθεωρήσεις και point-in-time), συν BLS,
  BEA, Census, ECB SDW, Eurostat και OECD SDMX APIs.

Μια νεκρή πηγή υποβαθμίζει την κάλυψη **μόνο για αυτή την πηγή**· το ημερολόγιο συνεχίζει να
σερβίρει τα υπόλοιπα και εμφανίζει το κενό ως freshness metric.

## Rate limiting & το εφεδρικό σχέδιο

Οι εξωτερικές πηγές δημοσιεύουν rate limits (το FRED επιτρέπει ~120 requests/minute). Το
ημερολόγιο είναι χτισμένο ώστε **ποτέ να μην υπερβαίνει το limit μιας πηγής**, και ώστε το
να περιορίζεται ή να κόβεται να μην υποβαθμίζει τα reads:

- **Proactive throttling.** Κάθε HTTP client της πηγής περνά μέσω ενός shared, thread-safe rate
  gate που απλώνει τα εξερχόμενα requests σε ένα διαμορφωμένο budget
  (`App:Calendar:FredRequestsPerMinute`, default 100 — σκόπιμα κάτω από το όριο της πηγής). Τα
  requests enqueue και pace, ποτέ burst.
- **Honor `429 Retry-After`.** Αν μια πηγή επιστρέψει ποτέ `429 Too Many Requests`, το gate
  backing off την πηγή κατά το server-requested cooldown (ή `App:Calendar:RateLimitBackoff`,
  default 60s) πριν την επόμενη κλήση — κανένα tight retry loop.
- **Standard resilience.** Κάθε source client επίσης κληρονομεί το app-wide resilience handler
  (retry με backoff + jitter, circuit breaker, timeouts), οπότε παροδικά blips απορροφώνται
  και μια επίμονα αποτυγχάνουσα πηγή парκάρεται (η κάλυψή της γίνεται stale) χωρίς να
  επηρεάζει τις άλλες.
- **Το εφεδρικό σχέδιο — το durable read-through cache.** Τα reads **ποτέ** δεν σερβίρονται
  καλώντας μια πηγή. Μόλις ένα range fetchαρει, επιμένει append-only στο Postgres και
  σερβίρεται από εκεί για πάντα (βλ. §"On-demand load"). Οπότε ακόμα και όταν μια πηγή είναι
  rate-limited ή down, το ημερολόγιο συνεχίζει να απαντά από cached, point-in-time-correct
  δεδομένα· το missing span απλά μένει uncovered και επαναλαμβάνεται στον επόμενο ingestion
  cycle. Οι blackout απαντήσεις επιπλέον αποτυγχάνουν στη συντηρητική default υπό αβεβαιότητα,
  οπότε ένα κενό δεδομένων ποτέ δεν πρασινίζει τις συναλλαγές μέσω ενός release.
- **Φτηνό polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors)
  και το "fetch ένα span μία φορά, ποτέ ξανά" cache κρατούν τον πραγματικό όγκο requests
  πολύ κάτω από οποιοδήποτε limit σε κανονική λειτουργία — το rate gate είναι ένα safety net,
  όχι η κοινή διαδρομή.

## Ενεργοποίηση / απενεργοποίηση

Δύο ανεξάρτητες στρώσεις, ακριβώς όπως τα άλλα cMind features:

- **Στρώση 1 — runtime feature toggle** (`Feature.EconomicCalendar`) γυρνά από το Features admin
  UI· χωρίς redeploy, εφαρμόζεται live.
- **Στρώση 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`).
  Ένας reseller το θέτει `false` για να αφαιρέσει εντελώς το feature· ένας operator τότε δεν
  μπορεί να το επανενεργοποιήσει.

Η effective state είναι `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`.
Όταν απενεργοποιείται, η nav entry κρύβεται και το `/economic-calendar`, `/api/calendar/**` και
τα MCP calendar tools επιστρέφουν ένα clean feature-disabled `404` — ποτέ `500`. Η επιμένουσα
ιστορία διατηρείται σε runtime toggle-off ώστε η επανενεργοποίηση να είναι άμεση.

## Φάσεις rollout

- **P0 — domain core** *(υλοποιημένο)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, πλήρες unit suite.
- **P1 — persistence + μία πηγή** *(υλοποιημένο)*: EF `calendar` schema (δικοί του πίνακες,
  append-only, hot indexes), το read-through `IEconomicCalendar` reader με point-in-time `asOf`,
  η idempotent append-only write service, ο FRED connector πίσω από ένα resilient typed client,
  και ο config-gated ingestion worker· Testcontainers integration tests (persistence, PIT,
  idempotency, blackout).
- **P2 — δημόσιο JWT REST API + Web UI** *(υλοποιημένο)*: το versioned, JWT-secured
  `/api/calendar/v1` API — client issuance, token exchange, και τα βασικά read endpoints (events,
  history, series, surprises, next, blackout, affected-symbols, health) με scope enforcement και
  two-tier gating, integration-tested. Συν τη mobile-first **`/economic-calendar` page** — ένα
  gated, fully-localized (23 γλώσσες) agenda επερχόμενων releases ως phone-friendly κάρτες με
  colour-banded impact chips και ένα MudBlazor **filter dialog** (currencies + minimum impact +
  ένα **From-date** picker για να πηδήξετε σε **οποιαδήποτε** παρελθούσα ημερομηνία σε όλο το
  ιστορικό — κανένα 60-day cap, κανένα τοίχος)· nav entry, smoke/mobile/a11y/E2E tested. Μια
  **per-indicator series history page** (`/economic-calendar/series/{code}`, linked from each
  event) παραθέτει το πλήρες print history μιας series. Τα surprise charts + infinite-scroll
  browser ακολουθούν.
- **P3 — περισσότερες πηγές & warm-up** *(ξεκίνησε)*: ένας **core-series catalog** (CPI,
  Core CPI, NFP, unemployment, GDP, PCE, Fed funds, retail sales → τα FRED ids τους) είναι
  seeded αυτόματα στο startup, και ένα one-time, idempotent, year-chunked **proactive backfill**
  τραβά την ιστορία ≥10 ετών τους ώστε η κοινή περίπτωση να είναι warm χωρίς να περιμένει ο
  χρήστης να χάσει κάτι. **Η ingestion είναι default-on** (`App:Calendar:IngestionEnabled`,
  default `true`): η **central-bank schedule source** χρειάζεται **κανένα API key**, οπότε το
  FOMC / ECB / BoE decision calendar πληρούται out of the box — το backfill seedάρει αυτές
  τις ημερομηνίες συνεδρίασης τόσο στο πρόσφατο ιστορικό όσο και στον forward ορίζοντα,
  οπότε η περιήγηση *του περασμένου μήνα* (ή οποιουδήποτε παρελθόντος παραθύρου) δείχνει τις
  συνεδριάσεις ακόμα και πριν διαμορφωθεί οποιοδήποτε FRED/BLS key· οι value series
  συμπληρώνονται μόλις τα keys τους διαμορφωθούν. Οι workers τιμούν το two-tier gate του
  ημερολογίου — ένα white-label deployment ή ο owner που απενεργοποιεί το economic-calendar
  feature σταματά την ingestion, και το `App:Calendar:IngestionEnabled=false` τον
  απενεργοποιεί explicit. **Per-source freshness** είναι επίσης real τώρα: ο worker
  καταγράφει το last successful poll κάθε πηγής, consecutive-failure count και ένα
  tripped-circuit flag (επιμένει στις app settings, cross-process), και το `/health` endpoint
  + το `calendar_health` MCP tool αναφέρουν μια αληθινή `stale` ετυμηγορία ανά πηγή. Η **BLS**
  (δεύτερη value source) και η **central-bank schedule source** (FOMC / ECB / BoE decision
  dates, backfilled σε ιστορικό και synced forward σε έναν ορίζοντα από τον worker) είναι in.
  Ακόμα να έρθει: BEA/Census/ECB-SDW/Eurostat/OECD value sources και το reconciliation pass.
- **P4 — βαθιά ενσωμάτωση**: **MCP tools** *(υλοποιημένο — πλήρες read-API parity:
  `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`,
  `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`,
  `calendar_health`, gated on το feature)* και το **alerts `EconomicEvent` trigger**
  *(υλοποιημένο — ένα `AlertRule` που πυροδοτεί N λεπτά πριν από ένα επερχόμενο release
  σε/πάνω από επιλεγμένο impact, προαιρετικά narrowing σε currencies· αξιολογείται από τον
  existing alert worker χωρίς AI, de-duplicated per release· δημιουργείται μέσω
  `POST /api/alerts/rules/economic-event`)*. Η prop-guard news-blackout gate **και το
  copy-trade blackout pause** είναι in (§5.1 — ένα opt-in `App:Copy:NewsPauseEnabled`,
  default off: ένα source position του οποίου το σύμβολο βρίσκεται σε Critical-impact
  blackout παραλείπεται, byte-identical hot path όταν off). Το **backtest event overlay**
  είναι in — `GET /api/calendar/v1/for-symbol` και το `calendar_events_for_symbol` MCP tool
  επιστρέφουν τα point-in-time-correct events που επηρεάζουν ένα σύμβολο σε ένα παράθυρο, και η
  **instance/backtest report page** αποδίδει τα high-impact releases που έπεσαν μέσα στο
  backtest window κάτω από την equity curve (ώστε ένας author να βλέπει ποιες συναλλαγές
  προσγειώθηκαν σε NFP), gated και localized. Όλο το σχέδιο είναι τώρα υλοποιημένο.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

Δείτε το [cBot & REST API reference](calendar-cbot-api.md) για την επιφάνεια ενσωμάτωσης.
