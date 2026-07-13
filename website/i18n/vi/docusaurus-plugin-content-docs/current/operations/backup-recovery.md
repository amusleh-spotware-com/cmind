---
description: "Ứng dụng trading/tài chính này: database chứa trading accounts, copy profiles, prop-firm challenges, audit chains, và Data Protection key ring…"
---

# Backup & disaster recovery

Đây là ứng dụng trading/tài chính: database chứa trading accounts, copy profiles, prop-firm challenges, audit chains, và Data Protection key ring. Mất nó mất tiền và breaks regulatory/audit obligations. Backup nó, và **chứng minh restore hoạt động**.

## Targets

| Metric | Target | Meaning |
|--------|--------|---------|
| RPO (max data loss) | ≤ 5 min | Sử dụng point-in-time recovery (continuous WAL), không chỉ nightly dumps. |
| RTO (max downtime) | ≤ 1 h | Thời gian để restore + re-point app tới restored database. |
| Backup retention | ≥ 35 days | Covers late-discovered corruption + monthly audit windows. |
| Restore drill | monthly | Untested backup không phải backup. |

## Cái gì phải được backed up

1. **Postgres database** — tất cả app data (single logical database `appdb`).
2. **Data Protection key ring** — persisted **trong** database (`PersistKeysToDbContext<DataContext>`) và PFX-encrypted qua `App:DataProtectionCertBase64`. Nó rides along trong DB backup, **nhưng protecting certificate + password của nó (`App:DataProtectionCertPassword`) là secrets lưu trữ ngoài DB** — backup chúng trong secrets manager của bạn. Không có cert bạn không thể decrypt secrets (cTID passwords, Open API tokens, node secrets, AI key) sau restore.

## Managed Postgres (recommended)

Cả hai cloud IaC paths cấp phát managed Postgres với built-in PITR — enable + verify retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): đặt `backup.backupRetentionDays` (≥ 35) và `geoRedundantBackup` nơi compliance yêu cầu. Restore với Point-in-time restore tới new server, sau đó update app's `appdb` connection string.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): đặt `backup_retention_period` (≥ 35) và `backup_window`; keep automated backups + optional cross-region copy. Restore với RestoreDBInstanceToPointInTime, sau đó repoint app.

Managed PITR cho ≤ 5 min RPO mà không cần app changes — app chỉ cần connection string mới (và existing retrying execution strategy, xem [scaling.md](../deployment/scaling.md), tolerates cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on`, `archive_command` tới object storage) + periodic `pg_basebackup`. Restore = restore base backup + replay WAL tới target time. Đây là cái meets RPO target.
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` tới off-box storage cho portability / partial restores. Không đủ alone cho RPO target.
- Encrypt backups at rest; store off database host.

## Restore drill (chạy monthly)

1. Restore latest backup (PITR tới "now − 10 min") vào **scratch** database, không phải production.
2. Point throwaway app instance (hoặc psql session) tại nó.
3. Verify schema: `dotnet ef migrations list` shows không pending migrations, app starts và becomes `/health`-ready.
4. **Verify audit chain** intact và unbroken qua `IAuditTrailVerifier` (tamper-evident `AuditChainInterceptor` chain) — broken chain sau restore means corruption hoặc tampering.
5. Confirm secret decryption works (ví dụ Open API authorization decrypts) — proves Data Protection cert + password được restored chính xác.
6. Record drill result (time taken vs RTO) và destroy scratch database.

Automate steps 1–4 trong CI nơi environment cho phép (restore seeded backup vào Testcontainer, chạy `dotnet ef migrations list` + audit-chain verify) nên broken-backup regression được caught trước bạn cần nó.

## Sau một real restore

1. Restore DB (PITR tới just before incident).
2. Ensure Data Protection cert + password là **same** ones trong use trước incident.
3. Repoint app `appdb` connection string; roll replicas.
4. Startup chạy migrations dưới advisory lock (xem scaling.md) — safe với N replicas.
5. Copy/prop-firm supervisors reclaim leases của chúng và **resync từ broker** (cTrader là source of truth), nên open positions reconverge tự động — nothing trusted từ stale local state.
6. Verify audit chain + spot-check recent trading data.
