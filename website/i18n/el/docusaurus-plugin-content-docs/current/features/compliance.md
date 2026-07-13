---
description: "Τα Retail FX/CFD/crypto brokerage φέρουν νομικές και τηρητέες υποχρεώσεις τήρησης αρχείων. Το module υλοποιεί τέσσερις βιομηχανικά τυπικούς πυλώνες: συγκατάθεση αποκάλυψης κινδύνου, αμετάβλητο αρχείο ελέγχου, τήρηση αρχείων στο στυλ MiFID/ESMA, δικαιώματα δεδομένων GDPR…"
---

# Νομική συμμόρφωση

Τα Retail FX/CFD/crypto brokerage φέρουν νομικές και τηρητέες υποχρεώσεις τήρησης αρχείων. Το module υλοποιεί τέσσερις βιομηχανικά τυπικούς πυλώνες: **συγκατάθεση αποκάλυψης κινδύνου**, **αμετάβλητο αρχείο ελέγχου (tamper-evident audit trail)**, **τηρητέα αρχεία στο στυλ MiFID/ESMA**, **δικαιώματα δεδομένων GDPR**. Ολα ελέγχονται από το feature flag `Compliance`.

## 1. Νομικά έγγραφα με εκδόσεις + συγκατάθεση

- `LegalDocument` (aggregate) — Ενια Service Terms με εκδόσεις, CFD **Risk Disclosure**, ή Privacy Policy.
  Το draft συντάσσεται και μετά **δημοσιεύεται**; οι δημοσιευμένες εκδόσεις είναι **αμετάβλητες** (edit πετάει εξαίρεση), ώστε το ακριβές κείμενο που συμφώνησε ο χρήστης να είναι πάντα ανακτήσιμο. Το ενεργό έγγραφο για έναν τύπο είναι η υψηλότερη δημοσιευμένη έκδοσή του.
- `ConsentRecord` (aggregate) — αμετάβλητη εγγραφή ότι ο χρήστης αποδέχθηκε συγκεκριμένη έκδοση εγγράφου σε μια χρονική στιγμή, με την προέλευση IP.
- **Επιβολή:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` αποκλείει την ενέργεια με `403`
  όταν υπάρχει δημοσιευμένο έγγραφο αυτού του τύπου και ο χρήστης δεν έχει συναινέσει στην ενεργή έκδοσή του. Εφαρμόζεται στη
  **δημιουργία copy-profile** (`RiskDisclosure`). Αν δεν υπάρχει τίποτα δημοσιευμένο → οι ενέργειες επιτρέπονται — δεν υπάρχει τίποτα να συναινέσει κανείς ακόμα — ώστε η ενεργοποίηση του module να μην αποκλείει τίποτα αναδρομικά μέχρι να δημοσιευθεί πραγματικά η αποκάλυψη κινδύνου.

## 2. Αμετάβλητο αρχείο ελέγχου (tamper-evident audit trail)

Οι εγγραφές `AuditLog` είναι συνδεδεμένες με hash: κάθε γραμμή αποθηκεύει `PrevHash` και `Hash = SHA-256(prev | canonical fields)`.
Το `AuditChainInterceptor` εφαρμόζει την αλυσίδα διαφανώς κατά το `SaveChanges`, ώστε τα υπάρχοντα σημεία κλήσης ελέγχου να παραμένουν αμετάβλητα.
Το `IAuditTrailVerifier.VerifyAsync` επαναδιατρέχει την αλυσίδα και αναφέρει την πρώτη γραμμή της οποίας το αποθηκευμένο hash ή back-link δεν αντιστοιχεί πια — εντοπίζει οποιαδήποτε επεξεργασία ή διαγραφή παλαιότερης εγγραφής. Endpoint ιδιοκτήτη: `GET /api/compliance/audit/verify`.

## 3. Τήρηση αρχείων (MiFID II / ESMA RTS)

Η τήρηση αρχείων ικανοποιείται από το **αμετάβλητο, κατακερματισμένο αρχείο ελέγχου** συν τα **διατηρημένα αρχεία συγκατάθεσης** και
τα soft-deleted (ποτέ hard-deleted) domain records. Τα UTC timestamps προέρχονται από το injected `TimeProvider`. Τα αρχεία συγκατάθεσης κρατούν την έκδοση εγγράφου + IP; τα δημοσιευμένα νομικά έγγραφα δεν μεταλλάσσονται ποτέ. Διατήρηση = μη διαγραφή αυτών των πινάκων (append-only / soft-delete).

## 4. Δικαιώματα δεδομένων GDPR

- `GET /api/compliance/export` — μηχαναγνώσιμη εξαγωγή των δεδομένων του καλούντος (προφίλ, συγκαταθέσεις, copy profiles, prop-firm challenges).
- `POST /api/compliance/erase` — δικαίωμα διαγραφής: `AppUser.Anonymize()` καθαρίζει τα PII (email, MFA) και η γραμμή
  γίνεται soft-delete, διατηρώντας τη συνοχή του ιστορικού αναφορών/ελέγχου.

## Σύνοψη API

| Μέθοδος | Διαδρομή | Ρόλος | Σκοπός |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | ενεργά δημοσιευμένα έγγραφα |
| GET | `/api/compliance/consent/status` | User+ | ποιες συγκαταθέσεις εκκρεμούν |
| POST | `/api/compliance/consent` | User+ | αποδοχή της ενεργής έκδοσης ενός εγγράφου |
| GET | `/api/compliance/export` | User+ | εξαγωγή δεδομένων GDPR |
| POST | `/api/compliance/erase` | User+ | διαγραφή GDPR του δικού σας λογαριασμού |
| POST | `/api/compliance/documents` | Owner | σύνταξη ενός εγγράφου |
| POST | `/api/compliance/documents/{id}/publish` | Owner | δημοσίευση μιας έκδοσης |
| GET | `/api/compliance/audit/verify` | Owner | επαλήθευση της αλυσίδας hash του ελέγχου |

UI: `/settings/legal` (πλοήγηση *Settings → Legal & Privacy*, gated από `Compliance`) εμφανίζει εκκρεμή συμφωνητικά με κουμπιά αποδοχής + ενέργειες εξαγωγής/διαγραφής GDPR.

## Δοκιμές

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draft/publish/immutability, capture συγκατάθεσης),
  `AuditChainTests.cs` (hash links, εντοπισμός παραποίησης, ευαισθησία περιεχομένου).
- **Integration** — `IntegrationTests/CompliancePersistenceTests.cs` (queries ενεργής έκδοσης + συγκατάθεσης σε πραγματικό
  Postgres), `AuditChainIntegrityTests.cs` (η αλυσίδα επαληθεύεται άθικτη, τότε εντοπίζει SQL-level παραποίηση),
  `ComplianceFlowTests.cs` (WebApplicationFactory, απομονωμένη DB: η πύλη συγκατάθεσης αποκλείει τη δημιουργία copy μέχρι να γίνει αποδεκτή η αποκάλυψη κινδύνου· εξαγωγή GDPR· επαλήθευση ελέγχου).
- **E2E** — `E2ETests/ComplianceTests.cs`: Η σελίδα Legal & Privacy αποδίδεται και η εξαγωγή GDPR επιστρέφει τα δεδομένα του χρήστη σε πραγματικό browser.