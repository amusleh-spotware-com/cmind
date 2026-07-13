---
description: "Backup & disaster recovery για το cMind — η βάση δεδομένων κρατά λογαριασμούς συναλλαγών, copy profiles, prop-firm challenges, audit chains, και το Data Protection key ring. Η απώλεια σημαίνει απώλεια χρημάτων και παραβίαση κανονιστικών/ελεγκτικών υποχρεώσεων."
---

# Backup & disaster recovery

Αυτή είναι μια εφαρμογή trading/χρηματοοικονομική: η βάση δεδομένων κρατάει λογαριασμούς
συναλλαγών, copy profiles, prop-firm challenges, audit chains, και το Data Protection key ring.
Η απώλειά της σημαίνει απώλεια χρημάτων και σπάει τις κανονιστικές/ελεγκτικές υποχρεώσεις.
Κρατήστε αντίγραφα, και **αποδείξτε ότι η επαναφορά λειτουργεί**.

## Στόχοι

| Μετρική | Στόχος | Σημασία |
|--------|--------|---------|
| RPO (max data loss) | ≤ 5 min | Χρήση point-in-time recovery (continuous WAL), όχι μόνο nightly dumps. |
| RTO (max downtime) | ≤ 1 h | Χρόνος για επαναφορά + επανασύνδεση της εφαρμογής στην επαναφερθείσα βάση. |
| Backup retention | ≥ 35 days | Καλύπτει μια αργά-ανακαλυφθείσα καταστροφή + μηνιαία ελεγκτικά παράθυρα. |
| Restore drill | monthly | Ένα untested backup δεν είναι backup. |

## Τι πρέπει να αντιγραφεί

1. **Η Postgres βάση δεδομένων** — όλα τα δεδομένα της εφαρμογής (single logical database `appdb`).
2. **Το Data Protection key ring** — επιμένει **μέσα** στη βάση δεδομένων
   (`PersistKeysToDbContext<DataContext>`) και PFX-encrypted μέσω `App:DataProtectionCertBase64`.
   Πηγαίνει μαζί στο DB backup, **αλλά το protecting certificate + ο κωδικός του
   (`App:DataProtectionCertPassword`) είναι secrets αποθηκευμένα εκτός της DB** —
   αντιγράψτε τα στον secrets manager σας. Χωρίς το πιστοποιητικό δεν μπορείτε να
   αποκρυπτογραφήσετε secrets (cTID passwords, Open API tokens, node secrets, AI key) μετά
   από επαναφορά.

## Managed Postgres (συνιστάται)

Και οι δύο cloud IaC διαδρόμοι προμηθεύουν managed Postgres με ενσωματωμένο PITR — ενεργοποιήστε
και επαληθεύστε retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): θέστε
  `backup.backupRetentionDays` (≥ 35) και `geoRedundantBackup` όπου η συμμόρφωση το απαιτεί.
  Επαναφορά με *Point-in-time restore* σε νέο server, μετά ενημερώστε το connection string της
  εφαρμογής `appdb`.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): θέστε `backup_retention_period` (≥ 35) και
  `backup_window`· κρατήστε automated backups + προαιρετικό cross-region copy. Επαναφορά με
  *RestoreDBInstanceToPointInTime*, μετά repoint την εφαρμογή.

Το managed PITR δίνει το ≤ 5 min RPO χωρίς αλλαγές στην εφαρμογή — η εφαρμογή χρειάζεται μόνο
το νέο connection string (και η υπάρχουσα retrying execution strategy, βλ.
[scaling.md](../deployment/scaling.md), ανέχεται το cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** ενεργοποιήστε WAL archiving (`archive_mode=on`,
  `archive_command` σε object storage) + ένα περιοδικό `pg_basebackup`. Restore = επαναφορά
  base backup + replay WAL στον target χρόνο. Αυτό είναι που πληροί το RPO target.
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` σε off-box storage για portability /
  partial restores. Δεν αρκεί μόνο του για το RPO target.
- Κρυπτογραφήστε backups at rest· αποθηκεύστε εκτός του database host.

## Restore drill (εκτελέστε monthly)

1. Επαναφέρετε το τελευταίο backup (PITR σε "now − 10 min") σε μια **scratch** βάση, όχι production.
2. Στοχεύστε μια throwaway εφαρμογή (ή μια psql session) σε αυτήν.
3. Επαληθεύστε schema: `dotnet ef migrations list` δείχνει κανένα pending migration, η
   εφαρμογή ξεκινά και γίνεται `/health`-ready.
4. **Επαληθεύστε ότι η audit chain** είναι άθικτη και αδιάρρηκτη μέσω `IAuditTrailVerifier`
   (η tamper-evident `AuditChainInterceptor` chain) — μια σπασμένη chain μετά από επαναφορά
   σημαίνει καταστροφή ή παραβίαση.
5. Επιβεβαιώστε ότι η αποκρυπτογράφηση secrets λειτουργεί (π.χ. μια Open API εξουσιοδότηση
   αποκρυπτογραφεί) — αποδεικνύει ότι το Data Protection cert + password
   επαναφέρθηκαν σωστά.
6. Καταγράψτε το αποτέλεσμα του drill (χρόνος vs RTO) και καταστρέψτε τη scratch βάση.

Αυτοματοποιήστε τα βήματα 1–4 στο CI όπου το επιτρέπει το περιβάλλον (επαναφορά ενός seeded
backup σε Testcontainer, εκτέλεση `dotnet ef migrations list` + audit-chain verify) ώστε μια
regression broken-backup να πιάνεται πριν τη χρειαστείτε.

## Μετά από μια πραγματική επαναφορά

1. Επαναφέρετε τη βάση (PITR σε λίγο πριν το συμβάν).
2. Εξασφαλίστε ότι το Data Protection cert + password είναι **τα ίδια** που
   χρησιμοποιούνταν πριν το συμβάν.
3. Repoint το `appdb` connection string της εφαρμογής· κυλήστε τα replicas.
4. Η εκκίνηση τρέχει migrations under the advisory lock (βλ. scaling.md) — ασφαλές με N replicas.
5. Οι copy/prop-firm supervisors διεκδικούν τα μισθώματά τους και **resync από τον broker**
   (ο cTrader είναι η πηγή αλήθειας), οπότε οι ανοιχτές θέσεις συγκλίνουν αυτόματα — τίποτα
   δεν εμπιστεύεται από stale τοπική κατάσταση.
6. Επαληθεύστε audit chain + spot-check πρόσφατα δεδομένα συναλλαγών.
