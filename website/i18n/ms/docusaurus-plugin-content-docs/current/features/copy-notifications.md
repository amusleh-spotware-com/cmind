---
description: "Suapan setiap pemilik acara salinan yang berkaitan dengan keselamatan — destinasi mencetuskan breaker penolakan, perlindungan akaun atau pelanggaran peraturan prop, panik rata. Secara lalai…"
---

# Pemberitahuan operasi salinan (Fasa 2b)

Suapan setiap pemilik acara salinan yang berkaitan dengan keselamatan — destinasi mencetuskan breaker penolakan, perlindungan akaun atau pelanggaran peraturan prop, panik rata. **Secara lalai aktif** (`App:Copy:NotificationsEnabled`, lalai `true`); tetapkan false untuk senyapkan. Konsep sendiri dalam konteks Salin, berasingan daripada agregat `AlertRule` pasar/AI.

## Cara ia berfungsi

Corak hos→sink→saliran yang sama seperti log ketelusan pelaksanaan:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (pemberitahuan off) NullCopyNotificationSink   → buangan (no-op; enjin tidak berubah)
             (pemberitahuan on)  ChannelCopyNotificationSink → saluran DropOldest bersempadan
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  selesaikan pemilik Profil setiap pemberitahuan, kumpulan
                                     ▼
                            Suapan Pemberitahuan Salinan  ◀── GET /api/copy/notifications
```

- Hos `Notify(...)` bukan menyekat, tidak pernah membaling — tidak pernah sentuh DB, tidak pernah lambatakan salinan.
- Saliran menyelesaikan `UserId` yang memiliki dari profil setiap pemberitahuan; pemberitahuan yang profilnya hilang (pemilik tidak boleh diselesaikan) digugurkan, tidak Diasingkan.
- `CopyNotification` = suapan hanya-tambah, boleh-aku tahu setiap baris (bukan agregat).

## Apa yang dinaikkan

| Jenis | Severity | Bila |
|------|----------|------|
| `DestinationTripped` | Warning | Bajet penolakan G8 habis; buka baharu dihentikan untuk masa penyejukan. |
| `AccountProtectionTriggered` | Critical | Lantai/had ekuiti ZuluGuard dilanggar; buka dilekapkan (SellOut mencairkan). |
| `PropRuleBreached` | Critical | Kehilangan harian prop / undur traillng dilanggar; destinasi diratakan + dikunci untuk hari tersebut. |
| `FlattenAll` | Critical | Panik ratakan dilaksanakan; setiap destinasi ditutup + dikunci. |
| `TokenInvalidated` | (dikhaskan) | Token destinasi dibatalkan; menunggu putaran. |

## API

- `GET /api/copy/notifications` (skop pemilik) — pemberitahuan terkini pengguna (200 terkini) merentasi semua profil, ditambah bilangan **tidak diaknowledge**.
- `POST /api/copy/notifications/{id}/acknowledge` — tandakan satu sebagai dibaca.

## Konfigurasi (`App:Copy`)

| Tetapan | Lalai | Kesan |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Pancarkan pemberitahuan keselamatan + jalankan saliran. `false` → sink no-op. |

## Ujian

- **Unit** (`CopyNotificationTests`) — destinasi yang dicetuskan membangkitkan `DestinationTripped`; ratakan panik membangkitkan `FlattenAll` peringkat profil. Melalui sink tangkapan.
- **Integrasi** (`CopyNotificationDrainerTests`, Postgres sebenar) — saliran menyelesaikan pemilik + mengekalkan; pemberitahuan untuk profil tidak diketahui digugurkan.
- **DST** — hos memancarkan baiki-dan-lupa dengan sink lalai no-op, jadi suite tekanan salinan kekal hijau (23/23).
