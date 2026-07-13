---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = dalam-memori IOpenApiTradingSession semua ujian unit salinan perdagangan dijalankan terhadap. Kerja: tirukan pelayan Open API cTrader cukup dekat supaya ujian unit menutup kelakuan yang hanya paras langsung pernah tangkap."
---

# FakeTradingSession — kontrak kesetiaan Open API cTrader

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = dalam-memori `IOpenApiTradingSession` semua ujian unit salinan perdagangan dijalankan terhadap. Kerja: tirukan **pelayan Open API cTrader** cukup dekat supaya ujian unit melindungi kelakuan yang hanya paras langsung pernah tangkap. Dokumen ini = kontrak kesetiaan: apa yang palsu моделирует, bagaimana dengan setia, dan aturan menjaga nó jujur.

> **Peraturan mengikat (CLAUDE.md):** palsu kekal setia kepada cTrader. **Perluasnya, tidak pernah melemahkannya** untuk melepaskan ujian. Setiap kelakuan sebenar baharu yang anda andalkan моделируется di sini, dipancangkan oleh ujian kesetiaan.

## Matriks kesetiaan (F1–F13)

Menjejaki pelan `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ моделируется · ◑ separa (pilihan masuk / diperluaskan) · ⬜ belum dimodelkan.

| # | Kelakuan Open API sebenar | Status palsu | Bagaimana nó моделируется |
|---|------------------------|-------------|------------------------|
| F1 | Pesanan pasaran boleh **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` mengisi hanya `f×volume`; reconcile kemudian menunjukkan gap Fasa-1 true-up (G5) menutup. Pasangan acara Accept→fill masih belum datang. |
| F2 | Volum dinormalisasi kepada **langkah**, ditolak bawah **min** / atas **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` bundar ke bawah kepada langkah, membaling `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **SL/TP tidak sah** ditolak (sisi + digit) | ⬜ | Dirancang Fasa 0a/1 (berpasangan dengan M6 normalisasi presisi SL/TP). |
| F4 | Harga **diskalakan integer oleh digit**; `pipPosition` | ◑ | `SymbolDetails` sekarang membawa `Digits` (dan `MaxVolume`), dipopulate dari simbol sebenar; `PipPosition` menggerakkan toleransi julat pasaran, `Digits` menggerakkan normalisasi presisi SL/TP (M6). Penskalaan harga integer penuh masih tertunda. |
| F5 | **Julat pasaran** mengisi hanya jika spot dalam `base ± slippage`, jika tidak ditolak | ✅ | `IsMarketRangeRejected` membandingkan spot langsung (`SetSpot`) kepada `baseSlippagePrice ± slippageInPoints`. Tanda `RejectMarketRangeForCtid` legacy masih memaksa penolakan. |
| F6 | **Trigger→isi tertunda** dwi acara (Order membawa `positionId` + Position OPEN) | ◑ | `PushOpen(..., orderId:)` mereproduksi acara tertunda-diisi; FX‑Blue/cMAM salinan dedup diliputi dalam `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Tutup yang diarahkan pelayan** (SL/TP tecetus, stop-out) | ⬜ | Hari ini tutup ujian-d tolak (`PushClose`); SL/TP harga-cetus + tutup stop-out dirancang. |
| F8 | **Setiap akaun** jadual/butiran simbol | ◑ | Nama/id simbol setiap palsu; jadual menyimpang setiap akaun (lintas-broker) tertunda. |
| F9 | **Keadaan akaun penuh** (baki, ekuiti, margin, margin bebas) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission melalui `SetPositionValuation`) + `SetSpot` makan ekuiti langsung ke dalam saiz ekuiti berkadaran (G2, diuji unit dalam `CopyEquitySizingTests`). Margin yang digunakan tidak didedahkan oleh API reconcile, jadi margin bebas dilaporkan sebagai ekuiti. |
| F10 | Acara membawa **cap waktu pelayan** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — sesi sebenar membaca dari `ExecutionTimestamp` deal; `PushOpen`/`PushPending` terima `serverTimestamp:` supaya `FakeTimeProvider`-driven ujian mengemudi salinan kependaman sebenar (G1). |
| F11 | **Mod/s.jadual perdagangan** (dilumpuhkan / tutup sahaja / ditutup) | ⬜ | Dirancang Fasa 2b. |
| F12 | **Taksonomi ralat taip** (`ProtoOAErrorRes` kod) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` membaling `CtraderRejectException(reason)` sekali (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Pembatalan token** — token basi → ralat auth | ✅ | `InvalidateToken(ctid)` menanda token yang dilampirkan basi; panggilan perdagangan membaling `OpenApiException` sebenar dengan `OpenApiErrorKind.TokenInvalid` (kod `CH_ACCESS_TOKEN_INVALID`), tepat seperti pelayan sebenar, sehingga `SwapAccessTokenAsync` memasang token baharu. Memakan ujian keteguhan token M1. |

Ujian kesetiaan tinggal dalam `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Pilihan masuk, lalai mengekalkan kelakuan legacy

Setiap knob kesetiaan **off secara lalai** supaya palsu mengekalkan kelakuan isi yang mudah untuk ujian yang tidak peduli. Ujian pilihan masuk setiap akaun:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (sekali)
session.InvalidateToken(slave);                                             // F13
```

## Karakterisasi + conformance (dirancang, menjaga palsu ≡ sebenar)

Dua mekanism mengekalkan palsu jujur terhadap pelayan sebenar yang bergerak (jejak, mendarat merentasi Fasa 0a):

1. **Karakterisasi langsung** (`LiveApiCharacterization`, akaun demo, gerbang kredensi, `Inconclusive` pada pasaran tutup): menghidupkan Open API sebenar, merekodkan realiti wire yang tepat (jujukan acara, penskalaan, kod penolakan) ke dalam fixture emas yang diperiksa masuk ke projek ujian. Tiada rahsia dalam fixture — hanya bentuk yang diperhatikan.
2. **Harness conformance**: Melarikan *suites* yang sama dua kali — sekali terhadap `FakeTradingSession`, sekali terhadap sesi langsung (apabila kredensi hadir) — menegaskan hasil yang boleh dilihat sama. Pelayan sebenar berubah → kaki live gagal → kemas kini palsu. Ini menjadikan "ujian unit melindungi segalanya" boleh dipercayai.

Kredensi langsung: `secrets/dev-credentials.local.json` (atau fail legacy split) — lihat `docs/testing/dev-credentials.md`.
