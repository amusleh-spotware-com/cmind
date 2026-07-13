---
description: "Direktori boleh-layari strategi salinan. Penyedia menerbitkan profil salinan sebagai penyenaraian dengan lencana hidup-teresahkan (akaun sumber strategi berdagang wang sebenar, bukan demo) ditambah yuran prestasi."
---

# Pasaran pembekal salinan (Fasa 4)

Direktori boleh-layari strategi salinan. Penyedia **menerbitkan** profil salinan sebagai penyenaraian dengan **lencana hidup-teresahkan** (akaun sumber strategi berdagang wang sebenar, bukan demo) ditambah yuran prestasi. Pengikut boleh-layari pasaran, disenaraikan oleh skor prestasi yang diproyeksikan dari data ketelusan pelaksanaan.

## Model

- `CopyProviderListing` = agregat: `UserId`, `ProfileId`, nama paparan, keterangan, yuran prestasi, `VerifiedLive`, `Published` + `PublishedAt`. Satu penyenaraian setiap profil (indeks unik).
- **Teresahkan-live** diperolehi pada masa publish dari `TradingAccount.IsLive` sumber — penyedia tidak boleh menegaskan sendiri.
- Statistik prestasi **tidak disimpan pada penyenaraian** — unjuran model baca melalui log ketelusan `CopyExecution` (kadar isi, purata kependaman, purata gelinciran realisasi), jadi pasaran sentiasa mencerminkan kualiti pelaksanaan langsung.

## Pemeringkatan

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → skor 0–100: kadar isi mendominasi (×60), kependaman rendah + gelinciran rendah menambah (×20 setiap), lencana hidup-teresahkan menambah bonus kepercayaan kecil. Deterministic + monotonik, jadi urutan stabil.

## API

- `POST /api/copy/profiles/{id}/publish` — terbitkan/kemaskini penyenaraian profil (`DisplayName`, `Description`, `PerformanceFeePercent`); hidup-teresahkan ditetapkan dari akaun sumber.
- `DELETE /api/copy/profiles/{id}/publish` — nyahterbit.
- `GET /api/copy/marketplace` — semua penyenaraian diterbitkan, disenaraikan, setiap dengan ringkasan prestasi (pelaksanaan, kadar isi, purata kependaman, purata gelinciran, skor) + lencana hidup-teresahkan.

## Ujian

- **Unit** (`CopyProviderListingTests`) — invariant agregat: nama paparan diperlukan; publish menetapkan cap waktu; nyahpublish sembunyikan; kemaskini gantian medan paparan + yuran + lencana.
- **Integrasi** (`CopyMarketplaceTests`, Postgres sebenar) — penyenaraian diterbitkan bertekak dengan lencana; satu penyenaraian setiap profil (indeks unik); pemeringkatan skor mengutamakan penyedia hidup-teresahkan/kadar-isi tinggi.

Hos salinan tidak disentuh (penyenaian + model baca sahaja), jadi suite tekanan DST salinan tidak terjejas.
