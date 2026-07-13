---
description: "Όλα τα credentials που χρειάζονται τα test suites βρίσκονται σε ένα μόνο gitignored αρχείο: secrets/dev-credentials.local.json. Αντιγράψτε το committed template και συμπληρώστε ό,τι έχετε — κάθε τιμή είναι προαιρετικό και τα tests που χρειάζονται ένα missing value παραλείπονται gracefully."
---

# Dev credentials — ένα αρχείο για κάθε test

Όλα τα credentials που χρειάζονται τα test suites βρίσκονται σε ένα μόνο gitignored αρχείο:
`secrets/dev-credentials.local.json`. Αντιγράψτε το committed template και συμπληρώστε ό,τι
έχετε — κάθε τιμή είναι προαιρετικό και τα tests που χρειάζονται ένα missing value
παραλείπονται gracefully.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## Τι διαβάζει κάθε test tier

| Tier | Χρειάζεται | Από |
|------|------------|-----|
| **Unit** (`tests/UnitTests`) | τίποτα | — deterministic, χωρίς secrets, χωρίς δίκτυο |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | ένα cID login + ένας **demo** account number | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI features** | Anthropic key | `Ai.ApiKey` (unset ⇒ AI features return disabled, η app εξακολουθεί να τρέχει) |

## Schema

Δείτε `dev-credentials.example.json` στο repo root. Sections:

- `OpenApi.App` — `{ ClientId, ClientSecret }` της cTrader Open API εφαρμογής.
- `OpenApi.Cids` — cTrader ID logins που χρησιμοποιούνται από το headless OAuth onboarding.
  Κάθε entry επίσης φέρει ένα **`Accounts`** array — τα cTrader trading-account numbers
  (το login/account number, π.χ. `3635817`) κάτω από εκείνο το cID που η test
  infrastructure επιτρέπεται να συνδέσει στην app και να οδηγήσει. Το
  `CBotRealRunBacktestTests` διαβάζει το πρώτο entry που έχει non-empty `Accounts` array,
  προσθέτει εκείνο το cID + account στην app, μετά πραγματικά εκτελεί και backtest ένα cBot
  σε αυτό. **Βάλτε μόνο demo account numbers εδώ** — ποτέ live account· τα run/backtest
  tests τοποθετούν πραγματικές εντολές σε ό,τι account λίσταρετε. Empty/omitted
  `Accounts` ⇒ το real run/backtest test παραλείπεται gracefully.
- `OpenApi.Tokens` — το multi-cID token cache (μία entry ανά authorized cID με το
  refresh/access token + account list). Γράφεται αυτόματα από onboarding και από το
  token-refresh step· σπάνια επεξεργάζεται με το χέρι.
- `Owner` — seed owner login για την app under E2E.
- `Database.ConnectionString` — μόνο όταν τα tests στοχεύουν σε external Postgres
  αντί για Testcontainers.
- `Ai.ApiKey` — Anthropic API key για τα AI features.

## Προτεραιότητα

1. **Περιβαλλοντικές μεταβλητές** override όλα (π.χ. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — το ενοποιημένο αρχείο (προτιμάται).
3. **Legacy split files** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` εξακολουθούν να διαβάζονται όταν το ενοποιημένο αρχείο
   απουσιάζει, οπότε τα υπάρχοντα machines συνεχίζουν να δουλεύουν. Τα νέα setups πρέπει
   να χρησιμοποιούν το single file.

## Ασφάλεια

- `secrets/` και `*.local.json` είναι gitignored — τίποτα εδώ δεν commitάρεται ποτέ.
- Τα live copy tests αρνούνται να τρέξουν ενάντια σε non-demo accounts (τα `IsLive`
  accounts φιλτράρονται από `LiveCopyFixture`). Κρατήστε μόνο demo accounts στο token cache.
- In-cluster (Kubernetes) runs mount το αρχείο ως read-only Secret· τα token refreshes
  κρατούνται in memory και το read-only write-back είναι silent no-op.
