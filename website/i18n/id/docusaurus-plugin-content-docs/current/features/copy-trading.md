---
description: "Salin akun master cTrader ke satu atau lebih akun slave — lintas broker, lintas cID — dengan kontrol per-destinasi dan rekonsiliasi tingkat keuangan."
---

# Copy Trading

Salin akun master cTrader ke satu atau lebih akun slave — lintas broker, lintas cID — dengan kontrol per-destinasi dan rekonsiliasi tingkat keuangan.

## Konsep

- **Copy profile** — satu master (`SourceAccountId`) + satu atau lebih **destinasi**. Siklus hidup: `Draft → Running → Paused → Stopped` (`Error` jika gagal). Aggregate root: `CopyProfile` (memiliki `CopyDestination`).
- **Destination** — satu akun slave + seperangkat aturan lengkap untuk cara master disalin ke dalamnya. Semua konfigurasi per-destinasi, sehingga satu master dapat feeds ke slave konservatif dan agresif sekaligus.
- **Copy engine host** — worker yang menjalankan profile (`CopyEngineHost`). Berlangganan ke stream eksekusi master, menerapkan setiap event ke setiap destinasi.
- **Supervisor** — `CopyEngineSupervisor`, background service di setiap node. Host yang ditugaskan profile, self-heals lintas cluster (lihat [scaling](../deployment/scaling.md)).

## Apa yang Dicerminkan

| Event Master | Aksi Slave |
|--------------|------------|
| Market / market-range position open | Buka salinan berukuran (diberi label dengan source position id) |
| Limit / stop / stop-limit pending order | Tempatkan pending order yang sesuai |
| Pending order amend | Amend pending order yang di-mirror di tempat |
| Pending order cancel / expiry | Batalkan pending order yang di-mirror |
| Partial close | Tutup proporsi yang sama dari posisi slave |
| Scale-in (volume increase) | Buka volume tambahan (opt-in) |
| Stop-loss / trailing-stop change | Amend perlindungan posisi slave |
| Full close | Tutup salinan |

Setiap salinan **diberi label dengan source position/order id**. Setelah reconnect, host membangun ulang state dari reconcile: buka salinan untuk posisi master yang dipegang tetapi slave tidak punya, tutup slave "orphan" yang master tidak pegang lagi — **tanpa menduplikasi trades**.

## Membuat Profile

Dialog **New Profile** di halaman Copy Trading mengumpulkan semua upfront: nama profile, source (master) account, destination (slave) accounts (multi-select dengan tombol **Select all**; master yang dipilih dikecualikan dari daftar slave), + seperangkat opsi lengkap per-destinasi di bawah. Semua input **divalidasi sebelum menyimpan** — nama/source/destination yang hilang, parameter sizing non-positif, batas lot yang negatif/tidak konsisten, drawdown % di luar jangkauan, tidak ada order type yang diaktifkan, filter simbol kosong, atau pasangan symbol-map yang malformed akan muncul sebagai daftar error + blokir penyimpanan. Pada konfirmasi, profile dibuat + setiap slave yang dipilih ditambahkan dengan pengaturan yang dipilih.

Aksi baris menghormati siklus hidup: **Start** hanya aktif ketika tidak berjalan, **Stop** + **Pause** hanya aktif ketika berjalan, **Delete** dinonaktifkan ketika berjalan + meminta konfirmasi sebelum menghapus profile + destinasi.

## Opsi Per-Destinasi

