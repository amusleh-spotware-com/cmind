---
description: "Modul kepatuhan — pantau batas prop-firm, aturan trading, dan kepatuhan regulatory."
---

# Kepatuhan

Modul kepatuhan — pantau batas prop-firm, aturan trading, dan kepatuhan regulatory.

## Overview

cMind menyediakan sistem kepatuhan komprehensif untuk akun prop-firm dan akun regular:

- **Monitoring batas** — profit/loss harian, drawdown, batas posisi.
- **Alert** — notifikasi saat mendekati batas.
- **Enforcement** — tindakan otomatis saat batas dilanggar.
- **Reporting** — laporan kepatuhan untuk audit.

## Prop-Firm Rules

### Challenge Types

| Tipe | Durasi | Max Daily Loss | Max Total Loss |
|------|--------|----------------|----------------|
| Evaluation | 30 hari | 5% | 10% |
| Evaluation Plus | 60 hari | 4% | 8% |
| Funded | Ongoing | 5% | 10% |

### Monitoring

Setiap akun prop-firm dipantau untuk:

- **Profitabilitas** — profit harian dan total.
- **Drawdown** — drawdown saat ini vs batas.
- **Trading rules** — jam trading, posisi max, lot size.
- **Risk rules** — exposure per simbol, correlation limits.

### Enforcement

| Pelanggaran | Tindakan |
|-------------|----------|
| Daily loss limit exceeded | Akun ditangguhkan, notifikasi dikirim |
| Total loss limit exceeded | Akun ditutup, dana dijamin |
| Trading hours violation | Trade dibatalkan, warning issued |
| Position limit exceeded | Trade ditolak |

## Regulatory Compliance

### AML / KYC

- Verifikasi identitas otomatis.
- Monitoring transaksi mencurigakan.
- Laporan regulatory otomatis.

### Trade Reporting

- Semua trade dilaporkan ke pihak yang diperlukan.
- Audit trail lengkap.
- Retensi data sesuai regulasi.

## Alerting

Sistem mengirim alert melalui:

- **In-app notifications** — muncul di dashboard.
- **Email** — notifikasi ke alamat terdaftar.
- **Webhook** — integrasi dengan sistem eksternal.

## Laporan

Laporan kepatuhan tersedia di **Compliance → Reports**:

- **Daily summary** — ringkasan aktivitas trading.
- **Rule violations** — daftar pelanggaran dan tindakan.
- **Performance report** — laporan performa untuk prop-firm.
