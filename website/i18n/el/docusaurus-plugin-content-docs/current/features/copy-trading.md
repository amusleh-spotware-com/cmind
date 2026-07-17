---
description: "Αντιγραφή λογαριασμού master cTrader σε έναν ή περισσότερους slave λογαριασμούς — διαδιακοπής, διαφορετικές cID — με έλεγχο ανά προορισμό + συμφωνία τραπεζικής ποιότητας."
---

# Copy trading

Αντιγραφή **master** λογαριασμού cTrader σε έναν ή περισσότερους **slave** λογαριασμούς — διαδιακοπής, διαφορετικές cID — με έλεγχο ανά προορισμό + συμφωνία τραπεζικής ποιότητας.

## Concepts

- **Copy profile** — ένα master (`SourceAccountId`) + ένας ή περισσότεροι **προορισμοί**. Κύκλος ζωής: `Draft → Running → Paused → Stopped` (`Error` σε περίπτωση αποτυχίας). Aggregate root: `CopyProfile` (ιδιοκτησία `CopyDestination`).
- **Destination** — ένας slave λογαριασμός + πλήρο σύνολο κανόνων για το πώς αντιγράφεται το master σε αυτόν. Όλη η ρύθμιση ανά προορισμό, οπότε ένα master μπορεί να τροφοδοτήσει συντηρητικά + επιθετικά slave λογαριασμάτα ταυτόχρονα.
- **Copy engine host** — εργαζόμενος που εκτελείται για profile (`CopyEngineHost`). Συνδρομή στο stream εκτέλεσης του master, εφαρμογή κάθε γεγονότος σε κάθε προορισμό.
- **Supervisor** — `CopyEngineSupervisor`, background service σε κάθε node. Φιλοξενεί αναθεμένα profiles, αυτό-διορθώνει στο cluster (δείτε [scaling](../deployment/scaling.md)).

## What gets mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Άνοιγμα μιας σταθμισμένης αντιγραφής (επισημασμένης με το ID της πηγαίας θέσης) |
| Limit / stop / stop-limit pending order | Τοποθέτηση της αντίστοιχης pending order |
| Pending order amend | Τροποποίηση της αντιγραφής pending order στη θέση της |
| Pending order cancel / expiry | Ακύρωση της αντιγραφής pending order |
| Partial close | Κλείσιμο του ίδιου ποσοστού της slave θέσης |
| Scale-in (volume increase) | Άνοιγμα του πρόσθετου όγκου (opt-in) |
| Stop-loss / trailing-stop change | Τροποποίηση της προστασίας της slave θέσης |
| Full close | Κλείσιμο της slave αντιγραφής |

Κάθε αντιγραφή **επισημασμένη με το ID της πηγαίας θέσης/order**. Μετά την επανασύνδεση, ο host ανακατασκευάζει την κατάσταση από reconcile: ανοίγει αντιγραφές που διατηρεί το master αλλά λείπουν από το slave, κλείνει slave "ορφανές" που το master δεν διατηρεί πλέον — **χωρίς να διπλασιάζει trades**.

## Creating a profile

