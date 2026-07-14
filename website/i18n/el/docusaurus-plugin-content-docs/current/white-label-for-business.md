---
slug: /white-label-for-business
title: White-label για επιχειρήσεις
description: Αποστείλετε το cMind ως το δικό σας branded προϊόν — για prop firms, brokers και επιχειρήσεις copy-trading. Επανατοποθετήστε κάθε επιφάνεια μέσω config, χωρίς αλλαγές κώδικα.
sidebar_position: 4
---

# White-label cMind για την επιχείρησή σας

Διαχειρίζεστε prop firm, desk broker, ή υπηρεσία copy-trading; Το cMind χτίστηκε από την πρώτη μέρα για να είναι
**με revendeur ως δικό σας προϊόν**. Κάθε επιφάνεια — το όνομα, το λογότυπο, το favicon, τα χρώματα, ακόμα
και η εγκαταστάσιμη εφαρμογή τηλεφώνου — κάμπτεται στο brand σας. Οι πελάτες σας βλέπουν *την δική σας* εταιρεία. Χωρίς αλλαγές κώδικα,
χωρίς fork, μόνο config.

:::tip[TL;DR]
Στοχεύστε το `App:Branding` στο όνομά σας, τα χρώματα και το λογότυπό σας. Επανεκκινήστε. Εγινε. Η πλήρης τεχνική αναφορά ζει
στο [White-label feature doc](./features/white-label.md).
:::

## Τι μπορείτε να επανατοποθετήσετε

| Επιφάνεια | Τι αλλάζει |
|---|---|
| **Όνομα προϊόντος** | Κείμενο app bar + τίτλος καρτέλας browser |
| **Λογότυπο & favicon** | Τα σήματά σας παντού, συμπεριλαμβανομένης της καρτέλας browser |
| **Χρώματα** | Πλήρης παλέτα — primary, surfaces, status colors — διαπερνά όλο το UI *και* το CSS της εφαρμογής μέσω design tokens |
| **Εγκαταστάσιμη εφαρμογή (PWA)** | Το όνομα add-to-home-screen, το εικονίδιο και το splash χρησιμοποιούν το brand σας |
| **Meta / SEO** | Η περιγραφή και η URL υποστήριξης είναι δικές σας |
| **Custom CSS** | Εισάγετε το δικό σας φινίρισμα για το τελευταίο 5% |

Ολα προεπιλέγονται στην ταυτότητα stock cMind, οπότε αντικαθιστάτε μόνο ό,τι σας ενδιαφέρει.

## Το rebrand των 60 δευτερολέπτων

Ορίστε αυτά στο deployment σας (JSON config ή μεταβλητές περιβάλλοντος):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Μορφή μεταβλητής περιβάλλοντος: `App__Branding__ProductName=AcmeFX`. Τα χρώματα επικυρώνονται κατά την εκκίνηση —
μια κακή τιμή hex αποτυγχάνει το boot με ένα σαφές μήνυμα αντί να αποδίδει μια χαλασμένη σελίδα. Ωραία και
δυνατά, ακριβώς όταν το θέλετε.

## Το link "Powered by cMind"

Από **προεπιλογή**, το dashboard εμφανίζει ένα μικρό, κομψό link **"Powered by cMind"** που
οδηγεί τους επισκέπτες πίσω σε αυτό το site. Είναι ενεργοποιημένο εξ ορισμού επειδή είμαστε περήφανοι για το project και
βοηθά άλλους traders να το βρουν — αλλά είναι **δική σας απόφαση**.

- **Κρατήστε το** (προεπιλογή): ένα λεπτό link πίστωσης στο dashboard. Δεν σας κοστίζει τίποτα, βοηθά το project.
- **Αποκρύψτε το**: ορίστε `App__Branding__ShowSiteLink=false` και εξαφανίζεται τελείως — τέλειο για ένα
  πλήρως white-labeled deployment όπου το προϊόν είναι αναμφισβήτητα *δικό σας*.

Δείτε το [White-label feature doc](./features/white-label.md#powered-by-link) για το ακριβώς πού αποδίδεται.

## Multi-tenant, per-customer branding

Επειδή το branding είναι απλώς deployment config, κάθε tenant deployment μπορεί να φέρει τη δική του ταυτότητα. Εκτελέστε
μια ξεχωριστή instance ανά πελάτη, ή οδηγήστε το branding από το δικό σας control plane — η εφαρμογή το διαβάζει από
`IOptionsMonitor`, οπότε μπορεί ακόμα να ξαναχτίσει το theme ζωντανά όταν αλλάζουν οι επιλογές.

Συνδυάστε το με:

- **[Feature toggles](./features/feature-toggles.md)** — αποφασίστε ποιες δυνατότητες βλέπει κάθε tenant.
- **[Prop-firm rules](./features/prop-firm.md)** — επιβάλετε τους κανόνες challenge σας με live equity tracking.
- **[Performance fees](./features/copy-performance-fees.md)** + **[provider marketplace](./features/copy-provider-marketplace.md)** — μονετοποιήστε το copy trading.
- **[Compliance](./features/compliance.md)** — διατηρήστε το αρχείο ελέγχου που θα ζητήσει ο ρυθμιστής σας.

## Assets & hosting

Τοποθετήστε το λογότυπο/favicon στο `wwwroot/branding/` της Web εφαρμογής (ή στοχεύστε το `LogoUrl`/`FaviconUrl`
σε οποιαδήποτε absolute URL). Deploy όπως σας ταιριάζει — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), ή
[AWS](./deployment/cloud-aws.md).

Ετοιμοι να το κάνετε δικό σας; Ξεκινήστε με την [τεχνική αναφορά white-label →](./features/white-label.md)