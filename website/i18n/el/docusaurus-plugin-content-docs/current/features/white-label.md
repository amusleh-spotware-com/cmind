---
description: "Reseller rebrand εφαρμογή — προϊόν όνομα, λογότυπο, favicon, χρώματα, προσαρμοσμένα CSS — μέσα ανάπτυξης ρύθμιση, χωρίς κώδικα αλλαγή. Κάθε κοσμήματος αξία **προεπιλογή σε αποθέματα…"
---

# Λευκό-ετικέτα κοσμήματος

Reseller rebrand εφαρμογή — προϊόν όνομα, λογότυπο, favicon, χρώματα, προσαρμοσμένα CSS — μέσα ανάπτυξης ρύθμιση, χωρίς κώδικα αλλαγή. Κάθε κοσμήματος αξία **προεπιλογή σε αποθέματα ταυτότητα**: unconfigured ανάπτυξη φαίνεται ίδια όπως προηγουμένως. reseller αντικατάσταση μόνο τι απαιτούν.

## Μοντέλο

- `Core.Options.BrandingOptions` — δεσμευμένο από `App:Branding`. Κείμενο-βασισμένα (ρύθμιση άκρο). κάθε χρώμα επικύρωση όταν θέμα χτίστηκε.
- `Core.Branding.HexColor` — αντικείμενο αξιών για CSS hex χρώμα (`#RGB` / `#RRGGBB`), αμετάβλητο, εαυτό-validating.
  Άκυρο χρώμα ρίχει `DomainException` (`domain.branding.color_invalid`) όταν θέμα χτίστηκε — misconfigured ανάπτυξη αποτυγχάνει γρήγορα σε εκκίνηση, δεν απόδοση σπασμένα παλέτα.
- `Web.Components.Theme.Build(BrandingOptions)` — παράγουν MudBlazor θέμα από κοσμήματος. Μόνο branded παλέτα καταχωρίσεις έρχονται από ρύθμιση. τυπογραφία, διάταξη, ουδέτερα επιφάνεια τόνοι παραμένει σταθερό έτσι προϊόν κρατήστε συνεκτικό φαίνεται διαμέσου resellers.
- `Web.Branding.IBrandingThemeProvider` — singleton, κτίριο θέμα μία φορά, κτίριο πάνω εναλλαγή επιλογή.
  Εγχυμένο από `MainLayout`/`EmptyLayout` για `MudThemeProvider`, κατά εφαρμογή μπάρα για προϊόν όνομα/λογότυπο. `App.razor` διαβάστε `IOptionsMonitor<AppOptions>` απευθείας για σελίδα `<head>` (τίτλος, περιγραφή, favicon, θέμα-χρώμα, προσαρμοσμένα CSS).

## Ρύθμιση

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — αντιγραφή διαπραγμάτευσης και στρατηγική αυτοματοποίηση.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Περιβάλλον-μεταβλητή μορφή: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Κλειδί | Αποτέλεσμα | Προεπιλογή |
|-----|--------|---------|
| `ProductName` | Εφαρμογή-μπάρα κείμενο + σελίδα `<title>` | `cMind` |
| `LogoUrl` | Εφαρμογή-μπάρα λογότυπο εικόνα. όταν κενό, προϊόν όνομα κείμενο δείχνει | *(κενό)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | αποθέματα περιγραφή |
| `PrimaryColor` / `SecondaryColor` | προσέγγιση, αναδυόμενο εικονίδιο, κουμπιά | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + επιφάνειες. `AppBarColor` drives `<meta θέμα-χρώμα>` + PWA δείλιαση `θέμα_χρώμα`, `BackgroundColor` το δείλιαση `φόντο_χρώμα` | σκοτεινό παλέτα |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | κατάσταση χρώματα | αποθέματα |
| `CustomCss` | εγχυμένο `<style>` σε `<head>` (ανάπτυξη-έμπιστος) | *(κενό)* |
| `ShowSiteLink` | δείχνω το "Powered by cMind" πίστωση σύνδεσμο σε το πίνακας ελέγχου | `true` |
| `RequireMfa` | απαιτούν κάθε χρήστη ρύθμιση δύο-παράγοντας τυλίγματος πριν χρησιμοποιώντας την εφαρμογή | `false` |
| `NodesUi` | πόση του Nodes επιφάνεια κάρπα: `Full` (λίστα + χειροκίνητα προσθήκη/διαγραφή), `Monitor` (διαβάστε-μόνο λίστα, χωρίς προσθήκη/διαγραφή), `Hidden` (χωρίς nav, χωρίς σελίδα, χωρίς χειροκίνητα API) | `Full` |
| `RestrictNodesToOwner` | όταν `true`, μόνο ο ιδιοκτήτης μπορώ να δούν/διαχείριση κόμβι. διαφορετικά ολόκληρος ο διαχειριστής-ή-έξω προσωπικό επιφάνεια μπορώ. Κανονικοί χρήστες ποτέ δείχνω κόμβι κατά τα άλλα | `false` |

