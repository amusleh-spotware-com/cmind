---
description: "Ini adalah aplikasi trading/keuangan: database menyimpan trading accounts, copy profiles, prop-firm challenges, audit chains, dan Data Protection key ring — kehilangannya berarti kehilangan uang dan melanggar kewajiban regulasi/audit."
---

# Backup & Disaster Recovery

Ini adalah aplikasi trading/keuangan: database menyimpan trading accounts, copy profiles, prop-firm
challenges, audit chains, dan Data Protection key ring. Kehilangannya berarti kehilangan uang dan melanggar
kewajiban regulasi/audit. Cadangkan, dan **buktikan restore berfungsi**.

## Target

| Metrik | Target | Arti |
|--------|--------|------|
| RPO (max data loss) | ≤ 5 min | Gunakan point-in-time recovery (continuous WAL), bukan hanya nightly dumps. |
| RTO (max downtime) | ≤ 1 h | Waktu untuk restore + arahkan ulang app ke database yang di-restore. |
| Backup retention | ≥ 35 days | Mencakup corruption yang ditemukan terlambat + audit window bulanan. |
| Restore drill | monthly | Backup yang tidak di-test bukan backup. |

## Apa yang Harus Di-backup

1. **Database Postgres** — semua data app (single logical database `appdb`).
2. **Data Protection key ring** — persisted **in** the database
   (`PersistKeysToDbContext<DataContext>`) dan PFX-encrypted via `App:DataProtectionCertBase64`.
   Ini ikut dalam DB backup, **tapi certificate pelindung + passwordnya**
   (`App:DataProtectionCertPassword`) adalah secrets yang disimpan di luar DB** — backup mereka di
   secrets manager Anda. Tanpa cert Anda tidak dapat mendekripsi secrets (cTID passwords, Open API tokens,
   node secrets, AI key) setelah restore.

## Managed Postgres (direkomendasikan)

Kedua cloud IaC paths menyediakan managed Postgres dengan built-in PITR — enable + verifikasi retention:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): set
  `backup.backupRetentionDays` (≥ 35) dan `geoRedundantBackup` dimana compliance memerlukan. Restore
  dengan *Point-in-time restore* ke server baru, lalu update `appdb` connection string app.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): set `backup_retention_period` (≥ 35) dan
  `backup_window`; tetap automated backups + optional cross-region copy. Restore dengan
  *RestoreDBInstanceToPointInTime*, lalu arahkan ulang app.

Managed PITR memberikan RPO ≤ 5 min tanpa perubahan app — app hanya butuh connection string baru
(dan existing retrying execution strategy, lihat [scaling.md](../deployment/scaling.md), tolerates the
cutover blip).

## Self-hosted Postgres

- **Continuous archiving (PITR):** enable WAL archiving (`archive_mode=on`, `archive_command` ke
  object storage) + periodic `pg_basebackup`. Restore = restore base backup + replay WAL ke
  target time. Ini yang memenuhi target RPO.
- **Logical dumps (secondary):** nightly `pg_dump -Fc appdb` ke off-box storage untuk portability /
  partial restores. Tidak cukup sendiri untuk target RPO.
- Encrypt backups at rest; simpan off the database host.

## Restore Drill (jalankan monthly)

1. Restore backup terbaru (PITR ke "now − 10 min") ke **scratch** database, bukan production.
2. Arahkan throwaway app instance (atau psql session) ke sana.
3. Verifikasi schema: `dotnet ef migrations list` menunjukkan tidak ada pending migrations, app starts and becomes
   `/health`-ready.
4. **Verifikasi audit chain** intact dan unbroken via `IAuditTrailVerifier` (the tamper-evident
   `AuditChainInterceptor` chain) — broken chain after restore berarti corruption atau tampering.
5. Konfirmasi secret decryption works (mis. sebuah Open API authorization decrypted) — proves the Data
   Protection cert + password were restored correctly.
6. Catat hasil drill (waktu yang diambil vs RTO) dan destroy scratch database.

Otomasi langkah 1–4 di CI dimana environment memungkinkan (restore seeded backup ke Testcontainer,
run `dotnet ef migrations list` + audit-chain verify) sehingga regression backup yang rusak caught
sebelum Anda membutuhkannya.

## Setelah Restore Nyata

1. Restore DB (PITR ke tepat sebelum insiden).
2. Pastikan Data Protection cert + password adalah **sama** dengan yang digunakan sebelum insiden.
3. Arahkan ulang app `appdb` connection string; roll replicas.
4. Startup menjalankan migrations under advisory lock (lihat scaling.md) — aman dengan N replicas.
5. Copy/prop-firm supervisors reclaim leases mereka dan **resync dari broker** (cTrader adalah
   source of truth), sehingga open positions reconverge otomatis — tidak ada yang dipercaya dari stale local
   state.
6. Verifikasi audit chain + spot-check data trading recent.
