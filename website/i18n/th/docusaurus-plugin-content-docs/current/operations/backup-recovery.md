---
description: "This is a trading/financial app: the database holds trading accounts, copy profiles, prop-firm challenges, audit chains, and the Data Protection key ring…"
---

# Backup & disaster recovery

นี้ trading/financial app: database holds trading accounts copy profiles prop-firm
challenges audit chains และ Data Protection key ring losing มัน loses money และ breaks
regulatory/audit obligations back มัน up และ **prove restore works**

## Targets

| Metric | Target | Meaning |
|--------|--------|---------|
| RPO (max data loss) | ≤ 5 min | Use point-in-time recovery (continuous WAL) ไม่ใช่ เพียงnightly dumps |
| RTO (max downtime) | ≤ 1 h | Time ไป restore + re-point app ที่ restored database |
| Backup retention | ≥ 35 days | Covers late-discovered corruption + monthly audit windows |
| Restore drill | monthly | untested backup ไม่ใช่ backup |

## What must backed up

1. **Postgres database** — ทั้งหมด app data (single logical database `appdb`)
2. **Data Protection key ring** — persisted **in** database
   (`PersistKeysToDbContext<DataContext>`) และ PFX-encrypted ผ่าน `App:DataProtectionCertBase64`
   มัน rides along ใน DB backup **แต่ protecting certificate + password ของมัน
   (`App:DataProtectionCertPassword`) secrets stored outside DB** — back พวกเขา up ใน secrets manager ของคุณ
   ไม่มี cert คุณ cannot decrypt secrets (cTID passwords Open API tokens
   node secrets AI key) หลัง restore

## Managed Postgres (recommended)

ทั้ง cloud IaC paths provision managed Postgres ด้วย built-in PITR — enable + verify retention:

- **Azure** (`deploy/azure/main.bicep` Flexible Server): set
  `backup.backupRetentionDays` (≥ 35) และ `geoRedundantBackup` ที่ compliance requires มัน restore
  ด้วย *Point-in-time restore* ไป new server จากนั้น update app ของ `appdb` connection string
- **AWS** (`deploy/aws` RDS Postgres Terraform): set `backup_retention_period` (≥ 35) และ
  `backup_window`; keep automated backups + optional cross-region copy restore ด้วย
  *RestoreDBInstanceToPointInTime* จากนั้น repoint app

Managed PITR gives ≤ 5 min RPO ด้วย ไม่มี app changes — app just needs new connection string
(และ existing retrying execution strategy ดู [scaling.md](../deployment/scaling.md) tolerates
cutover blip)

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on` `archive_command` ไป
  object storage) + periodic `pg_basebackup` restore = restore base backup + replay WAL ไป
  target time นี้ คือ what meets RPO target
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` ไป off-box storage สำหรับ portability /
  partial restores ไม่ sufficient alone สำหรับ RPO target
- encrypt backups ที่ rest; store off database host

## Restore drill (run monthly)

1. restore latest backup (PITR ไป "now − 10 min") ไป **scratch** database ไม่ production
2. point throwaway app instance (หรือ psql session) ที่มัน
3. verify schema: `dotnet ef migrations list` shows ไม่มี pending migrations app starts และ becomes
   `/health`-ready
4. **Verify audit chain** intact และ unbroken ผ่าน `IAuditTrailVerifier` (tamper-evident
   `AuditChainInterceptor` chain) — broken chain หลัง restore means corruption หรือ tampering
5. confirm secret decryption works (เช่น Open API authorization decrypts) — proves Data
   Protection cert + password were restored correctly
6. record drill result (time taken vs RTO) และ destroy scratch database

automate steps 1–4 ใน CI ที่ environment allows (restore seeded backup ไป Testcontainer
run `dotnet ef migrations list` + audit-chain verify) ดังนั้น broken-backup regression caught
ก่อน คุณ need มัน

## After real restore

1. restore DB (PITR ไป just ก่อน incident)
2. ensure Data Protection cert + password เป็น **same** ones ใน use ก่อน incident
3. repoint app `appdb` connection string; roll replicas
4. startup runs migrations ภายใต้ advisory lock (ดู scaling.md) — safe ด้วย N replicas
5. copy/prop-firm supervisors reclaim leases ของพวกเขา และ **resync จาก broker** (cTrader source ของ truth) ดังนั้น open positions reconverge อัตโนมัติ — nothing trusted จาก stale local
   state
6. verify audit chain + spot-check recent trading data
