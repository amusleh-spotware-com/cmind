---
description: "AI macro currency strength & forward outlook — κατάταξη νομισμάτων βάσει τρέχουσας θεμελιώδους ισχύος με AI-assistance, ντετερμινιστικός υπολογισμός, και forward προοπτική ανά ζεύγος για επιλεγμένο ορίζοντα."
---

# AI macro currency strength & forward outlook

Το cMind αποστέλλει μια **AI-assisted, math-deterministic** μηχανή macro currency-strength.
Κατατάσσει ένα διαμορφώσιμο σύμπαν νομισμάτων — τα 8 majors συν αναδυόμενες αγορές και exotic
νομίσματα — βάσει **τρέχουσας** θεμελιώδους ισχύος, και προβάλλει μια **forward κατευθυντική
προοπτική** για κάθε ζεύγος σε επιλεγμένο ορίζοντα (1M / 3M / 6M / 12M). Κάθε κατάταξη, κάθε
pair bias και κάθε αριθμός υπολογίζεται από καθαρά ντετερμινιστικά μαθηματικά στο domain core·
το LLM μόνο **συλλέγει** τα forward-looking inputs που τα δεδομένα δεν μπορούν να δημοσιεύσουν
και **εξηγεί** το αποτέλεσμα σε απλά Αγγλικά. Ποτέ δεν εφευρίσκει κατάταξη, κατεύθυνση ή αριθμό.

> **Ειλικρινής περιορισμός.** Τα fundamentals προβλέπουν καλά τη μεσαία-μακροπρόθεσμη αξία και
> φτωχά τη βραχυπρόθεσμη. Αντιμετωπίστε το ως φίλτρο θέσης / confluence, **όχι** ως short-term
> timing signal. Οι μετρήσεις κοντά σε high-impact releases (NFP/CPI/κεντρική τράπεζα) είναι θορυβώδεις.
> Δεν είναι χρηματοοικονομική συμβουλή.

## Πώς λειτουργεί

1. **Τα τρέχοντα fundamentals προέρχονται από το Economic Calendar, όχι το LLM.** Οι σκληροί
   αριθμοί — επιτόκια πολιτικής, CPI vs στόχος, ΑΕΠ, απασχόληση, εμπορικό ισοζύγιο — και οι
   **surprise z-scores** τους προέρχονται **point-in-time** από τη μονάδα [economic calendar](./economic-calendar.md)
   (FRED/BLS/BEA/ECB και προγράμματα κεντρικών τραπεζών). Ένα ιστορικό snapshot δεν διαρρέει ποτέ look-ahead.
2. **Το LLM συλλέγει μόνο ό,τι το ημερολόγιο δεν μπορεί να δημοσιεύσει** — ανά νόμισμα: την
   **forward** τροχιά (αναμενόμενη πορεία επιτοκίου πολιτικής σε bp, τάση πληθωρισμού-vs-στόχος,
   ορμή ανάπτυξης) και μια **γεωπολιτική** προοπτική (risk-on/off, δασμοί, δημοσιονομικά/χρέος,
   εκλογές), συν τυχόν EM/exotic στοιχεία που το ημερολόγιο στερείται. Αυστηρό JSON, tier-aware
   validation, web search on.
3. **Το domain υπολογίζει την κατάταξη και τον forward πίνακα ντετερμινιστικά.** Κάθε driver
   βαθμολογείται ως **within-tier z-score** (ώστε ένα exotic με 50% πληθωρισμό να μην
   διαστρεβλώνει τα majors), winsorized, weight-summed σε ένα composite, και κατατάσσεται
   strongest→weakest με stable ISO tie-break. Η forward στρώση μεταφέρει κάθε composite
   κατά μήκος της τροχιάς του —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — και αντιστοιχίζει το
   projected differential κάθε ζεύγους σε ένα **directional bias** (▲ appreciate / ▬ neutral /
   ▼ depreciate) με μια conviction.
4. **Το LLM εξηγεί** την κατάταξη και τις κορυφαίες pair calls σε απλή γλώσσα.

## Οι drivers

| Driver | Επίδραση στην ισχύ | Σημειώσεις |
|---|---|---|
| Επιτόκιο πολιτικής & τροχιά | Υψηλότερο / hawkish ⇒ ισχυρότερο | Υψηλότερο βάρος· η απόκλιση κεντρικών τραπεζών οδηγεί τα μεγαλύτερα κενά. |
| Πληθωρισμός (CPI vs στόχος) | Πάνω από στόχο ⇒ ασθενέστερο | Βαθμολογείται αντίστροφα (drag αγοραστικής δύναμης). |
| Ανάπτυξη ΑΕΠ | Υψηλότερη σχετική ανάπτυξη ⇒ ισχυρότερο | Διαφορικό έναντι του πάνελ. |
| Απασχόληση | Ισχυρότερο εργατικό δυναμικό ⇒ ισχυρότερο | Τροφοδοτεί την πορεία πολιτικής. |
| Εμπορικό ισοζύγιο / τρεχούμενος λογαριασμός | Πλεόνασμα ⇒ ισχυρότερο | Διαρθρωτική ζήτηση. |
| Στάση πολιτικής | Hawkish ⇒ ισχυρότερο | Ο κύριος μακροπρόθεσμος driver. |
| Surprise momentum | Πρόσφατα beats ⇒ ισχυρότερο | Από τις surprise z-scores του ημερολογίου. |
| Γεωπολιτικά / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) ισχυρότερα | Οριοθετημένο forward risk delta. |
| Real yield / carry *(EM/exotic)* | Θετικό real rate ⇒ ισχυρότερο | Κυρίαρχος EM driver σε ήρεμα regimes. |
| Εξωτερική ευπάθεια *(EM/exotic)* | Ελλείμματα / χαμηλά αποθέματα / χρέος USD ⇒ ασθενέστερο | Διαρθρωτική πίεση υποτίμησης. |
| Όροι εμπορίου *(commodity exporters)* | Ανερχόμενες τιμές εξαγωγών ⇒ ισχυρότερο | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Πολιτικός / θεσμικός κίνδυνος *(EM/exotic)* | Αστάθεια ⇒ ασθενέστερο | Ευρύτερο dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

