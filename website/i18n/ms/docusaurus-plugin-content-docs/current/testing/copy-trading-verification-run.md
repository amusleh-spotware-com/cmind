---
description: "Pengesahan penuh kerja salinan perdagangan yang tinggal — kesemua di bawah sebenarnya dilaksanakan, bukan sahaja ditulis."
---

# Larian pengesahan salinan perdagangan (2026-07-10)

Pengesahan penuh kerja salinan perdagangan yang tinggal — kesemua di bawah **sebenarnya dilaksanakan**, bukan sahaja ditulis.

## Langsung (akaun demo cTrader sebenar) — 8/8 lulus
1:1 · 1:banyak · songsang · lintas-cID · partial-close · **pending limit + batal** · **trailing stop** · token-refresh.
Senario langsung ditambahkan `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integrasi (Postgres sebenar, Testcontainers) — lulus
- `CopyNodeAffinityTests` — tuntutan atomik penyelia sebenar: nod pertama menuntut semua profil berjalan, nod kedua menuntut **0** (tiada salinan berganda); jeda melepaskan + exigam.
- `TokenRotationSignatureTests` — tandatangan berubah hanya pada token sebenar berputar.

## Dalam-kluster (kind + Helm) — lulus
`kind`/`kubectl`/`helm` dipasang, lari `scripts/k8s-e2e.sh` terhadap kluster kind sebenar:
- **Kerja Deterministik: 101 lulus** dalam-kluster.
- **Kerja Langsung: 8 lulus** dalam-kluster (init-container `seed-secrets` menyalin Rahsia → emptyDir boleh tulis, akaun demo sebenar).
- Kerja `Complete 1/1`, skrip keluar 0.

## Pepijat ditemui semasa pengesahan (diperbaiki + disahkan semula)
- **Peristiwa yang belum selesai**: cTrader melampirkan *placeholder Position bukan terbuka* pada `ORDER_ACCEPTED`/`CANCELLED` untuk had/dog hold yang melanggar. `SourceExecutionsAsync` sekarang mengklasifikasikan penempatan/batal pesanan sebagai peristiwa pesanan sebelum cawangan posisi, tetapi membiarkan had/dog hold *fill* (cth. stop-loss mencetuskan tutup) jatuh melalui ke laluan tutup.
- **Token penyegaran sekali-guna**: cTrader memutar token penyegaran setiap penyegaran. Cache baca sahaja yang tidak boleh mengekalkan membatalkan dirinya. Kerja K8s langsung therefore menyalin Rahsia ke **emptyDir boleh tulis**; kerja lalai kepada suite deterministik. `SaveTokens` sekarang usaha terbaik. Simbol langsung dipaksa kepada FX (BTCUSD trailing membaiki broker yang ditolak).
- Penamaan imej skrip dibetulkan untuk match Helm `registry/repository` split + `pullPolicy=Never`.

## Program advanced mirroring + kitaran hayat token + penskalaan (2026-07-10) — lapisan deterministik lulus

Program susulan menambah penapisan jenis pesanan, salinan tamat tempoh pesanan tertunda, slippage julat pasaran / stop-limit, SL/TP salinan Togol, pertukaran token di tempat yang aman (satu token sah setiap cID), simulator yang setia kepada cTrader, lesen nod penyembuhan diri, fail kredensi dev yang disatukan.

- **Unit — 210 lulus** (`dotnet test tests/UnitTests`). Perlindungan salinan baharu: penapis jenis pesanan (buka + tertunda), slippage mirror julat pasaran + harga asas, tamat tempoh salinan atas/matikan, slippage stop-limit, amend tertunda, buka-dengan-master-terbuka, putuskan→master-dagang→sambung semula resync
  (buka hilang + tutup orphan), pertukaran token di tempat (tiada restart), pembatalan lintas-cID,
  invariant domain, pemilikan lesen, bumbunan versi token.
- **Integrasi (Postgres sebenar, Testcontainers) — lulus**: `CopyNodeAffinityTests` (tuntutan atomik,
  tiada salinan berganda, pelepasan jeda, **lesen luput dituntut oleh nod lain**),
  `TokenRotationSignatureTests` (tandatangan berubah pada bumbunan versi token),
  `OpenApiAuthorizationPersistenceTests` (TokenVersion bertekak + meningkat pada penyegaran).
- **E2E** (`tests/E2ETests`): pusingan pilihan destinasi sekarang menegaskan penapis jenis pesanan,
  salinan-tamat, slippage seiring dengan kitaran hayat penuh.
- **Bina**: bersih di bawah `TreatWarningsAsErrors`; `get_file_problems` bersih pada fail yang diubah.

Senario langsung (akaun demo cTrader sebenar) untuk stop-tertunda, julat pasaran, tamat, buka-dengan-terbuka,
putaran token tengah-lari ditulis terhadap enjin yang sama; lari dengan `secrets/dev-credentials.local.json` berpadu
mengikut [dev-credentials.md](dev-credentials.md).

## Tindak lanjut yang diketahui
Jalankan semula secara langsung Kluster live Token sekali-guna berputar; regenerate cache tempatan dengan
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`
(cTrader mengekang halaman OAuth mereka sebaik sahaja lari — cuba semula apabila jelas).