Ditetapkan di dialog New Profile, di panel per-destinasi halaman Copy Trading, atau melalui `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot, lot/notional multiplier, proporsional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Ditambah batas lot min/max + force-min-lot. **Risk-from-stop** men-size destinasi sehingga berisiko persen yang dikonfigurasi dari *saldo miliknya sendiri*, berasal dari **jarak stop-loss master** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master buka **tanpa** stop-loss tidak punya jarak untuk di-size → menggunakan **max-risk fallback lot** (M7) jika diset, kalau tidak di-skip (`no_stop_loss`) bukan ditebak. Size proporsional-**equity**/**free-margin** dari **equity** account (`balance + Σ floating P&L`, berasal dari cTrader Open API yang tidak delivers equity), bukan plain balance — sehingga master yang duduk di profit/loss terbuka men-size salinan dengan benar. Used margin tidak diekspos oleh reconcile API, jadi free-margin diperlakukan sebagai equity (honest available-funds proxy); mode lain membaca balance + skip extra revaluation round-trip.
- **Direction filter**: both / long-only / short-only. **Reverse**: balik sisi (+ swap SL↔TP) untuk contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror menutup, partial closes + perubahan perlindungan pada posisi yang sudah di-copy, tapi **tidak buka** posisi/pending order baru (`manage_only`). Gunakan untuk wind-down destinasi tanpa memotong salinan yang ada.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): pada **pertama** resync profile, apakah akan membuka salinan untuk posisi master yang sudah ada sebelumnya, + apakah akan menutup salinan yang master tutup ketika profile berhenti. Keduanya hanya berlaku saat start — reconnect mid-run selalu mereconcile sepenuhnya sehingga desync pulih tanpa memandang.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Setiap entri symbol-map membawa optional **per-symbol volume multiplier** (cMAM per-symbol override) yang menyesuaikan ukuran copy untuk simbol tersebut di atas sizing destinasi (1 = tidak ada perubahan). Seluruh map dapat diimpor/ekspor sebagai **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; kolom `Source,Destination,VolumeMultiplier`) — setiap baris divalidasi melalui domain value objects, sehingga file malformed tidak dapat menghasilkan map tidak valid.
- **Trading-hours window** (C18) — window UTC harian per-destinasi (`start`/`end` menit-dari-hari, end exclusive; `start == end` = all-day). Buka baru di luar window di-skip (`trading_hours`); window dengan `start > end` wrapping melewati tengah malam (mis. 22:00–06:00). Posisi yang ada tetap dikelola.
- **Source-label filter** (C18, ekuivalen cTrader dari filter magic-number MT) — ketika diset, copy hanya trade master yang labelnya cocok **tepat** (mis. trade dari satu bot, atau label manual saja); kalau tidak di-skip (`source_label`). Kosong = copy semua. Dibawa pada `ExecutionEvent.SourceLabel` dari `TradeData.Label` posisi/order master, dihormati juga pada resync.
- **Account protection** (ZuluGuard / Global Account Protection) — pantau **live equity** destinasi (`balance + Σ floating P&L`, polled setiap `CopyDefaults.EquityGuardInterval`) terhadap `StopEquity` floor dan/atau `TakeEquity` ceiling opsional. Pada pelanggaran, apply mode: **CloseOnly** (stop salinan baru, tetap kelola yang ada), **Frozen** (stop pembukaan), **SellOut** (tutup **semua** salinan di destinasi segera). Setelah fire, destinasi latched — tidak ada buka baru sampai host restart — + alert `CopyAccountProtectionTriggered` raised. `SellOut` memerlukan `StopEquity`; `TakeEquity` harus di atas `StopEquity`. **No-guarantee caveat:** sell-out menggunakan market execution — seperti setiap kompetitor yang setara, tidak dapat menjamin harga fill di pasar yang cepat/gapped.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` segera menutup **setiap** posisi yang di-copy di setiap destinasi + lock terhadap buka baru. Routed cross-process: API set flag, supervisor deliver ke running host (reuse channel token-rotation), yang flatten di tempat; flag cleared sehingga fire exactly once (`CopyFlattenAll` alert). User kemudian pause/stop profile.
- **Prop-firm rule guard** (C7) — enforcement untuk pengguna prop-firm copier yang memintanya. Per destinasi, **daily-loss cap** (loss dari opening equity hari itu) dan/atau **trailing-drawdown** limit (loss dari peak equity berjalan), keduanya dalam deposit currency. Pada pelanggaran destinasi **auto-flattened** (setiap salinan ditutup) + **locked out** sisa UTC day (buka baru di-skip `prop_lockout`); alert `CopyPropRuleBreached` fire. Lockout clear ketika UTC day bergulir (fresh baseline/peak taken). Shares same live-equity poll sebagai account protection.
- **Execution jitter** (C11, off by default) — random `0..N` ms delay sebelum menempatkan setiap salinan, untuk de-correlate timestamp order yang hampir-identik lintas akun **milik** user. **Compliance caveat:** bantuan untuk prop firm yang *memungkinkan* copying — **bukan** alat untuk evade firm yang melarangnya; tetap dalam aturan firm Anda adalah tanggung jawab Anda.
- **Config lock** (C9) — freeze pengaturan destinasi untuk periode (`POST …/destinations/{id}/lock` dengan menit). Ketika locked, destinasi tidak dapat dihapus (aggregate reject dengan `CopyDestinationConfigLocked`) — deliberate guard terhadap perubahan impulsif saat drawdown. Lock expired secara otomatis di timestampnya.
- **Consistency pre-alert** (C10) — warn (sekali per UTC day) ketika **daily profit** destinasi mencapai persen yang dikonfigurasi dari opening equity hari itu (`CopyConsistencyThresholdApproaching`), sehingga aturan consistency prop-firm dihormati *sebelum* terpicu. Profit-side, independen dari loss-side lockout; runs off same day baseline sebagai prop-rule guard.
- **Order-type filter** — pilih persis jenis order master mana yang akan di-copy: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; default semua). Selectivity gaya cMAM.
- **Copy SL / Copy TP** — mirror stop-loss / take-profit master, atau kelola perlindungan secara independen.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — masing-masing dapat di-toggle secara independen.
- **Copy pending expiry** (default on) — mirror timestamp expiry Good-Till-Date dari pending order master.
- **Copy master slippage** (default on) — untuk market-range + stop-limit orders, tempatkan order slave dengan slippage master yang tepat dalam poin (base price diambil dari spot live slave).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy jika harga slave bergerak melampaui N pips dari entry master). **Max copy delay** diukur terhadap real server timestamp event master (`ExecutionEvent.ServerTimestamp`) melalui `TimeProvider` yang di-inject: signal yang lebih lama dari max-lag yang dikonfigurasi di-skip, sehingga stale copy tidak pernah ditempatkan terlambat (sebelumnya delay selalu nol + guard mati).
- **SL/TP precision normalization** (M6) — stop-loss/take-profit yang di-copy dibulatkan ke **digit precision** simbol destinasi sebelum amend, sehingga harga master dengan precision lebih halus (atau ketidakcocokan digit lintas broker) tidak pernah memicu `INVALID_STOPLOSS_TAKEPROFIT` server.
- **Rejection circuit breaker / Follower Guard** (G8) — destinasi yang menolak `CopyDefaults.RejectionBudget` opens berturut-turut adalah **tripped**: tidak ada buka baru untuk cooldown window (alert `CopyDestinationTripped` fire), menghentikan rejection storm yang hammering (prop-firm) account. Posisi yang ada tetap dikelola + ditutup sementara tripped; breaker auto-resets setelah cooldown + copy berhasil clear counter.
- **Lot sanity ceiling** (C14) — max absolute copy size dan/atau multiple-of-master cap. Copy yang di-compute melebihi absolute cap, atau melebihi `N×` ukuran lot master sendiri, **hard-blocked** (muncul sebagai skip `lot_sanity`, dihitung pada `cmind.copy.skipped`) tidak ditempatkan — defends against catastrophic-oversize class (0.23-lot master berubah menjadi 3 lots di setiap receiver melalui runaway multiplier atau rounding bug). Kedua dimensi default `0` (off).

