---
description: "Backup dan recovery — strategi backup, jadwal, dan prosedur restore."
---

# Backup dan Recovery

Backup dan recovery — strategi backup, jadwal, dan prosedur restore.

## Strategi Backup

### 3-2-1 Rule

- **3** salinan data.
- **2** media berbeda.
- **1** salinan offsite.

### Komponen yang Dibackup

| Komponen | Frekuensi | Retention |
|----------|-----------|-----------|
| Database (Postgres) | Setiap 6 jam | 30 hari |
| File uploads | Harian | 90 hari |
| Config files | Setiap change | 90 hari |
| Logs | Mingguan | 30 hari |
| Snapshots | Bulanan | 12 bulan |

## Database Backup

### Automated Backup

```bash
# Full backup setiap hari jam 2 pagi
0 2 * * * pg_dump -U cmind -Fc cmind > /backups/cmind_$(date +\%Y\%m\%d).dump
```

### Continuous Archiving

```bash
# Aktifkan WAL archiving
wal_level = replica
archive_mode = on
archive_command = 'cp %p /backups/wal/%f'
```

### Point-in-Time Recovery

```bash
# Restore ke waktu tertentu
pg_restore -U cmind -d cmind_restore \
  --point-in-time='2024-01-15 10:30:00' \
  /backups/cmind_20240115.dump
```

## File Backup

### Volume Mounts

Data directories di-mount sebagai volume:

```yaml
volumes:
  - pgdata:/var/lib/postgresql/data
  - uploads:/app/uploads
  - backups:/backups
```

### S3 Backup Script

```bash
#!/bin/bash
# Sync backups to S3
aws s3 sync /backups s3://cmind-backups/$(date +\%Y/\%m/\%d)/
# Hapus local backups older than 7 days
find /backups -mtime +7 -delete
```

## Recovery Procedures

### Full Restore

1. Stop aplikasi:

```bash
docker-compose down
```

2. Restore database:

```bash
pg_restore -U cmind -c -d cmind /backups/cmind_latest.dump
```

3. Restore files:

```bash
aws s3 sync s3://cmind-backups/latest/uploads/ /app/uploads/
```

4. Start aplikasi:

```bash
docker-compose up -d
```

### Partial Restore

Restore specific table:

```bash
pg_restore -U cmind -d cmind -t users /backups/cmind_latest.dump
```

## Disaster Recovery

### RTO (Recovery Time Objective)

| Komponen | RTO |
|----------|-----|
| Database | 4 jam |
| File storage | 8 jam |
| Full system | 24 jam |

### RPO (Recovery Point Objective)

| Komponen | RPO |
|----------|-----|
| Database | 6 jam |
| File storage | 24 jam |
| Config | 1 jam |

## Testing

### Backup Verification

```bash
# Test restore ke environment terpisah
docker-compose -f docker-compose.test.yml up -d
pg_restore -U cmind -d cmind_test /backups/cmind_latest.dump
# Verify data integrity
psql -U cmind -d cmind_test -c "SELECT COUNT(*) FROM users;"
```

### Schedule

- **Daily** — backup verification test.
- **Weekly** — full restore test ke staging.
- **Monthly** — disaster recovery drill.
