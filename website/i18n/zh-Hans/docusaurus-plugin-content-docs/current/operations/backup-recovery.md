---
description: "This is a trading/financial app: the database holds trading accounts, copy profiles, prop-firm challenges, audit chains, and the Data Protection key ring…"
---

# Backup & disaster recovery

This is a trading/financial app: the database holds trading accounts, copy profiles, prop-firm
challenges, audit chains, and the Data Protection key ring. Losing it loses money and breaks
regulatory/audit obligations. Back it up, and **prove the restore works**.

## Targets

| Metric | Target | Meaning |
|--------|--------|---------|
| RPO (max data loss) | ≤ 5 min | Use point-in-time recovery (continuous WAL), not just nightly dumps. |
| RTO (max downtime) | ≤ 1 h | Time to restore + re-point the app at the restored database. |
| Backup retention | ≥ 35 days | Covers a late-discovered corruption + monthly audit windows. |
| Restore drill | monthly | An untested backup is not a backup. |

## What must be backed up

1. **The Postgres database** — all app data (single logical database `appdb`).
2. **The Data Protection key ring** — persisted **in** the database
   (`PersistKeysToDbContext<DataContext>`) and PFX-encrypted via `App:DataProtectionCertBase64`.
   It rides along in the DB backup, **but the protecting certificate + its password
   (`App:DataProtectionCertPassword`) are secrets stored outside the DB** — back them up in your
   secrets manager. Without the cert you cannot decrypt secrets (cTID passwords, Open API tokens,
   node secrets, AI key) after a restore.

## Managed Postgres (recommended)

Both cloud IaC paths provision managed Postgres with built-in PITR — enable + verify retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): set
  `backup.backupRetentionDays` (≥ 35) and `geoRedundantBackup` where compliance requires it. Restore
  with *Point-in-time restore* to a new server, then update the app's `appdb` connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): set `backup_retention_period` (≥ 35) and
  `backup_window`; keep automated backups + optional cross-region copy. Restore with
  *RestoreDBInstanceToPointInTime*, then repoint the app.

Managed PITR gives the ≤ 5 min RPO with no app changes — the app just needs the new connection string
(and the existing retrying execution strategy, see [scaling.md](../deployment/scaling.md), tolerates the
cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on`, `archive_command` to
  object storage) + a periodic `pg_basebackup`. Restore = restore base backup + replay WAL to the
  target time. This is what meets the RPO target.
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` to off-box storage for portability /
  partial restores. Not sufficient alone for the RPO target.
- Encrypt backups at rest; store off the database host.

## Restore drill (run monthly)

1. Restore the latest backup (PITR to "now − 10 min") into a **scratch** database, not production.
2. Point a throwaway app instance (or a psql session) at it.
3. Verify schema: `dotnet ef migrations list` shows no pending migrations, app starts and becomes
   `/health`-ready.
4. **Verify the audit chain** is intact and unbroken via `IAuditTrailVerifier` (the tamper-evident
   `AuditChainInterceptor` chain) — a broken chain after restore means corruption or tampering.
5. Confirm secret decryption works (e.g. an Open API authorization decrypts) — proves the Data
   Protection cert + password were restored correctly.
6. Record the drill result (time taken vs RTO) and destroy the scratch database.

Automate steps 1–4 in CI where the environment allows (restore a seeded backup into a Testcontainer,
run `dotnet ef migrations list` + the audit-chain verify) so a broken-backup regression is caught
before you need it.

## After a real restore

1. Restore DB (PITR to just before the incident).
2. Ensure the Data Protection cert + password are the **same** ones in use before the incident.
3. Repoint the app `appdb` connection string; roll the replicas.
4. Startup runs migrations under the advisory lock (see scaling.md) — safe with N replicas.
5. Copy/prop-firm supervisors reclaim their leases and **resync from the broker** (cTrader is the
   source of truth), so open positions reconverge automatically — nothing is trusted from stale local
   state.
6. Verify audit chain + spot-check recent trading data.

<!-- [ZH-HANS] Translation needed -->
