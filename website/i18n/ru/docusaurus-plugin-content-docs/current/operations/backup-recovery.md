---
description: "Это trading/financial приложение: database holds trading accounts, copy profiles, prop-firm challenges, audit chains, и Data Protection key ring…"
---

# Backup & disaster recovery

Это trading/financial приложение: database holds trading accounts, copy profiles, prop-firm challenges, audit chains, и Data Protection key ring. Losing это loses money и breaks regulatory/audit obligations. Back это up, и **prove restore works**.

## Targets

| Metric | Target | Meaning |
|--------|--------|---------|
| RPO (max data loss) | ≤ 5 min | Use point-in-time recovery (continuous WAL), не только nightly dumps. |
| RTO (max downtime) | ≤ 1 h | Time для restore + re-point app на restored database. |
| Backup retention | ≥ 35 days | Covers late-discovered corruption + monthly audit windows. |
| Restore drill | monthly | Untested backup — не backup. |

## Что должно быть backed up

1. **Postgres database** — все app data (одна logical database `appdb`).
2. **Data Protection key ring** — persisted **в** database (`PersistKeysToDbContext<DataContext>`) и PFX-encrypted через `App:DataProtectionCertBase64`. Это rides along в DB backup, **но protecting certificate + его password (`App:DataProtectionCertPassword`) — secrets хранящиеся outside DB** — back их up в ваш secrets manager. Без cert вы не можете decrypt secrets (cTID passwords, Open API tokens, node secrets, AI key) после restore.

## Managed Postgres (recommended)

Оба cloud IaC paths provision managed Postgres с built-in PITR — enable + verify retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): set `backup.backupRetentionDays` (≥ 35) и `geoRedundantBackup` где compliance требует это. Restore с *Point-in-time restore* на новый сервер, затем update app's `appdb` connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): set `backup_retention_period` (≥ 35) и `backup_window`; keep automated backups + optional cross-region copy. Restore с *RestoreDBInstanceToPointInTime*, затем repoint app.

Managed PITR дает ≤ 5 min RPO с нет app changes — app просто needs новый connection string (и existing retrying execution strategy, см. [scaling.md](../deployment/scaling.md), tolerates cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on`, `archive_command` на object storage) + periodic `pg_basebackup`. Restore = restore base backup + replay WAL на target time. Это что meets RPO target.
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` на off-box storage для portability / partial restores. Не sufficient один для RPO target.
- Encrypt backups в rest; store off database host.

## Restore drill (run monthly)

1. Restore latest backup (PITR на "now − 10 min") в **scratch** database, не production.
2. Point throwaway app instance (или psql session) на это.
3. Verify schema: `dotnet ef migrations list` shows нет pending migrations, app starts и becomes `/health`-ready.
4. **Verify audit chain** intact и unbroken через `IAuditTrailVerifier` (tamper-evident `AuditChainInterceptor` chain) — broken chain после restore means corruption или tampering.
5. Confirm secret decryption works (например Open API authorization decrypts) — proves Data Protection cert + password были restored correctly.
6. Record drill result (time taken vs RTO) и destroy scratch database.

Automate шаги 1–4 в CI где environment позволяет (restore seeded backup в Testcontainer, run `dotnet ef migrations list` + audit-chain verify) поэтому broken-backup regression caught перед вы нуждаетесь это.

## После real restore

1. Restore DB (PITR к just перед incident).
2. Ensure Data Protection cert + password — то **же** ones в use перед incident.
3. Repoint app `appdb` connection string; roll replicas.
4. Startup runs migrations under advisory lock (см. scaling.md) — safe с N replicas.
5. Copy/prop-firm supervisors reclaim их leases и **resync из broker** (cTrader — source of truth), поэтому open positions reconverge automatically — nothing trusted из stale local state.
6. Verify audit chain + spot-check recent trading data.