Το **Νέο Προφίλ** ανοίγει ένα αφιερωμένο **φόρμα ολόκληρης σελίδας** (`/copy-trading/new`), όχι διάλογο — το σύνολο επιλογών είναι αρκετά μεγάλο ώστε μια σελίδα να διαβάζεται καλύτερα σε τηλέφωνο και desktop. Συλλέγει τα πάντα εκ των προτέρων: όνομα profile, πηγαία (master) λογαριασμός, προορισμοί (slave) λογαριασμοί (multi-select με κουμπί **Επιλογή όλων**; ο επιλεγμένος master εξαιρείται από τη λίστα slave), + το πλήρες σύνολο επιλογών ανά προορισμό. **Μόνο λογαριασμοί που συνδέονται μέσω του cTrader Open API είναι επιλέξιμοι** ως master ή προορισμός — η αντιγραφή τοποθετεί εντολές μέσω του Open API, έτσι ένας χειροκίνητα προστιθέμενος (μόνο cID) λογαριασμός δεν μπορεί να αντιγράψει και δεν εμφανίζεται στη λίστα; όταν δεν συνδέονται λογαριασμοί, η σελίδα εμφανίζει σημείωση που δείχνει στους Λογαριασμούς Διαπραγμάτευσης. Οι τρόποι μεγέθους, κατεύθυνση και φίλτρο συμβόλων **απεικονίζονται ως ανθρώπινες ετικέτες με επεξήγηση με κουκκίδες ανά τρόπο** στη βοήθεια διαχείρισης χρημάτων. **Κάθε έλεγχος φέρει ένα tooltip βοήθειας** που εξηγεί τι κάνει και πώς να το χρησιμοποιήσει. Δομημένες εισροές χρησιμοποιούν **σωστούς επικυρωμένους ελέγχους** — αριθμοί/ποσοστό μέσω αριθμητικών πεδίων, τρόποι/κατεύθυνση/φίλτρο μέσω selects, το σύμβολο φίλτρου μέσω λίστας προσθήκης/κατάργησης συμβόλων και ο χάρτης συμβόλων μέσω πίνακα προσθήκης/κατάργησης σειρών `Πηγή → Προορισμός (× πολλαπλασιαστής)` — ποτέ ένα blob κειμένου χωρισμένο με κόμματα. Όλες οι εισροές **επικυρώνονται πριν από την αποθήκευση** — ονόματος/πηγαίας/προορισμού που λείπουν, παράμετρος σταθμίσεως όχι θετική, αρνητικά/ασυνεπή όρια lot, ποσοστό drawdown εκτός εύρους, κανένας τύπος εντολής ενεργοποιημένος, ή κενό σύμβολο φίλτρου εμφανίζονται ως λίστα σφάλματος + εμποδίζουν την αποθήκευση. Κατά τη δημιουργία, το προφίλ δημιουργείται + κάθε επιλεγμένος slave προστίθεται με τις επιλεγμένες ρυθμίσεις, στη συνέχεια η σελίδα επιστρέφει στη λίστα Copy Trading.

**Εισαγωγή / εξαγωγή.** Ολόκληρο το σύνολο ρυθμίσεων μπορεί να **εξαχθεί σε αρχείο JSON** και να **εισαχθεί** ξανά για προ-συμπλήρωση του φόρμας, έτσι ώστε μια συνήθεια να μπορεί να επαναχρησιμοποιηθεί σε διαφορετικά προφίλ χωρίς επανατύπωση. Ο χάρτης συμβόλων μπορεί επίσης να **εξαχθεί / εισαχθεί ως αρχείο CSV** (`Source,Destination,VolumeMultiplier`) — προετοιμάστε έναν μεγάλο χάρτη συμβόλων του broker σε ένα υπολογιστικό φύλλο και φορτώστε τον σε ένα βήμα. Οι ίδιοι έλεγχοι συμβόλων και η εισαγωγή/εξαγωγή CSV είναι επίσης διαθέσιμα στο διάλογο προορισμού στη σελίδα Copy Trading.

Οι ενέργειες σειράς σέβονται τον κύκλο ζωής: **Εκκίνηση** ενεργοποιημένη μόνο όταν δεν εκτελείται, **Σταματήστε** + **Παύση** μόνο όταν εκτελείται, **Διαγραφή** απενεργοποιημένη κατά την εκτέλεση + ζητά επιβεβαίωση πριν αφαιρέσει το προφίλ + τους προορισμούς.

## Per-destination options

