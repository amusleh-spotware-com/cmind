---
description: "Ini adalah trading/financial app: database hold trading account, copy profile, prop-firm challenge, audit chain, dan Data Protection key ring…"
---

# Backup & disaster recovery

Ini adalah trading/financial app: database hold trading account, copy profile, prop-firm challenge, audit chain, dan Data Protection key ring. Kehilangannya kehilangan uang dan break regulatory/audit obligation. Backup, dan **prove restore bekerja**.

## Target

| Metric | Target | Meaning |
|--------|--------|---------|
| RPO (max data loss) | ≤ 5 min | Gunakan point-in-time recovery (continuous WAL), bukan hanya nightly dump. |
| RTO (max downtime) | ≤ 1 h | Waktu untuk restore + re-point app ke restored database. |
| Backup retention | ≥ 35 hari | Cover late-discovered corruption + monthly audit window. |
| Restore drill | bulanan | Untested backup bukan backup. |

## Apa yang harus di-backup

1. **Database Postgres** — semua app data (single logical database `appdb`).
2. **Data Protection key ring** — persisted **di** database (`PersistKeysToDbContext<DataContext>`) dan PFX-encrypted via `App:DataProtectionCertBase64`. Itu ride bersama dalam DB backup, **tetapi protecting certificate + password-nya (`App:DataProtectionCertPassword`) adalah secret disimpan di luar DB** — backup dalam secrets manager Anda. Tanpa cert Anda tidak dapat decrypt secret (cTID password, Open API token, node secret, AI key) setelah restore.

## Managed Postgres (recommended)

Kedua cloud IaC path provision managed Postgres dengan built-in PITR — enable + verify retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): set `backup.backupRetentionDays` (≥ 35) dan `geoRedundantBackup` di mana compliance memerlukan. Restore dengan *Point-in-time restore* ke server baru, kemudian update app's `appdb` connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): set `backup_retention_period` (≥ 35) dan `backup_window`; simpan automated backup + optional cross-region copy. Restore dengan *RestoreDBInstanceToPointInTime*, kemudian repoint app.

Managed PITR memberikan ≤ 5 min RPO dengan no app change — app hanya perlu connection string baru (dan existing retrying execution strategy, lihat [scaling.md](../deployment/scaling.md), tolerate cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on`, `archive_command` ke object storage) + periodic `pg_basebackup`. Restore = restore base backup + replay WAL ke target time. Ini yang meet RPO target.
- **Logical dump (secondary):** nightly `pg_dump -Fc appdb` ke off-box storage untuk portability /