Το σύμπαν είναι **διαμορφώσιμο ανά deployment** (`App:CurrencyStrength:Universe`) — η
προσθήκη ενός νομίσματος είναι config, όχι κώδικας. Κάθε νόμισμα φέρει ένα **tier**
(`Major` / `EmergingMarket` / `Exotic`) που ρυθμίζει τη στάθμιση, το πλάτος του dead-band
και το cap conviction:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (led by rate level).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK)· carry
  + risk + external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, συν USD-pegged HKD/SAR· low confidence, wider dead-band,
  capped conviction. **Pegged / heavily-managed** νομίσματα (HKD, SAR, CNH) σημαίνονται, η
  τροχιά τους down-weighted, και η pair outlook τους clampάρεται προς `Neutral` ώστε ένα
  peg να μη διαβάζεται ποτέ ως free-floating signal.

Επειδή τα επίσημα EM/exotic στατιστικά είναι χαμηλότερης συχνότητας, αναθεωρημένα και
μερικές φορές αδιαφανή, τα AI-gathered στοιχεία φέρουν μια **per-tier confidence** που
εμφανίζεται ως reliability badge.

## Graceful degradation

| Calendar | AI | Αποτέλεσμα |
|---|---|---|
| ✅ | ✅ | Πλήρης κατάταξη + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only τρέχουσα κατάταξη, χωρίς forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered τρέχοντα στοιχεία + forward, χαμηλότερη confidence (`AiOnly`). |
| ❌ | ❌ | Κανένα snapshot — το widget κρύβεται και η σελίδα δείχνει empty state. |

Η εφαρμογή τρέχει αμετάβλητη είτε έτσι είτε αλλιώς. Το AI είναι gated στο AI key· το
calendar leg σέβεται το δικό του white-label gate + runtime toggle.

## Χρήση

- **Ενεργοποιήστε το AI** (Settings → AI) και **ενεργοποιήστε το widget** από το δικό σας
  dashboard **Customize** dialog ("Currency strength" — opt-in, κρυφό εξ ορισμού). Το widget
  δείχνει τα κορυφαία strong/weak νομίσματα και την κορυφαία 3M pair call· επιστρέφει στην
  πλήρη σελίδα.
- **Πλήρης σελίδα** — `/ai/currency-strength`: ένας horizon selector (1M/3M/6M/12M), ένα tier
  filter (All/Majors/EM/Exotics), την τρέχουσα κατάταξη, το forward forecast, τον pair-outlook
  matrix (bias + conviction, pegged/low-confidence flagged), και την AI narrative. Πατήστε
  **Refresh now** (owner) για αναγέννηση. Ένα background worker
  (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) ανανεώνει σε πρόγραμμα ώστε η
  σελίδα να είναι πληθυσμένη εξ αρχής· μια deployment ή ο owner τον απενεργοποιεί (ή απενεργοποιεί
  το AI / economic-calendar feature, που ο refresher τιμά υποβαθμίζοντας σε no snapshot).

## Προγραμματιστική πρόσβαση

Ένα shared read model (`ICurrencyStrengthQuery`) είναι προσβάσιμο με τρεις τρόπους:

- **In-app AI** — εγχέεται απευθείας (in-process) σε AI features.
- **MCP** — το `currency_strength` tool (params `horizon`, `tier`) για AI clients/agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`,
  secured από την **ίδια** `CalendarJwt` μηχανή με το [calendar cBot API](./calendar-cbot-api.md)
  με ένα επιπλέον **`market:read`** scope. Ένα cBot καταχωρεί ένα API client με `market:read`,
  ανταλλάσσει το id + secret του για ένα βραχύβιο JWT στο `POST /api/calendar/v1/token`, και
  καλεί τα endpoints με ένα `Bearer` token. Κανένα δεύτερο JWT σχήμα, κανένα δεύτερο μυστικό
  — ένα διαρρεύσαν token είναι read-only, market-scoped, βραχύβιο και revocable.

Δείτε το [calendar cBot API](./calendar-cbot-api.md) για τη ροή του token και ένα copy-paste
sample.
