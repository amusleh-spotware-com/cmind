---
description: "Fakta pelaksanaan salinan setiap percubaan — kependaman, gelinciranrealisasi, isi berbanding kegagalan — ditangkap setiap percubaan salinan, dipaparkan sebagai laporan ketelusan setiap profil. Secara lalai…"
---

# Ketelusan pelaksanaan salinan (Fasa 3)

Fakta pelaksanaan salinan setiap percubaan — kependaman, gelinciran realisasi, isi berbanding kegagalan — ditangkap setiap percubaan salinan,
dipaparkan sebagai laporan ketelusan setiap profil. **Secara lalai dimatikan**; dayakan dengan
`App:Copy:TransparencyEnabled=true`. Apabila dimatikan, enjin salinan byte-untuk-byte tidak berubah: hos memancarkan
ke sink no-op, tiada apa ditulis.

## Cara ia berfungsi

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (ketelusan off) NullCopyEventSink   → buangan (lalai; kos laluan panas sifar)
             (ketelusan on)  ChannelCopyEventSink → saluran dalam-memori bersempadan (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  kumpulan setiap selang saliran App
                                   ▼
                          Jadual hanya-tambah CopyExecution  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Laluan panas kekal bebas I/O.** Hos memanggil `ICopyEventSink.Record(...)` — bukan menyekat,
  tidak pernah membaling. Tidak pernah tunggu, tidak pernah sentuh DB, tidak pernah halang pelaksanaan pesanan.
- **Kehilangan diutamakan berbanding belakang-tekanan.** Saluran bersempadan (`CopyExecutionChannelCapacity`) dengan
  `DropOldest`: jika saliran DB terbantut, *tertua* baris ketelusan digugurkan berbanding melambatkan
  salinan. Ketelusan = telemetri usaha terbaik, bukan kebergantungan perdagangan.
- **Penerusan di luar jalur.** `CopyExecutionDrainer` menyalir saluran dalam kumpulan
  (`CopyExecutionDrainBatchSize`) pada `CopyExecutionDrainInterval`, tulis baris `CopyExecution` melalui
  `DataContext` berskop. Flush akhir pada penutupan.
- **Fakta, bukan perintah.** `CopyExecution` = log hanya-tambah (seperti `InstanceLog`/`AuditLog`), bukan
  agregat. Model baca bertanya secara langsung (CQRS-lite), agregat dalam memori.

## Apa yang direkodkan

Satu `CopyExecutionRecord` setiap percubaan salinan pada satu destinasi:

| Jenis | Bila | Membawa |
|------|------|---------|
| `Opened` | pesanan salinan diletakkan | simbol, sisi, volum wire, harga master, gelinciran realisasi ( mata), kependaman (ms) |
| `Failed` | buka salinan dibuang/ditolak | simbol, sisi, volum/harga master, kependaman, sebab kegagalan (jenis pengecualian) |

(`Closed`/`Skipped`/`Reconciled` wujud dalam enum untuk pengembangan masa depan.)

## Laporan

`GET /api/copy/profiles/{id}/transparency` (skop pemilik) mengembalikan, merentasi 500 fakta terkini:

- **Ringkasan** — total, dibuka, gagal, **kadar isi**, **kependaman purata (ms)**, **gelinciran purata (mata)**.
- **Terbaru** — fakta terkini mentah (destinasi, posisi sumber, simbol, sisi, volum, harga master,
  gelinciran, kependaman, sebab, cap waktu).

## Konfigurasi (`App:Copy`)

| Tetapan | Lalai | Kesan |
|---------|---------|--------|
| `TransparencyEnabled` | `false` → Hidupkan捕获 fakta salinan setiap percubaan + saliran untuk nod. |

Kapasiti saluran, saiz kumpulan saliran, selang saliran = pemalar `CopyDefaults`
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Ujian

- **Unit** (`CopyTransparencyTests`) — buka berjaya memancarkan fakta `Opened` dengan simbol/sisi/volum/kependaman yang betul; buka ditolak memancarkan fakta `Failed` dengan sebab. Dikendalikan melalui sink tangkapan.
- **Integrasi** (`CopyExecutionDrainerTests`, Postgres sebenar) — saliran mengekalkan fakta buffer ke log `CopyExecution`; tulis sink kosong tidak menulis apa-apa.
- **DST** — hos berubah baiki-dan-lupa dengan sink lalai no-op, jadi suite tekanan salinan deterministik kekal hijau (23/23).
