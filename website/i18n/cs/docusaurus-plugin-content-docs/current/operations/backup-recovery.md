---
description: "Toto je trading/finanční app: databáze drží trading účty, copy profily, prop-firm challenges, audit chains, a Data Protection key ring…"
---

# Záloha a disaster recovery

Toto je trading/finanční app: databáze drží trading účty, copy profily, prop-firm
challenges, audit chains, a Data Protection key ring. Ztráta jí znamená ztrátu peněz a porušení
regulatorních/audit povinností. Zálohujte ji, a **dokažte že restore funguje**.

## Cíle

| Metrika | Cíl | Význam |
|--------|--------|--------|
| RPO (max data loss) | ≤ 5 min | Použijte point-in-time recovery (continuous WAL), ne jen noční dumpy. |
| RTO (max downtime) | ≤ 1 h | Čas na obnovení + přesměrování app na obnovenou databázi. |
| Backup retention | ≥ 35 days | Pokrývá pozdně-objevenou korupci + měsíční audit windows. |
| Restore drill | měsíčně | Netestovaná záloha není záloha. |

## Co musí být zálohováno

1. **Postgres databáze** — veškerá app data (single logical database `appdb`).
2. **Data Protection key ring** — persisted **in** databázi
   (`PersistKeysToDbContext<DataContext>`) a PFX-encrypted via `App:DataProtectionCertBase64`.
   Ride along in DB backup, **but the protecting certificate + its password
   (`App:DataProtectionCertPassword`) are secrets stored outside the DB** — zálohujte je ve
   svém secrets manageru. Bez certu nemůžete dešifrovat tajemství (cTID hesla, Open API tokeny,
   node tajemství, AI klíč) po obnovení.

## Managed Postgres (doporučeno)

Obě cloud IaC cesty provisionují managed Postgres s built-in PITR — enable + verify retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): set
  `backup.backupRetentionDays` (≥ 35) and `geoRedundantBackup` where compliance requires it. Restore
  with *Point-in-time restore* to a new server, then update app's `appdb` connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): set `backup_retention_period` (≥ 35) and
  `backup_window`; keep automated backups + optional cross-region copy. Restore with
  *RestoreDBInstanceToPointInTime*, then repoint the app.

Managed PITR dává ≤ 5 min RPO bez app změn — app just needs the new connection string
(and the existing retrying execution strategy, viz [scaling.md](../deployment/scaling.md), tolerates the
cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on`, `archive_command` to
  object storage) + a periodic `pg_basebackup`. Restore = restore base backup + replay WAL to the
  target time. Toto is what meets the RPO target.
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` to off-box storage for portability /
  partial restores. Not sufficient alone for the RPO target.
- Encrypt backups at rest; store off the database host.

## Restore drill (spouštět měsíčně)

1. Obnovte poslední zálohu (PITR to "now − 10 min") into a **scratch** database, not production.
2. Namiřte throwaway app instance (nebo psql session) na ni.
3. Ověřte schema: `dotnet ef migrations list` shows no pending migrations, app starts and becomes
   `/health`-ready.
4. **Ověřte audit chain** is intact and unbroken via `IAuditTrailVerifier` (the tamper-evident
   `AuditChainInterceptor` chain) — broken chain after restore znamená korupci nebo tampering.
5. Potvrďte secret decryption works (e.g. an Open API authorization decrypts) — proves the Data
   Protection cert + password were restored correctly.
6. Record the drill result (time taken vs RTO) and destroy the scratch database.

Automatizujte kroky 1–4 in CI where the environment allows (restore a seeded backup into a Testcontainer,
run `dotnet ef migrations list` + the audit-chain verify) takže a broken-backup regression is caught
before you need it.

## Po reálném obnovení

1. Obnovte DB (PITR to just before the incident).
2. Ensure the Data Protection cert + password are the **same** ones in use before the incident.
3. Repoint app `appdb` connection string; roll the replicas.
4. Startup runs migrations under the advisory lock (viz scaling.md) — safe with N replicas.
5. Copy/prop-firm supervisoři reclaim their leases and **resync from the broker** (cTrader is the
   source of truth), takže open positions reconverge automatically — nothing is trusted from stale local
   state.
6. Ověřte audit chain + spot-check recent trading data.