Περιουσιακά στοιχεία που αναφέρονται από `LogoUrl`/`FaviconUrl` εξυπηρετούμενο από Web εφαρμογή `wwwroot` (π.χ. όρθιο `wwwroot/branding/` φάκελο) ή κάθε απόλυτο URL.

`App:Branding` επικύρωση κατά την εκκίνηση (`BrandingOptionsValidator`, τρέχουν μέσα `ValidateOnStart`): κάθε χρώμα πρέπει να ισχύει hex, `CustomCss` πρέπει δεν περιέχουν `<`/`>` (δεν μπορώ διακοπή έξω από `<style>` ετικέτα). Misconfigured ανάπτυξη αποτυγχάνει δημιουργική με σαφή μήνυμα, δεν απόδοση σπασμένα σελίδα.

## Δυνάμεις-κατά-σύνδεσμο

Ο πίνακας ελέγχου αποδίδει α μικρό **"Powered by cMind"** πίστωση σύνδεσμο το οποίο δείχνει σε το έργο του
τεκμηρίωση τοποθεσία. Ελέγχεται από `App:Branding:ShowSiteLink` και είναι **`true` κατά προεπιλογή** — ένα
unconfigured ανάπτυξη δείχνει αυτό. Α reseller τρέχοντας ένα πλήρως λευκό-ετικέτα παράδειγμα θέσεις
`App__Branding__ShowSiteLink=false` να κατάργηση αυτό ολοκληρωτικά.