Ορίστε στη σελίδα New Profile, στο διάλογο προορισμού στη σελίδα Copy Trading, ή μέσω `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + παράμετρος): σταθερό lot, lot/notional multiplier, proportional balance/equity/free-margin, σταθερό risk %, σταθερό leverage, auto-proportional, **risk-%-from-stop** (M7). Συν ελάχιστο/μέγιστο όρια lot + force-min-lot. **Risk-from-stop** σταθμίζει τον προορισμό ώστε να κινδυνεύει με ρυθμιστέο ποσοστό *της δικής του* απόδοσης, προερχόμενο από **απόσταση stop-loss του master** (`master κινδυνεύει 2% → slave auto-κινδυνεύει 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Ανοιχτό master **χωρίς** stop-loss δεν έχει απόσταση για σταθμίσεται εναντίον → χρησιμοποιεί ρυθμιστέο **max-risk fallback lot** (M7) αν τεθεί, αλλιώς παραλείπεται (`no_stop_loss`) όχι μαντεύεται. Proportional-**equity**/**free-margin** μέγεθος από πραγματική λογαριασμού **equity** (`balance + Σ floating P&L`, προερχόμενο ανά cTrader Open API το οποίο δεν παρέχει equity), όχι απλό balance — έτσι το master που κάθεται σε ανοικτό κέρδος/ζημία σταθμίζει αντιγραφές σωστά. Χρησιμοποιημένο περιθώριο δεν εκτίθεται από reconcile API, οπότε free-margin αντιμετωπίζεται ως equity (ειλικρινές διαθέσιμα-κεφάλαια proxy); άλλοι modes διαβάζουν balance + παραλείπουν επιπλέον γύρο αποτίμησης.
- **Direction filter**: both / long-only / short-only. **Reverse**: αναστροφή πλευράς (+ swap SL↔TP) για contrarian αντιγραφή.
- **Manage-only** (Ignore-New-Trades / Close-Only): αντιγραφή κλεισίματα, μερικά κλεισίματα + αλλαγές προστασίας σε ήδη αντιγραμμένες θέσεις, αλλά άνοιγμα **χωρίς** νέες θέσεις/pending orders (παραλείπεται `manage_only`). Χρησιμοποιήστε για το κατέβασμα προορισμού χωρίς κοπή υπάρχουσών αντιγραφών.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): στην **πρώτη** resync του profile, αν άνοιγμα αντιγραφών για προ-υπάρχουσες θέσεις του master, + αν κλείσιμο αντιγραφών που ο master έκλεισε ενώ το profile ήταν σταματημένο. Και τα δύο ισχύουν μόνο κατά την έναρξη — mid-run reconnect πάντα reconciles πλήρως έτσι η desync ανακάμπτει ανεξάρτητα.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Κάθε symbol-map entry φέρει προαιρετικά **per-symbol volume multiplier** (cMAM per-symbol override) scaling copy size για αυτό το σύμβολο επί του sizing προορισμού (1 = χωρίς αλλαγή). Ολόκληρος ο χάρτης εισάγει/εξάγει ως **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; στήλες `Source,Destination,VolumeMultiplier`) — κάθε σειρά επικυρώνεται μέσω domain value objects, οπότε κακοδημιουργημένο αρχείο δεν μπορεί να παράγει άκυρο χάρτη.
- **Trading-hours window** (C18) — ανά-προορισμό καθημερινό UTC παράθυρο (`start`/`end` minutes-of-day, end exclusive; `start == end` = all-day). Νέα άνοιγματα έξω από παράθυρο παραλείπονται (`trading_hours`); παράθυρο με `start > end` τυλίγεται περά τα μεσάνυχτα (π.χ. 22:00–06:00). Υπάρχουσες θέσεις παραμένουν διαχειρίσιμες.
- **Source-label filter** (C18, cTrader equivalent του MT magic-number filter) — όταν ορίζεται, αντιγραφή μόνο master trades του οποίου η ετικέτα ταιριάζει **ακριβώς** (π.χ. trades ενός bot, ή manual-only label); αλλιώς παραλείπεται (`source_label`). Κενό = αντιγραφή όλα. Μεταφερόμενο στο `ExecutionEvent.SourceLabel` από master position/order του `TradeData.Label`, τιμάται και κατά το resync.
- **Account protection** (ZuluGuard / Global Account Protection) — παρακολούθηση προορισμού **live equity** (`balance + Σ floating P&L`, polled κάθε `CopyDefaults.EquityGuardInterval`) εναντίον `StopEquity` floor και/ή προαιρετικό `TakeEquity` ceiling. Στη παραβίαση, εφαρμογή mode: **CloseOnly** (σταματήστε νέες αντιγραφές, διατηρήστε υπάρχουσες), **Frozen** (σταματήστε άνοιγμα), **SellOut** (κλείστε **κάθε** αντιγραφή στον προορισμό αμέσως). Μόλις ενεργοποιηθεί, προορισμός latched — χωρίς νέα άνοιγματα έως ο host να επανεκκινηθεί — + `CopyAccountProtectionTriggered` alert σηκώθηκε. `SellOut` απαιτεί `StopEquity`; `TakeEquity` πρέπει να κάθεται πάνω από `StopEquity`. **Χωρίς εγγύηση caveat:** sell-out χρησιμοποιεί market execution — όπως κάθε competitor's equivalent, δεν μπορεί να εγγυηθεί τιμή πλήρωσης σε γρήγορη/gapped αγορά.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` αμέσως κλείνει **κάθε** copied position σε κάθε προορισμό + κλειδώνει εναντίον νέων ανοιγμάτων. Δρομολόγηση cross-process: API θέτει flag, supervisor παραδίδει σε running host (επαναχρησιμοποίηση channel rotation token), το οποίο ισοπεδώνει στη θέση· flag εκκαθαρίστηκε έτσι ενεργοποιείται ακριβώς μία φορά (`CopyFlattenAll` alert). Χρήστης τότε παύει/σταματά profile.
- **Prop-firm rule guard** (C7) — enforcement prop-firm copier users ζητούν. Ανά προορισμό, **daily-loss cap** (ζημία από equity αγοράς της ημέρας) και/ή **trailing-drawdown** limit (ζημία από τρέχον peak equity), και τα δύο σε νόμισμα κατάθεσης. Στη παραβίαση προορισμός **auto-flattened** (κάθε αντιγραφή κλειστή) + **locked out** rest του UTC day (νέα άνοιγματα παραλείπεται `prop_lockout`); `CopyPropRuleBreached` alert πυροδοτείται. Lockout καθαρίζει όταν UTC day κυλίεται πάνω (νέο baseline/peak λαμβάνεται). Μοιράζεται ίδιο live-equity poll με account protection.
- **Execution jitter** (C11, off by default) — τυχαίο `0..N` ms delay πριν τοποθετηθεί κάθε αντιγραφή, για de-correlate σχεδόν πανομοιότυπα order timestamps σε λογαριασμούς της **ίδιας του** χρήστη. **Compliance caveat:** aid για prop firms που *επιτρέπουν* αντιγραφή — **όχι** εργαλείο για παρακάμψεις firm που απαγορεύει· παραμονή εντός των κανόνων της firm σας είναι δική σας ευθύνη.
- **Config lock** (C9) — freeze ρυθμίσεων προορισμού για περίοδο (`POST …/destinations/{id}/lock` με λεπτά). Κατά την κλείδωση, προορισμός δεν μπορεί να αφαιρεθεί (aggregate απορρίπτει με `CopyDestinationConfigLocked`) — σκόπιμος φυλάκας εναντίον ωμής αλλαγής κατά drawdown. Κλείδωμα λήγει αυτόματα στο timestamp του.
- **Consistency pre-alert** (C10) — προειδοποίηση (μία φορά ανά UTC day) όταν **daily profit** προορισμού φτάνει ρυθμιστέο ποσοστό ημερήσιας equity αγοράς (`CopyConsistencyThresholdApproaching`), έτσι prop-firm consistency rule σεβαστός *πριν* εμπλακεί. Profit-side, ανεξάρτητο από loss-side lockout; εκτελείται από ίδιο day baseline με prop-rule guard.
- **Order-type filter** — επιλογή ακριβώς ποιοι master order types να αντιγράφονται: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; default all). cMAM-style selectivity.
- **Copy SL / Copy TP** — αντιγραφή stop-loss / take-profit του master, ή ανεξάρτητη διαχείριση προστασίας.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — κάθε ανεξάρτητα toggleable.
- **Copy pending expiry** (default on) — αντιγραφή master pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (default on) — για market-range + stop-limit orders, τοποθέτηση slave order με ακριβές slippage-in-points του master (base price λαμβάνεται από slave's live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy αν slave price μετακινήθηκε πέρα από N pips από master entry). **Max copy delay** μετρημένο εναντίον master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) μέσω injected `TimeProvider`: signal παλαιότερο του ρυθμιστέου max-lag παραλείπεται, έτσι stale copy ποτέ δεν τοποθετείται καθυστερημένο (πρέπει delay πάντα μηδέν + guard νεκρό).
- **SL/TP precision normalization** (M6) — αντιγραφή stop-loss/take-profit τιμές στρογγυλεμένες σε **destination** symbol's digit precision πριν amend, έτσι master price σε λεπτότερη precision (ή cross-broker digit mismatch) ποτέ δεν ενεργοποιεί server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — προορισμός απορρίπτων `CopyDefaults.RejectionBudget` ανοίγματα σε σειρά **tripped**: χωρίς νέα άνοιγματα για cooldown window (`CopyDestinationTripped` alert πυροδοτείται), σταματώντας απορρίψεις άνοιγμα από hammering (prop-firm) λογαριασμό. Υπάρχουσες θέσεις ακόμα διαχειρίσιμες + κλειστές κατά tripped; breaker auto-resets μετά cooldown + successful copy καθαρίζει counter.
- **Lot sanity ceiling** (C14) — απόλυτο μέγιστο copy size και/ή multiple-of-master cap. Υπολογισμένη αντιγραφή υπέρβαση absolute cap, ή υπέρβαση `N×` master's own lot size, **hard-blocked** (επιφανές ως `lot_sanity` skip, μετρημένο σε `cmind.copy.skipped`) δεν τοποθετείται — υπερασπίζει εναντίον catastrophic-oversize class (0.23-lot master γίνεται 3 lots σε κάθε receiver μέσω runaway multiplier ή rounding bug). Και τα δύο dimensions default `0` (off).

## Reliability & edge cases

Engine κατασκευασμένο για πραγματικότητα ότι τίποτα δεν μπορεί να αποτύχει οποιαδήποτε στιγμή:

- **Slave-pending fill-correlation timeout** (C13) — αντιγραμμένη slave pending του οποίου master pending εξαφανίστηκε (ούτε ξεκουράζεται ούτε φρεσκοπληρωθεί) ακυρώθηκε μετά correlation timeout, έτσι slave copy δεν μπορεί να πληρωθεί αλλοιωμένη σε unmanaged position (`CopyPendingTimedOut`). Resync επίσης καθαρίζει order-id-labelled filled-pending orphan.
- **Robust close/flatten** (M8) — κλείσιμο orphan σε resync, ή ισοπέδωση σε guard breach, ανοχή position broker ήδη κλειστή (`POSITION_NOT_FOUND`): κάθε κλείσιμο εκτελείται ανεξάρτητα, έτσι μία stale id ποτέ δεν αποτυγχάνει resync ή αφήνει rest του λογαριασμού un-flattened.

- **Start με master ήδη σε trades** — σε start host reconciles + ανοίγει αντιγραφές για υπάρχουσες θέσεις του master.
- **Connection drops / desync** — σε reconnect host reconciles: ανοίγει missing αντιγραφές, κλείνει orphans, re-labels pendings. Χωρίς duplicate orders.
- **Order placement failure** — αποτυχία σε έναν προορισμό καταγράφεται, ποτέ δεν αποκλείει άλλους προορισμούς.
- **Single valid token ανά cID** — cTrader ακυρώνει cID's παλιό access token στιγμή που κδοθεί νέο. cMind swaps running host's token **στη θέση** (re-auth σε live socket) έτσι copying συνεχίζεται χωρίς dropping stream. Δείτε [token lifecycle](token-lifecycle.md).

## Auditability

Κάθε ενέργεια εκπέμπει δομημένο, source-generated log event (`LogMessages`) με profile id, destination cID, order/position ids, + values — order placed/skipped (με λόγο), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored, market-range slippage mirrored, token swapped, resync summary. Αυτή είναι η audit trail για compliance + dispute resolution.

Παράλληλα με logs, engine εκπέμπει **OpenTelemetry metrics** σε `cMind.Copy` meter (καταχωρημένο σε shared OTel pipeline, exported πάνω OTLP / σε Azure Monitor όπως rest): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out σε όλους προορισμούς, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged by reason), + `cmind.copy.failed`. Αυτά κάνουν latency/slippage regression measurable, όχι μόνο ορατά σε log line — live suite asserts τα εναντίον budget.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — create (με προαιρετικό destination account ids).
- `GET /api/copy/profiles/{id}` — full detail incl. κάθε destination option.
- `POST /api/copy/profiles/{id}/destinations` — add a destination με το πλήρες option set.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Εκτελείται εναντίον `FakeTradingSession`, cTrader-faithful in-memory simulator.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation σε real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip μέσω API + UI, full lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` σε quiescence + assert convergence invariants. Δείτε [testing/stress-testing.md](../testing/stress-testing.md). Αυτή η suite επιφάνεια + fixed real startup race: `OnReconnected` wired πριν initial reference-load + resync, έτσι socket flap κατά startup θα μπορούσε να τρέξει δεύτερη resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync τώρα τρέχουν κάτω από `_stateGate`.
- **Live** — real cTrader demo accounts; δείτε [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Δείτε [dev-credentials.md](../testing/dev-credentials.md) για ενιαίο credentials file live + E2E tiers διάβασμα.

## Profile controls and destination management

Start/stop είναι icon buttons σε κάθε profile row (απενεργοποιημένο όταν η ενέργεια δεν ισχύει). Source και destination λογαριασμοί εμφανίζονται από τον **account number** τους, ποτέ ένα εσωτερικό id. Κλικ στο profile ανοίγει ένα **διάλογο** για διαχείριση των destination accounts του (προσθήκη/αφαίρεση με πλήρες per-destination settings).
