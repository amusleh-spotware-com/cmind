---
description: "Ini ialah apl perdagangan/kewangan: pangkalan data memegang akaun perdagangan, profil salinan, cabaran prop-firm, rantaian audit, dan cincin kunci Perlindungan Data…"
---

# Sandaran & pemulihan bencana

Ini ialah apl perdagangan/kewangan: pangkalan data memegang akaun perdagangan, profil salinan, cabaran prop-firm,
rantai audit, dan cincin kunci Perlindungan Data. Kehilangan nó loses wang dan memecahkan
kewajiban regulator/audit. Sandar nó, dan **buktikan pemulihan berfungsi**.

## Sasaran

| Metrik | Sasaran | Makna |
|--------|---------|---------|
| RPO (max kehilangan data) | ≤ 5 minit | Gunakan pemulihan titik-dalam-masa (WAL berterusan), bukan hanya longkangan malam. |
| RTO (max masa henti) | ≤ 1 jam | Masa untuk pemulihan + hantar semula apl pada pangkalan data yang dipulihkan. |
| Retensi sandaran | ≥ 35 hari | Mencukupi untukrosakan yang ditemui lambat + jendela audit bulanan. |
| Drill pemulihan | bulanan | Sandaran yang tidak diuji bukan sandaran. |

## Apa yang wajib disandarkan

1. **Pangkalan data Postgres** — semua data apl (pangkalan data logikal tunggal `appdb`).
2. **Cincin kunci Perlindungan Data** — bertekun **dalam** pangkalan data
   (`PersistKeysToDbContext<DataContext>`) dan dienkripsi PFX melalui `App:DataProtectionCertBase64`.
   nó rides bersama dalam sandaran DB, **tetapi sijil pelindung + kata laluan nó**
   (`App:DataProtectionCertPassword`) ialah **rahsia yang disimpan di luar DB** — sandar nó dalam
   pengurus rahsia anda. Tanpa sijil anda tidak boleh menyahsulit rahsia (kata laluan cTID, token Open API,
   rahsia nod, kunci AI) selepas pemulihan.

## Postgres terkelola (disyorkan)

Kedua-dua laluan IaC awan membekalkan Postgres terkelola dengan PITR terbina dalam — dayakan + sahkan retensi:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): tetapkan
  `backup.backupRetentionDays` (≥ 35) dan `geoRedundantBackup` di mana pematuhan memerlukan. Pulihkan dengan *Point-in-time restore* ke pelayan baharu, kemudian kemas kini rentetan sambungan `appdb` apl.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): tetapkan `backup_retention_period` (≥ 35) dan
  `backup_window`; simpan sandaran automatik + salinan lintas-rantau pilihan. Pulihkan dengan
  *RestoreDBInstanceToPointInTime*, kemudian hantar semula apl.

PITR terkelola memberi RPO ≤ 5 minit dengan tiada perubahan apl — apl hanya perlukan rentetan sambungan baharu
(dan strategi percubaan semula pelaksanaan sedia ada, lihat [scaling.md](../deployment/scaling.md)) menahan limpahan pemotongan.

## Postgres sendiri dihos

- **Pengarkiban berterusan (PITR):** dayakan pengarkiban WAL (`archive_mode=on`, `archive_command` kepada
  storan objek) + `pg_basebackup` berkala. Pulihkan = pulihkan sandaran asas + main balik WAL ke
  masa sasaran. Ini yang memenuhi sasaran RPO.
- **Longkangan logik (sekunder):** `pg_dump -Fc appdb` malam ke storan di luar kotak untuk perkhidmatan /
  pemulihan separa. Tidak mencukupi sendiri untuk sasaran RPO.
- **Enkripsi sandaran atas meja; simpan di luar hos pangkalan data.**

## Drill pemulihan (jalankan bulanan)

1. Pulihkan sandaran terkini (PITR kepada "sekarang − 10 min") ke dalam pangkalan data **senggat**, bukan pengeluaran.
2. Tunjuk contoh apl buangan (atau sesi psql) pad nó.
3. Sahkan skema: `dotnet ef migrations list` menunjukkan tiada migrasi yang belum selesai, apl bermula dan menjadi
   `/health`-sedia.
4. **Sahkan rantai audit** utuh dan tidak terputus melalui `IAuditTrailVerifier` (rantai `AuditChainInterceptor` anti-manipulasi) — rantai yang patah selepas pemulihan bermakna rosak atau manipulasi.
5. Sahkan penyahsulitan rahsia berfungsi (cth, otorisasi Open API menyahsulit) — membuktikan sijil +
   kata laluan Perlindungan Data dipulihkan dengan betul.
6. Rekod keputusan drill (masa diambil vs RTO) dan musnahkan pangkalan data senggat.

Automasikan langkah 1–4 dalam CI di mana persekitaran membenarkan (pulihkan sandaran berseed ke Testcontainer,
jalan `dotnet ef migrations list` + sahkan rantai audit) jadi regresi sandaran-rosak ditangkap
sebelum anda perlukannya.

## Selepas pemulihan sebenar

1. Pulihkan DB (PITR kepada tepat sebelum insiden).
2. Pastikan sijil + kata laluan Perlindungan Data ialah **yang sama** yang digunakan sebelum insiden.
3. Hantar semula rentetan sambungan `appdb` apl; gulingkan replika.
4. Permulaan menjalankan migrasi di bawah kunci nasihat (lihat scaling.md) — selamat dengan N replika.
5. Penyelia salinan/prop-firm menuntut semula lesen mereka dan **menyegerakkan daripada broker** (cTrader ialah sumber kebenaran), jadi posisi terbuka converge secara automatik — tiada apa yang dipercayai daripada keadaan tempatan basi.
6. Sahkan rantai audit + semak semula data perdagangan terkini.