Ο σύνδεσμος εκπέμπεται από το πίνακας ελέγχου συστατικό και διαβάζει την σημαία μέσα `IBrandingThemeProvider` /
`BrandingOptions`, έτσι εναλλαγή αυτό είναι ένα ρύθμιση-μόνο αλλαγή (χωρίς κτίριο). Δείτε
[Λευκό-ετικέτα για επιχείρηση](../white-label-for-business.md#το-powered-by-cmind-σύνδεσμο) για το
επιχείρηση-κοντινό περίληψη.

## Κομίστρα allowlist

Α λευκό-ετικέτα ανάπτυξη μπορώ να περιορίστε ποιο κομίστρες διαπραγμάτευσης λογαριασμούς του χρήστες του μπορώ προσθήκη — έτσι α κομίστρα
τρέχοντας cMind για δικό του κλιέντες μόνο ποτέ εξυπηρετούμενο δικό του βιβλίο. Διαμορφώνεται κάτω από `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Περιβάλλον-μεταβλητή μορφή: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Συμπεριφορά:**

- **Κενό κατάλογο (προεπιλογή) ⇒ unrestricted.** Κάθε κομίστρα είναι επιτρεπτό και **χωρίς επαλήθευση τρέχει** — α
  αποθέματα ανάπτυξη είναι εντελώς αμετάβλητο.
- **Μη-κενό ⇒ περιορισμένο.** cMind έλεγχο κάθε λογαριασμό ένας χρήστης προσπαθεί προσθήκη κατά τον κατάλογο
  (case-ανίδιος):
  - **Open API (OAuth) σύνδεσμο** — ο κομίστρα όνομα είναι ανάφερε αξιόπιστα από cTrader Open API, έτσι
    ένας disallowed λογαριασμό είναι απλά **παραληφθεί** (επιτρεπτό λογαριασμούς σε ίδια δώρο εξακολουθούν σύνδεσμο). η
    εξουσιοδόσης σελίδα λέει ο χρήστης ποιο κομίστρες ήσαν παραληφθεί.
  - **Χειροκίνητα cID (όνομα χρήστη / κωδικός πρόσβασης)** — ο χρήστη-τύπος κομίστρα είναι **δεν** έμπιστος. cMind **επαληθεύει**
    ο λογαριασμό πραγματικό κομίστρα κατά τρέχουν ο παραδίδεται κομίστρα-δοκιμή cBot μέσα cTrader CLI (ανάγνωση
    `Account.BrokerName`) και αποθηκεύει το επαληθευμένο όνομα. Α disallowed κομίστρα απορρίπτεται με ένα
    ειδοποίηση. ένα επαλήθευση αποτυχία (κακή διαπιστευτήρια, χωρίς κόμβο, χρονικό όριο) είναι επιφανείας πολύ, και ο
    λογαριασμό δεν προστίθεται.

**Μοντέλο:**

- `Core.Options.AccountsOptions` — δεσμευμένο από `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — αντικείμενο αξιών (trimmed, case-ανίδιος ισότητα).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`. κενό = επιτρέπουν όλα. Επιβάλλονται ως ένα
  αναλλοίωτο εσωτερικό `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — τρέχουν το δοκιμή κοντέινερ σε το ιστό
  κεντρικό υπολογιστή (ποιο έχει το Docker σόκετ), ουρές καταγραφή, και αναλύει κομίστρα μέσα
  `Core.Accounts.BrokerProbeOutput`. Μόνο εγκαλώ όταν το allowlist περιορισμένο.

**Κομίστρα-δοκιμή cBot:** α προκατασκευή `broker-probe.algo` κάρπα με το ιστό εφαρμογή (`src/Web/BrokerProbe/`,
αντιγράφησαν σε το έξοδο ως `broker-probe/broker-probe.algo`), έτσι ο προεπιλογή
`App:Accounts:BrokerProbeAlgoPath` επιλύει έξω του κιβωτίου — ένα σχετικό διαδρομή επιλύεται κατά τον εφαρμογή
βάση κατάλογο, ένα απόλυτο διαδρομή χρησιμοποιείται ως δοθεί. Ο πηγή ζει σε `tools/broker-probe/`. Όταν το
algo απουσία, χειροκίνητα-cID επαλήθευση αποτυγχάνει κλειστό — λογαριασμούς κάτω ένα περιορισμένο allowlist μπορώ εξακολουθούν
συνδεδεμένη μέσα Open API διαδρομή, ποιο απαιτήσεις χωρίς δοκιμή.

## Κομίστρα allowlist — δοκιμές

- **Ενότητα** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` αντικείμενα αξιών, `BrokerProbeOutput`
  αναλυτής, και το `CTraderIdAccount` allowlist αναλλοίωτο.
- **Ολοκλήρωση** — `IntegrationTests/BrokerAllowlistTests.cs`: χειροκίνητα-cID τελικό σημείο με ένα ψεύτικα verifier
  (unrestricted / επαληθευμένο / disallowed / επαλήθευση-απέτυχε) + Open API linker παλείει disallowed
  λογαριασμούς. `BrokerVerifierLiveTests.cs` τρέχουν το **πραγματικό** δοκιμή όταν cID creds + το algo παρέχονται
  (παραλήψεις καθαρά διαφορετικά).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: ένα περιορισμένο ανάπτυξη απορρίπτει ένα χειροκίνητα προσθήκη μέσα το
  πραγματικό UI και δείχνει ο "δεν μπορώ να επαληθεύσει" ειδοποίηση (χωρίς λογαριασμό σειρά προστέθηκε).

## Κόμβι UI ορατότητα

Κόμβι είναι υποδομή περισσότερα ενοικιαζόμενο ποτέ διαχείριση κατά χέρι — cTrader CLI πράκτορες
[αυτο-εγγραφή και καρδιά χτύπημα](../operations/node-discovery.md), έτσι α λευκό-ετικέτα ανάπτυξη μπορώ αποκρύψει το
χειροκίνητα έλεγχος, ή το Nodes επιφάνεια ολοκληρωτικά, και ακόμη τρέχουν α υγιεινό σύμπλεγμα μέσα αυτο-ανακάλυψη.
Δύο ρύθμιση-μόνο κοσμήματος κλειδιά άρχον αυτό:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Περιβάλλον-μεταβλητή μορφή: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — τρεις τρόποι:**

- **`Full` (προεπιλογή)** — το αποθέματα προϊόν: ο κόμβο κατάλογο συν χειροκίνητα **Νέα κόμβο** και **Διαγραφή**
  έλεγχος. `POST`/`DELETE /api/nodes` εργασία.
- **`Monitor`** — α διαβάστε-μόνο επιφάνεια: ο κατάλογο και ζωντανό στατιστικά παραμένει, αλλά χειροκίνητα προσθήκη και διαγραφή είναι
  αφαιρέθηκε. Κόμβι μόνο ποτέ εμφανίζω μέσα αυτο-ανακάλυψη. `POST`/`DELETE /api/nodes` επιστροφή **404**.
- **`Hidden`** — το Nodes nav σύνδεσμο και σελίδα είναι έξω ολοκληρωτικά και ο σελίδα διαδρομή ανακατευθύνει σε το
  πίνακας ελέγχου. ο χειροκίνητα προσθήκη/διαγραφή API είναι από. Το σύμπλεγμα είναι αυτο-ανακάλυψη μόνο.

**`RestrictNodesToOwner`** όροφος ποιο μπορώ να δούν και διαχείριση κόμβι. Προεπιλογή `false` κρατά το τυπικό
**διαχειριστής-ή-έξω** προσωπικό επιφάνεια (`AdminOrAbove`). θέστε `true` να κάνουν αυτό **ιδιοκτήτης-μόνο** (`Owner`). Κάτω
τρόπο **κανονικοί χρήστες ποτέ δείχνω κόμβι** — αυτό μόνο επιλέγει μεταξύ ιδιοκτήτης-μόνο και το ευρύτερο προσωπικό επιφάνεια.

Κόμβο **αυτο-ανακάλυψη είναι αβλεβής από αμφότερα κλειδιά**: ο ανώνυμος `POST /api/nodes/register` αυτο-εγγραφή
+ καρδιά χτύπημα τελικό σημείο πάντα εργάζεται, έτσι ένα `Hidden`/`Monitor` ανάπτυξη ακόμη αυξάνει το σύμπλεγμα
αυτόματα.

**Μοντέλο:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — το ενιαίο πηγή αλήθειας σύνθεση ο τρόπο + ιδιοκτήτης-περιορισμό:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), σελίδα (`Pages/Nodes.razor`) και το τελικά σημεία (`NodeEndpoints`) όλα διαβάστε αυτό έτσι
  ο UI και API μπορώ ποτέ διαφωνία.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — δεσμευμένο από `App:Branding`.

## Κόμβο UI ορατότητα — δοκιμές

- **Ενότητα** — `UnitTests/Nodes/NodesUiAccessTests.cs`: σελίδα-ορατότητα, χειροκίνητα-διαχείριση και
  απαιτούμενη-πολιτική ανάλυση διαμέσου κάθε τρόπο + προεπιλογή κοσμήματος.
- **Ολοκλήρωση** — `IntegrationTests/NodeUiGatingTests.cs`: πάνω πραγματικό HTTP + Postgres — `Full` επιτρέπει α
  χειροκίνητα προσθήκη, `Monitor`/`Hidden` 404 προσθήκη και διαγραφή, και `RestrictNodesToOwner` απαγορεύει ένα διαχειριστής κατά το
  ιδιοκτήτης ακόμη διαβάζει το κατάλογο.
- **E2E** — `E2ETests/NodesUiTests.cs` (προεπιλογή `Full`: nav σύνδεσμο + σελίδα + Νέα κόμβο κουμπί απόδοση) και
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav σύνδεσμο έξω, `/nodes` ανακατευθύνει).

## Σχέδιο tokens (CSS μεταβλητές)

Κοσμήματος επίσης φτάνει το εφαρμογή **δικό του** stylesheet + προσαρμοσμένα συστατικά, δεν ακριβώς MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` εκπέμπει το branded παλέτα ως CSS προσαρμοσμένα ιδιότητες σε `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), εγχυμένο σε `App.razor` δεξιά μετά `site.css`. `site.css` και κάθε συστατικό διαβάστε `var(--app-*)` — **χωρίς σκληρό-κωδικοποιημένα χρώματα** — έτσι ένα reseller παλέτα ρέει παντού (σύνδεση κύριο, πολυόροφο nav, βοήθειας συμβουλές, offline σελίδα) για δωρεάν. Ουδέτερα επιφάνεια τόνοι προεπιλογή σε `site.css :root`. `CustomCss` (εγχυμένο τελευταία) μπορώ αντικατάσταση κάθε token. Δείτε [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Η εγκαταστάσιμη εφαρμογή είναι branded πολύ — ο δείλιαση τελικό σημείο (`/manifest.webmanifest`) χτίστηκε από `BrandingOptions` (`ProductName` → `όνομα`/`σύντομη_όνομα`, `Description`, `AppBarColor`/`BackgroundColor` → θέμα/φόντο). Δείτε [pwa.md](pwa.md).

## Δοκιμές

- **Ενότητα** — `UnitTests/Branding/HexColorTests.cs`: ισχύει/άκυρο hex επικύρωση.
- **Ολοκλήρωση** — `IntegrationTests/ThemeBuildTests.cs`: χρώματα χάρτης σε παλέτα, άκυρο χρώμα ρίχει.
  `IntegrationTests/BrandingHttpTests.cs`: προσαρμοσμένα `ProductName`/description/θέμα-χρώμα απόδοση σε εξυπηρετούμενο σελίδα `<head>` (WebApplicationFactory + Postgres), προεπιλογή κρατήστε αποθέματα όνομα.
- **E2E** — `E2ETests/BrandingTests.cs`: branded προϊόν όνομα απόδοση σε εφαρμογή μπάρα σε πραγματικό πρόγραμμα περιήγησης.
