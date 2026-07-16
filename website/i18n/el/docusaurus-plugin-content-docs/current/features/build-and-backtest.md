---
description: "Δημιουργία, εκτέλεση και backtest cTrader cBots (C# και Python, και τα δύο .NET) από το ενσωματωμένο Monaco IDE, εκτέλεση στην επίσημη εικόνα ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Δημιουργία, εκτέλεση και backtest cTrader cBots (C# **και** Python, και τα δύο .NET) από το ενσωματωμένο Monaco
IDE, εκτέλεση στην επίσημη εικόνα `ghcr.io/spotware/ctrader-console`.

## Build

- Η σελίδα **Builder** φιλοξενεί τον επεξεργαστή Monaco. `CBotBuilder` μεταγλωττίζει το έργο με
  `dotnet build` **σε ένα αυτοκαταστρεφόμενο container** (`AppOptions.BuildImage`, κατάλογος εργασίας bind-mount
  στο `/work`), έτσι ώστε τα αναξιόπιστα στοιχεία MSBuild του χρήστη να μην φθάνουν το host. Η ανάκτηση NuGet
  αποθηκεύεται σε cache σε όλες τις μεταγλωττίσεις μέσω κοινού τόμου. Το host του Web χρειάζεται πρόσβαση
  στη υποδοχή Docker.
- Τα starter templates C# + Python βρίσκονται στο `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = Ιεραρχία κατάστασης TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Η μετάβαση αντικαθιστά την οντότητα (αλλαγή id),
  το id του container μεταφέρεται.
- `NodeScheduler` επιλέγει το λιγότερο φορτωμένο κατάλληλο Node. `ContainerDispatcherFactory` δρομολογεί
  στον απομακρυσμένο HTTP agent του Node ή στον τοπικό Docker dispatcher.
- Οι pollers ολοκλήρωσης συμφιλιώνουν τα containers που έχουν εξέλθει (backtest containers αυτο-κλείνουν μέσω
  `--exit-on-stop`). Report υπάρχει → ολοκληρώθηκε (αποθήκευση `ReportJson`), λείπει → απέτυχε.
- Τα ζωντανά logs του container ρέουν προς τον browser μέσω SignalR. Τα backtest equity curves αναλύονται
  από το report και σχεδιάζονται.

## Backtest market data is cached per account

Το cTrader Console λαμβάνει ιστορικά δεδομένα tick/bar στον κατάλογο `--data-dir`. Αυτός ο κατάλογος είναι
**σταθερή, διατηρούμενη cache με κλειδί το trading account** (ο αριθμός του λογαριασμού) — bind-mounted από το δίσκο
του Node στη δική του διαδρομή container (`/mnt/data`), ένα **ξεχωριστό, μη-ένθετο mount** από τον per-instance
κατάλογο εργασίας. Έτσι κάθε backtest στον ίδιο λογαριασμό **επαναχρησιμοποιεί** τα ήδη λήφθη δεδομένα αντί να τα
κατεβάσει ξανά κάθε εκτέλεση. (Παλαιότερα ο κατάλογος δεδομένων βρισκόταν κάτω από το per-instance κατάλογο
εργασίας, του οποίου το id άλλαζε κάθε εκτέλεση, το οποίο ανάγκαζε μια νέα λήψη κάθε backtest.) Ο εφήμερος
per-instance κατάλογος εργασίας εξακολουθεί να περιέχει το algo, τις παραμέτρους, τον κωδικό και το report. Η κοινή
cache δεδομένων υπολογίζεται στη χρήση backtest-data ενός Node και καθαρίζεται από την ενέργεια node-clean.

## Backtest settings

Το διάλογο **Backtest** εκθέτει κάθε ρύθμιση που το cTrader Console backtest CLI δέχεται, έτσι ώστε να μην
χρειάζεται ποτέ να αγγίξετε μια γραμμή εντολών:

- **From / To** — το παράθυρο backtest (`--start` / `--end`).
- **Data mode** — ένας από τους τρεις cTrader modes (`--data-mode`): **Tick data** (`tick`, ακριβή),
  **m1 bars** (`m1`, γρήγορη), ή **Open prices only** (`open`, γρηγορότατη).
