---
id: white-label-owner-settings
title: White-label options στα Owner settings
sidebar_label: White-label owner settings
---

# White-label options στα Owner settings

Κάθε white-label option που μια deployment μπορεί να θέσει μέσω configuration (`appsettings`/env) είναι **επίσης
settable κατά runtime από τον app owner**, από **Settings → Deployment**, χωρίς redeploy. Ένα owner
override **κερδίζει πάνω από configuration**; το clearing αυτού επιστρέφει την option στη deployment's configured (ή
built-in default) value.

Αυτό κατοπτρίζει πώς μια white-label *deployment* ρυθμίζει το product — τα ίδια knobs, το ίδιο effect —
ώστε ένας operator μπορεί να tune branding, gates και policy live και να δει αμέσως τα αποτελέσματα.

## Πού ζει

- **UI:** το owner-only **Deployment** section στα settings dialog, και τη deep-linkable page
  **`/settings/deployment`**. Οι Options ομαδοποιούνται σε ένα **tab ανά category** (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop firm), mobile-first, με windowed
  dialog σε desktop και full-screen surface σε phones.
- **API:** `/api/whitelabel` (owner-only, ποτέ feature-gated):
  - `GET /api/whitelabel` — κάθε option με το effective value του, provenance (`Config` / `Owner` /
    `Default`) και αν ένα override ορίστηκε. **Τα Secrets κρύβονται** (value ποτέ δεν επιστρέφεται).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — θέστε override (validated per option kind). Μια blank
    value σε ένα **secret** κρατάει το existing secret.
  - `DELETE /api/whitelabel/{key}` — καθαρίστε ένα override (revert στο config).
  - `POST /api/whitelabel/reset` — καθαρίστε **όλα** τα overrides (revert το deployment σε pure config).

## Πώς τα overrides παίρνουν effect

Τα Owner overrides αποθηκεύονται ως encrypted-where-needed `AppSetting` rows και layered πάνω από το bound
`AppOptions` από ένα decorated `IOptionsMonitor<AppOptions>`. Επειδή κάθε consumer ήδη διαβάζει options
μέσω αυτού του monitor, ένα override εφαρμόζει **live** σε όλη την app — το theme, page title, MFA
gate, AI-provider gates, broker allow-list, registration policy, email transport settings, κλπ. ενημερώνονται
κατά το επόμενο read (το theme/branding re-render αμέσως). Αν η database είναι προσωρινά unavailable το
layer **fails open** στο configured baseline, ώστε ένα override read ποτέ δεν μπορεί να σπάσει την app.

Τα **Feature flags** είναι μέρος της ίδιας surface αλλά persisted μέσω της υπάρχουσας feature-override
store (`IFeatureGate`), ώστε τα Features tab και τα standalone feature toggles ποτέ δεν diverge.

Τα **Secrets** (SMTP password, CAPTCHA secret, provisioning secret) κρυπτογραφούνται at rest
(`ISecretProtector`, purpose `whitelabel.secret`), write-only σε UI, και ποτέ δεν επιστρέφονται από API.

## Delegated options

Τα **shared Open API application** credentials και **per-message-type rate limits** διαχειρίζονται στο
**Open API** settings section (δείτε τα copy-trading / Open API docs). Εμφανίζονται στο Deployment
catalog ως *delegated* entries (read-only εδώ, με link) ώστε τίποτα δεν είναι duplicated και η sync
guarantee ακόμα τα μετρά ως covered.

## Πάντα in sync (enforced)

Προσθήκη ενός νέου white-label option στο configuration **πρέπει** να το surface σε owner settings σε αυτό
commit. Αυτό enforced από `WhiteLabelCatalogParityTests`: αντανακλά πάνω από κάθε white-label
options-record property και fails το build εκτός αν το property registered σε
`Core/WhiteLabel/WhiteLabelCatalog` (ή explicitly listed σε `IntentionallyExcluded` με λόγο).
Δείτε mandate 10 σε `CLAUDE.md`.

## Notes

- Enabling SMTP σε deployment που ξεκίνησε με **χωρίς** email configured χρειάζεται restart (ο sender
  type επιλέγεται κατά startup); host/credentials ενός ήδη-configured sender ενημερώνονται live.
- Οι Option **labels/descriptions** είναι technical config-knob identifiers που εμφανίζονται ως data; τα tab labels και
  όλα interactive chrome είναι πλήρως localized.
