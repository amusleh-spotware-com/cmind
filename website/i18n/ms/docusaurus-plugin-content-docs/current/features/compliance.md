---
description: "Pembrokeran FX/CFD/crypto ritel membawa kewajipan undang-undang + penyimpanan rekod. Modul melaksanakan empat tiang standard industri: persetujuan pendedahan risiko…"
---

# Undang-undang & pematuhan

Pembrokeran FX/CFD/crypto ritel membawa kewajipan undang-undang + penyimpanan rekod. Modul melaksanakan empat tiang standard industri: **persetujuan pendedahan risiko**, **jejak audit yang tahan tamper**, **penyimpanan rekod gaya MiFID/ESMA**, **hak data GDPR**. Semua pintu oleh bendera ciri `Compliance`.

## 1. Dokumen undang-undang yang disenaraikan + persetujuan

- `LegalDocument` (agregat) — Syarat Perkhidmatan yang disenaraikan, **Pendedahan Risiko** CFD, atau Dasar Privasi. Versi draf, kemudian **diterbitkan**; versi yang diterbitkan **tidak berubah** (edit melempar), jadi teks tepat pengguna setuju sentiasa boleh dipulihkan. Dokumen aktif untuk jenis = versi terbitan tertingginya.
- `ConsentRecord` (agregat) — rekod yang tidak berubah bahawa pengguna menerima versi dokumen khusus pada masa, dengan IP asal.
- **Penguatkuasaan:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` blok tindakan dengan `403` apabila dokumen yang diterbitkan jenis itu wujud dan pengguna tidak bersetuju dengan versi aktifnya. Digunakan untuk **penciptaan profil salinan** (`RiskDisclosure`). Tiada yang diterbitkan → tindakan dibenarkan — tiada apa untuk bersetuju lagi — jadi membolehkan modul tiada blok mundur sehingga pendedahan benar-benar diterbitkan.

## 2. Jejak audit yang tahan tamper

`AuditLog` entri rantai-cincang: setiap baris menyimpan `PrevHash` dan `Hash = SHA-256(prev | medan kanonis)`. `AuditChainInterceptor` menerapkan rantai secara telus pada `SaveChanges`, jadi tapak panggilan audit sedia ada tidak berubah. `IAuditTrailVerifier.VerifyAsync` re-jalan rantai, laporan baris pertama yang disimpan cincang atau pautan balik tidak lagi sepadan — mengesan mana-mana penyuntingan atau penghapusan rekod lepas. Titik akhir pemilik: `GET /api/compliance/audit/verify`.

## 3. Penyimpanan rekod (MiFID II / ESMA RTS)

Penyimpanan rekod berpuas hati oleh **jejak audit rantai-cincang tidak berubah** tambah **rekod persetujuan yang dikekalkan** dan lembut-padam (tidak pernah keras-padam) rekod domain. Cap waktu UTC daripada `TimeProvider` yang disuntik. Rekod persetujuan simpan versi dokumen + IP; dokumen undang-undang yang diterbitkan tidak pernah diubah. Pengekalan = tidak memurnikan jadual ini (tambah-sahaja / lembut-padam).

## 4. Hak data GDPR

- `GET /api/compliance/export` — eksport boleh dibaca mesin data pemanggil (profil, persetujuan, profil salinan, cabaran prop-firm).
- `POST /api/compliance/erase` — hak untuk penghapusan: `AppUser.Anonymize()` mengerik PII (e-mel, MFA) dan baris lembut-padam, mengekalkan rujukan/sejarah audit koheren.

## Ringkasan API

| Kaedah | Laluan | Peranan | Tujuan |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | Pengguna+ | dokumen yang diterbitkan aktif |
| GET | `/api/compliance/consent/status` | Pengguna+ | persetujuan mana yang tertunggak |
| POST | `/api/compliance/consent` | Pengguna+ | terima versi aktif dokumen |
| GET | `/api/compliance/export` | Pengguna+ | eksport data GDPR |
| POST | `/api/compliance/erase` | Pengguna+ | penghapusan GDPR akaun sendiri |
| POST | `/api/compliance/documents` | Pemilik | draf dokumen |
| POST | `/api/compliance/documents/{id}/publish` | Pemilik | terbitan versi |
| GET | `/api/compliance/audit/verify` | Pemilik | sahkan rantai cincang audit |

UI: `/settings/legal` (navigasi *Tetapan → Undang-undang & Privasi*, pintu oleh `Compliance`) menunjukkan perjanjian tertunggak dengan butang terima + tindakan eksport/hapus GDPR.

## Ujian-ujian

- **Unit** — `UnitTests/Compliance/LegalDocumentTests.cs` (draf/terbitan/tidak berubah, tangkapan persetujuan), `AuditChainTests.cs` (pautan cincang, pengesanan tamper, sensitiviti kandungan).
- **Integrasi** — `IntegrationTests/CompliancePersistenceTests.cs` (versi-aktif + pertanyaan persetujuan pada Postgres sebenar), `AuditChainIntegrityTests.cs` (rantai mengesahkan utuh, kemudian mengesan tamper peringkat SQL),