## Reliability & Edge Cases

Engine dibangun untuk kenyataan bahwa apapun bisa gagal kapan saja:

- **Slave-pending fill-correlation timeout** (C13) — slave pending yang di-mirror whose master pending menghilang (neither resting nor freshly filled) dibatalkan setelah correlation timeout, sehingga slave copy tidak dapat fill tidak berkorelasi ke posisi yang tidak dikelola (`CopyPendingTimedOut`). Resync juga membersihkan filled-pending orphan bert.label.
- **Robust close/flatten** (M8) — menutup orphan pada resync, atau flatten pada pelanggaran guard, tolerates posisi broker sudah ditutup (`POSITION_NOT_FOUND`): setiap close berjalan independen, sehingga satu stale id tidak pernah abort resync atau meninggalkan sisa account un-flattened.

- **Start dengan master sudah di trades** — pada start host mereconcile + buka salinan untuk posisi master yang ada.
- **Connection drops / desync** — pada reconnect host mereconcile: buka salinan yang hilang, tutup orphan, re-label pendings. Tidak ada order duplikat.
- **Order placement failure** — failure pada satu destinasi di-log, tidak pernah memblokir destinasi lain.
- **Single valid token per cID** — cTrader membatalkan access token lama cID di momen token baru diterbitkan. cMind menukar token running host **in place** (re-auth pada live socket) sehingga copying berlanjut tanpa menjatuhkan stream. Lihat [token lifecycle](token-lifecycle.md).

## Auditability

Setiap aksi memancarkan structured, source-generated log event (`LogMessages`) dengan profile id, destinasi cID, order/position ids, + values — order placed/skipped (dengan alasan), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored, market-range slippage mirrored, token swapped, resync summary. Ini adalah audit trail untuk compliance + dispute resolution.

Bersamaan dengan logs, engine memancarkan **OpenTelemetry metrics** pada `cMind.Copy` meter (terdaftar di shared OTel pipeline, diekspor via OTLP / ke Azure Monitor seperti rest): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out ke semua destinasi, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged by reason), + `cmind.copy.failed`. Ini membuat latency/slippage regression terukur, bukan hanya terlihat di log line — live suite asserts mereka terhadap budget.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — create (dengan optional destination account ids).
- `GET /api/copy/profiles/{id}` — detail lengkap incl. setiap opsi destinasi.
- `POST /api/copy/profiles/{id}/destinations` — tambahkan destinasi dengan seperangkat opsi lengkap.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — hapus.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — siklus hidup.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Runs against `FakeTradingSession`, simulator in-memory yang faithful terhadap cTrader.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation on real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip melalui API + UI, siklus hidup lengkap.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` ke quiescence + assert convergence invariants. Lihat [testing/stress-testing.md](../testing/stress-testing.md). Suite ini surfaced + fixed real startup race: `OnReconnected` wired sebelum initial reference-load + resync, sehingga socket flap during startup bisa run second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync now run under `_stateGate`.
- **Live** — real cTrader demo accounts; lihat [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Lihat [dev-credentials.md](../testing/dev-credentials.md) untuk file kredensial tunggal live + E2E tiers read.

## Profile Controls dan Destination Management

Start/stop adalah tombol ikon pada setiap baris profile (dinonaktifkan ketika aksi tidak berlaku). Source dan
destination accounts ditampilkan dengan **account number**, bukan internal id. Mengklik profile
membuka **dialog** untuk mengelola akun destinasi (add/remove dengan pengaturan per-destinasi lengkap).