- **Starting balance** — προεπιλέγεται σε `10000` (`--balance`). Ένα **μηδενικό υπόλοιπο δεν τοποθετεί trades και
  κάνει το cTrader να εκδώσει ένα κενό report που στη συνέχεια κολλάει** ("Message expected"), έτσι αποστέλλεται
  πάντα ένα μη-μηδενικό υπόλοιπο.
- **Commission** και **Spread** — `--commission` / `--spread` (spread σε pips).
- **Data file** (προαιρετικό) — μια Node-side διαδρομή σε ένα ιστορικό αρχείο δεδομένων (`--data-file`). Αφήστε κενό
  για να χρησιμοποιήσετε τα λήφθη/cached δεδομένα.
- **Expose environment variables** — ένας διακόπτης που περνάει τις μεταβλητές περιβάλλοντος του host στο cBot
  (η σημαία `--environment-variables`).

## Instance detail page

Το άνοιγμα ενός instance (`/instance/{id}`) δείχνει το ζωντανό του status, logs και — για ένα backtest — την equity
curve. Ο **τίτλος της καρτέλας του browser** αντικατοπτρίζει το συγκεκριμένο instance (**cBot name · kind · symbol**,
π.χ. `TrendBot · Backtest · EURUSD`) έτσι ώστε μια live-run tab και μια backtest tab να είναι διακριτές με μια ματιά.
Ένα run και ένα backtest του ίδιου cBot παρακολουθούνται ως διαφορετικές **lineages** (ένα σταθερό lineage id που
μεταφέρεται σε μεταβάσεις κατάστασης), έτσι η σελίδα ακολουθεί ακριβώς ένα instance και δεν μίγμα τα δεδομένα ενός
run με ένα backtest.

## Instance lifecycle controls

Κάθε σειρά instance (και η σελίδα λεπτομεριών της) έχει state-correct controls. Ένα **ενεργό** instance δείχνει
**Stop**. Ένα **terminal** (Stopped / Completed / Failed) δείχνει **Start (▶)** για να το επανεκκινήσει με το ίδιο
cBot, λογαριασμό, σύμβολο, χρονοδιάγραμμα, ParamSet και image (ένα run επανεκκινείται ως run, ένα backtest ως
backtest). Το κλικ Stop δείχνει ένα "Stopping…" notice και απενεργοποιεί το icon έως ότου επιλυθεί. Ένα νέο run
εμφανίζεται αμέσως στη λίστα — χωρίς page reload.

Τα Console logs **διατηρούνται όταν ένα instance τερματίζει** — για ένα run (στο Stop) και για ένα **backtest**
(κατά την ολοκλήρωση) ομοίως — έτσι ώστε τα τελευταία logs του run παραμένουν προβάσιμα στη σελίδα λεπτομεριών και,
μέσω της γραμμής εργαλείων log, **αντιγραφή στο πρόχειρο** (Copy logs icon) ή **λήψη** (Download logs icon) ακόμη και
μετά την αφαίρεση του container. Και οι δύο δρουν στο πλήρες console log του instance, όχι μόνο το on-screen tail.

Ένα uploaded `.algo` δεν κατασκευάστηκε ποτέ εδώ, έτσι η στήλη **Last Build** του στη σελίδα cBots αφήνεται κενή
(δείχνει χρόνο κατασκευής μόνο για cBots που κατασκευάζετε στο browser).

## Edit & re-run a stopped instance

Ένα **stopped** instance (run ή backtest) έχει ένα **Edit** control — ένα icon στη σειρά του στη λίστα **και** δίπλα
Start/Stop στη σελίδα λεπτομεριών του — που ανοίγει ένα διάλογο **προπληρωμένο** με τη τρέχουσα ρύθμιση του.
Μπορείτε να αλλάξετε το **trading account, σύμβολο, χρονοδιάγραμμα, ParamSet και image tag** (και, για ένα backtest,
το **παράθυρο και όλες τις backtest settings** παραπάνω). Το **Save & start** επανεκκινεί το με τις νέες ρυθμίσεις
(αντικαθιστώντας το stopped instance). Το control **απενεργοποιείται ενώ το instance είναι ενεργό** — μόνο ένα stopped
instance μπορεί να επεξεργαστεί.

## Run from the code editor

Το κλικ **Run** στον κώδικα editor ανοίγει ένα διάλογο αντί να ξεκινήσει ένα blind, hard-coded run:

