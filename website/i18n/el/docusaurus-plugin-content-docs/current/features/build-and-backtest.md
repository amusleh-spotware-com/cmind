---
description: "Δημιουργία, εκτέλεση, backtesting cTrader cBots (C# και Python, και τα δύο .NET) από ενσωματωμένο Monaco IDE στο πρόγραμμα περιήγησης, εκτέλεση στην επίσημη εικόνα ghcr.io/spotware/ctrader-console."
---

# Δημιουργία & backtesting cBots

Δημιουργία, εκτέλεση, backtesting cTrader cBots (C# **και** Python, και τα δύο .NET) από ενσωματωμένο Monaco
IDE, εκτέλεση στην επίσημη εικόνα `ghcr.io/spotware/ctrader-console`.

## Δημιουργία

- Η σελίδα **Builder** φιλοξενεί τον επεξεργαστή Monaco· το `CBotBuilder` μεταγλωττίζει το έργο με
  `dotnet build` **σε ephemeral container** (`AppOptions.BuildImage`, ο κατάλογος εργασίας bind-mount
  στο `/work`), έτσι ώστε ο κώδικας MSBuild δεν φτάνει στο host. Η αποκατάσταση NuGet
  αποθηκεύεται στη cache σε όλες τις δημιουργίες μέσω κοινού όγκου. Ο διακομιστής Web χρειάζεται πρόσβαση σε Docker socket.
- Τα πρότυπα εκκίνησης C# + Python βρίσκονται στο `src/Nodes/Builder/Templates/`.

## Εκτέλεση & backtesting

- **Instances** = ιεραρχία TPH state (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Η μετάβαση αντικαθιστά την οντότητα (αλλαγή id),
  το container id μεταφέρεται.
- Ο `NodeScheduler` επιλέγει τον λιγότερο φορτωμένο κατάλληλο κόμβο· ο `ContainerDispatcherFactory` δρομολογεί
  στον απομακρυσμένο κόμβο HTTP agent ή τοπικό Docker dispatcher.
- Οι pollers ολοκλήρωσης συμφιλιώνουν τα εξερχόμενα containers (τα backtest containers αυτό-εξέρχονται μέσω
  `--exit-on-stop`)· το report παρούσα → ολοκληρωμένο (αποθήκευση `ReportJson`), ελλιπές → failed.
- Τα live container logs εκρέουν στο πρόγραμμα περιήγησης μέσω SignalR· οι καμπύλες equity του backtest αναλύονται από
  το report + διαγράφονται.

## Τα δεδομένα αγοράς του backtest αποθηκεύονται στη cache ανά λογαριασμό

Το cTrader Console κατεβάζει ιστορικά δεδομένα tick/bar στο `--data-dir`. Εκείνος ο κατάλογος είναι μια
**σταθερή, μόνιμη cache με κλειδί στο λογαριασμό διαπραγμάτευσης** (τον αριθμό λογαριασμού του) — bind-mounted από τον δίσκο του κόμβου στη δική του διαδρομή container (`/mnt/data`), ένα **ξεχωριστό, μη-ένθετο mount** από τον κατάλογο εργασίας ανά instance. Έτσι κάθε backtest στον ίδιο λογαριασμό **επαναχρησιμοποιεί** τα ήδη κατεβασμένα δεδομένα αντί να τα κατεβάσει ξανά. (Νωρίτερα ο κατάλογος δεδομένων βρισκόταν κάτω από τον κατάλογο εργασίας ανά instance, του οποίου το id αλλάζει κάθε εκτέλεση, η οποία έναγε σε νέο κατέβασμα κάθε backtest.) Ο εφήμερος κατάλογος εργασίας ανά instance εξακολουθεί να κρατάει το algo, params, password και report· η κοινή cache δεδομένων υπολογίζεται στη χρήση backtest-data ενός κόμβου και διαγράφεται από τη δράση node-clean.

## Ρυθμίσεις Backtest

Το διάλογο **Backtest** εκθέτει τις ρυθμίσεις backtest του cTrader Console με δυνατότητα χρήστη, έτσι δεν χρειάζεται ποτέ να αγγίξετε μια γραμμή εντολών:

- **Symbol / Timeframe** — το timeframe είναι ένα **αναπτυσσόμενο κάθε cTrader period** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, και τα Renko/Range/Heikin periods), στη κανονική casing της κονσόλας, ώστε να επιλέγετε πάντα μια έγκυρη `--period`.
- **From / To** — το παράθυρο backtest (`--start` / `--end`).
- **Data mode** — ένα από τα τρία cTrader modes (`--data-mode`): **Tick data** (`tick`, accurate), **m1 bars** (`m1`, fast), ή **Open prices only** (`open`, fastest).
- **Starting balance** — προεπιλέγεται σε `10000` (`--balance`). Ένα **0 balance δεν τοποθετεί ποτέ trades και κάνει το cTrader να εκπέμψει ένα άδειο report το οποίο έπειτα κρασάρει** ("Message expected"), έτσι ένα μη-μηδενικό balance αποστέλλεται πάντα.
- **Commission** — `--commission`.
- **Spread** — `--spread`, ένα **numeric field σε pips που δεν μπορεί να πάει κάτω από 0**. Είναι **κρυμμένο στο Tick data mode**, όπου το cTrader προέρχεται το spread από τα δεδομένα tick (χωρίς `--spread` που αποστέλλεται).

Ο κατάλογος δεδομένων (`--data-file` / `--data-dir`) διαχειρίζεται από το ίδιο το app (μια cache ανά λογαριασμό, δείτε παραπάνω), δεν εκτίθεται στο διάλογο.

:::note Το cTrader κρασάρει σε ένα άδειο backtest
Αν ένα backtest δεν παράγει **κανένα αποτέλεσμα** — χωρίς trades, ή χωρίς δεδομένα αγοράς για τις επιλεγμένες ημερομηνίες/σύμβολο — ο δικός του report writer του cTrader Console ρίχνει `Message expected` και εξέρχεται χωρίς report. Το app δεν μπορεί να διορθώσει ότι upstream bug, αλλά το ανιχνεύει και σημαδεύει την instance **Failed** με μια actionable αιτία ("no backtest results for the selected range…") αντί ενός raw stack trace. Επιλέξτε ένα ευρύτερο εύρος ημερομηνιών που έχει διαθέσιμα δεδομένα αγοράς και ξαναδοκιμάστε.
:::

## Σελίδα λεπτομερειών Instance

Το άνοιγμα ενός instance (`/instance/{id}`) δείχνει την live status, logs και — για ένα backtest — την καμπύλη equity. Ο **τίτλος της καρτέλας πρόγραμμα περιήγησης** αντικατοπτρίζει την συγκεκριμένη instance (**όνομα cBot · είδος · σύμβολο**, π.χ. `TrendBot · Backtest · EURUSD`) έτσι μια live-run καρτέλα και μια backtest καρτέλα είναι διακριτά σε ένα πρώτο βλέμμα. Μια εκτέλεση και ένα backtest του ίδιου cBot ιχνηλατούνται ως ξεχωριστά **lineages** (μια σταθερή lineage id που μεταφέρεται σε μεταβάσεις state), έτσι η σελίδα ακολουθεί ακριβώς μια instance και ποτέ δεν αναμειγνύει δεδομένα εκτέλεσης με δεδομένα backtest.

## Ελέγχοι κύκλου ζωής Instance

Κάθε σειρά instance (και η σελίδα λεπτομερειών της) έχει state-correct ελέγχους. Μια **ενεργή** instance εμφανίζει **Stop**· μια **terminal** (Stopped / Completed / Failed) εμφανίζει **Start (▶)** για να την επανεκκινήσει με τον ίδιο cBot, λογαριασμό, σύμβολο, timeframe, ParamSet και εικόνα (μια εκτέλεση επανεκκινείται ως εκτέλεση, ένα backtest ως backtest). Το κλικ Stop εμφανίζει μια ειδοποίηση "Stopping…" και απενεργοποιεί το εικονίδιο μέχρι να επιλυθεί, και ένα νέο δημιουργημένο run εμφανίζεται αμέσως στη λίστα — χωρίς ανανέωση σελίδας.

Console logs είναι **persisted όταν ένα instance τερματίζεται** — για μια εκτέλεση (στο Stop) και για ένα **backtest** (στην ολοκλήρωση) — έτσι τα logs της τελευταίας εκτέλεσης παραμένουν viewable στη σελίδα λεπτομερειών και, μέσω της γραμμής εργαλείων log, **αντιγράφονται στο πρόχειρο** (Copy logs icon) ή **κατεβάζονται** (Download logs icon) ακόμη και αφού ο container είναι χάσιμος. Και οι δύο δρουν στο πλήρες console log της instance, όχι μόνο στην on-screen tail.

Ένα **completed backtest** επίσης persists το **cTrader report** και στις δύο μορφές — το raw **JSON** (το ίδιο που διαβάζουν η equity curve και η AI analysis) και το πλήρες **HTML** report. Και τα δύο είναι downloadable από το backtest row **και** τη σελίδα λεπτομερειών μέσω ειδικών εικονιδίων. Μόνο τα **reports της τελευταίας εκτέλεσης** διατηρούνται, και τα εικονίδια είναι **απενεργοποιημένα** για οποιοδήποτε backtest που δεν έχει ξεκινήσει, τρέχει ή failed (και δεν εμφανίζονται ποτέ για μια instance εκτέλεσης) — μόνο ένα ολοκληρωμένο backtest έχει ένα report για κατέβασμα.

Ένα **uploaded** `.algo` δεν κτίστηκε ποτέ εδώ, οπότε η στήλη **Last Build** του στη σελίδα cBots αφήνεται κενή (εμφανίζει χρόνο δημιουργίας μόνο για cBots που δημιουργείτε στο πρόγραμμα περιήγησης).

## Επεξεργασία & επανεκτέλεση ενός σταματημένου instance

Ένα **σταματημένο** instance (εκτέλεση ή backtest) έχει ένα **Edit** έλεγχο — ένα εικονίδιο στη σειρά του στη λίστα **και** δίπλα Start/Stop στη σελίδα λεπτομερειών του — που ανοίγει ένα διάλογο **προϋπάρχον** με την τρέχουσα διαμόρφωσή του. Μπορείτε να αλλάξετε το **λογαριασμό διαπραγμάτευσης, σύμβολο, timeframe, ParamSet και image tag** (και, για ένα backtest, το **παράθυρο και όλες τις ρυθμίσεις backtest** παραπάνω), έπειτα το **Save & start** επανεκκινεί το με τις νέες ρυθμίσεις (αντικαθιστώντας το σταματημένο instance). Ο έλεγχος είναι **απενεργοποιημένος ενώ η instance είναι ενεργή** — μόνο ένα σταματημένο instance μπορεί να επεξεργαστεί.

## Εκτέλεση από τον επεξεργαστή κώδικα

Το κλικ **Run** στον επεξεργαστή κώδικα ανοίγει ένα διάλογο αντί να εκκινήσει μια τυφλή, hard-coded εκτέλεση:

- **Trading account** (απαραίτητο) — ο λογαριασμός cTrader στον οποίο συνδέεται το cBot.
- **ParamSet** (προαιρετικό) — επιλέξτε ένα υπάρχον set, ή αφήστε το κενό για εκτέλεση με τις **προεπιλεγμένες τιμές παραμέτρων** του cBot. Ένα **+** κουμπί δίπλα στο selector δημιουργεί ένα νέο parameter set inline (δείτε παρακάτω) και το επιλέγει.
- Το **Symbol / Timeframe** προεπιλέγεται σε `EURUSD` / `h1` και μπορεί να αλλαχθεί· **Cancel** ή **Run**.

Στο **Run** ο επεξεργαστής αποθηκεύει + δημιουργεί τον τρέχον κώδικα, ξεκινά την instance στο επιλεγμένο λογαριασμό με τις επιλεγμένες παραμέτρους, έπειτα tails τα live container logs. (Η ροή log προωθεί το cookie auth του συνδεδεμένου χρήστη στο `/hubs/logs` SignalR hub, έτσι συνδέεται αντί να αποτυγχάνει με `Invalid negotiation response received`.)

## Σύνολα Παραμέτρων

Ένα **parameter set** είναι ένα ονοματισμένο, επαναχρησιμοποιήσιμο σύνολο cBot parameter overrides αποθηκευμένο ως ένα flat JSON object αντιστοίχισης κάθε ονόματος παραμέτρου σε ένα scalar value, π.χ. `{"Period": 14, "Label": "trend"}`. Στο run/backtest time μετατρέπεται στο αρχείο cTrader `params.cbotset` (`{ "Parameters": { … } }`). Μπορείτε να δημιουργήσετε/επεξεργαστείτε ένα set ως raw JSON από το διάλογο **Parameter sets** του cBot ή inline από το Run διάλογο.

Κάθε parameter set **ανήκει σε ένα cBot**: το διάλογο New Parameter Set παραθέτει όλα τα cBots σας και **πρέπει να επιλέξετε ένα** — η δημιουργία είναι αποκλεισμένη έως ότου επιλεγεί ένα cBot. Το **όνομα ενός set είναι μοναδικό ανά cBot**: η δημιουργία ή μετονομασία ενός set σε ένα όνομα που ένα άλλο set του ίδιου cBot ήδη χρησιμοποιεί απορρίπτεται (ένα σαφές σφάλμα στο διάλογο, `409 Conflict` στο API). Το ίδιο όνομα μπορεί να επαναχρησιμοποιηθεί σε ένα **διαφορετικό** cBot.

Το JSON είναι **validated** στην αποθήκευση: πρέπει να είναι ένα μόνο flat object του οποίου οι τιμές είναι όλες scalars (string / number / bool). Μια non-object root, ένας πίνακας, ένα ένθετο object, μια `null` τιμή, ή κακό JSON απορρίπτεται (ένα σαφές σφάλμα στο διάλογο, `400 Bad Request` στο API). Ένα άδειο object `{}` είναι επιτρεπτό και σημαίνει "no overrides".

## Σημειώσεις cTrader Console CLI

Τα backtests χρειάζονται `--data-mode` (προεπιλέγεται `m1`), ημερομηνίες ως `dd/MM/yyyy HH:mm`, και `params.cbotset` JSON positional arg· το `run` απορρίπτει `--data-dir` (backtest-only). Δείτε `ContainerCommandHelpers`.

## Κόμβοι & κλίμακα

Η χωρητικότητα εκτέλεσης κλιμακώνεται με την προσθήκη Node agents (self-register + heartbeat). Δείτε [node discovery](../operations/node-discovery.md) και [scaling](../deployment/scaling.md).

## Απαιτείται ένας λογαριασμός διαπραγμάτευσης

Η εκτέλεση ή backtesting ενός cBot χρειάζεται ένα λογαριασμό διαπραγμάτευσης cTrader για σύνδεση. Έως ότου προσθέσετε ένα κάτω από **Trading accounts**, το **Run New cBot** / **Backtest New cBot** κουμπιά είναι απενεργοποιημένα (με μια tooltip) και η σελίδα εμφανίζει μια προτροπή που σχετίζεται με ρύθμιση λογαριασμού — δεν χτυπάτε πλέον ένα raw `stream connect failed` σφάλμα από ένα bot χωρίς λογαριασμό.