- **Trading account** (απαιτούμενο) — ο cTrader λογαριασμός που συνδέεται το cBot.
- **Parameter set** (προαιρετικό) — επιλέξτε ένα υπάρχον σύνολο, ή αφήστε κενό για να τρέξετε με τις
  **default parameter values** του cBot. Ένα **+** κουμπί δίπλα στο selector δημιουργεί ένα νέο ParamSet inline (δείτε
  παρακάτω) και το επιλέγει.
- **Symbol / Timeframe** προεπιλέγονται στο `EURUSD` / `h1` και μπορούν να αλλάξουν. **Cancel** ή **Run**.

Κατά το **Run** ο editor αποθηκεύει + κατασκευάζει την τρέχουσα πηγή, ξεκινάει το instance στο επιλεγμένο λογαριασμό
με τις επιλεγμένες παραμέτρους, στη συνέχεια κολλάει τα ζωντανά logs του container. (Το log stream προωθεί το auth
cookie του συνδεδεμένου χρήστη στο `/hubs/logs` SignalR hub, έτσι ώστε να συνδεθεί αντί να αποτύχει με `Invalid
negotiation response received`.)

## Parameter sets

Ένα **parameter set** είναι ένα ονοματισμένο, επαναχρησιμοποιήσιμο σύνολο cBot parameter overrides που αποθηκεύεται
ως ένα flat JSON object αντιστοιχίζοντας κάθε όνομα παραμέτρου σε μια scalar value, π.χ. `{"Period": 14, "Label":
"trend"}`. Κατά την εκτέλεση/backtest μετατρέπεται στο cTrader `params.cbotset` αρχείο (`{ "Parameters": { … } }`).
Μπορείτε να δημιουργήσετε/επεξεργαστείτε ένα σύνολο ως raw JSON από το διάλογο **Parameter sets** του cBot ή inline
από το Run dialog.

Κάθε ParamSet **ανήκει σε ένα cBot**: το New Parameter Set dialog απαριθμεί όλα τα cBots σας και **πρέπει να
επιλέξετε ένα** — η δημιουργία μπλοκάρεται έως ότου επιλεγεί ένα cBot. Ένα σύνολο **το όνομα είναι μοναδικό ανά
cBot**: η δημιουργία ή μετονομασία ενός συνόλου σε ένα όνομα που ένα άλλο σύνολο του ίδιου cBot ήδη χρησιμοποιεί
απορρίπτεται (ένα σαφές σφάλμα στο διάλογο, `409 Conflict` στο API). Το ίδιο όνομα μπορεί να χρησιμοποιηθεί ξανά σε
ένα **διαφορετικό** cBot.

Το JSON είναι **validated** κατά την αποθήκευση: πρέπει να είναι ένα single flat object του οποίου οι τιμές είναι
όλες scalars (string / number / bool). Μια μη-object ρίζα, ένας array, ένα nested object, μια `null` τιμή, ή
κακοδιατυπωμένο JSON απορρίπτεται (ένα σαφές σφάλμα στο διάλογο, `400 Bad Request` στο API). Ένα κενό object `{}`
επιτρέπεται και σημαίνει "χωρίς overrides".

## cTrader Console CLI notes

Τα backtests χρειάζονται `--data-mode` (προεπιλογή `m1`), ημερομηνίες ως `dd/MM/yyyy HH:mm`, και
`params.cbotset` JSON positional arg. `run` απορρίπτει `--data-dir` (backtest-only). Δείτε
`ContainerCommandHelpers`.

## Nodes & scale

Η χωρητικότητα εκτέλεσης κλιμακώνεται με την προσθήκη Node agents (self-register + heartbeat). Δείτε
[node discovery](../operations/node-discovery.md) και [scaling](../deployment/scaling.md).

## A trading account is required

Το τρέξιμο ή backtest ενός cBot χρειάζεται ένα cTrader trading account για να συνδεθεί. Έως ότου προσθέσετε ένα κάτω
από **Trading accounts**, τα κουμπιά **Run New cBot** / **Backtest New cBot** είναι απενεργοποιημένα (με tooltip) και η
σελίδα εμφανίζει ένα prompt που συνδέει στη ρύθμιση λογαριασμού — δεν χτυπάτε πλέον ένα raw `stream connect failed`
σφάλμα από ένα bot χωρίς λογαριασμό.
