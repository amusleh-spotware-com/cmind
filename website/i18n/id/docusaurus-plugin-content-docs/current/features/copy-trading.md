---
description: "Mirror akun cTrader master ke satu+ akun slave — cross-broker, cross-cID — dengan kontrol per-tujuan + rekonsiliasi money-grade."
---

# Copy trading

Mirror akun cTrader **master** ke satu+ akun **slave** — cross-broker, cross-cID — dengan kontrol per-tujuan + rekonsiliasi money-grade.

## Konsep

- **Copy profile** — satu master (`SourceAccountId`) + satu+ **destinations**. Lifecycle: `Draft → Running → Paused → Stopped` (`Error` pada kegagalan). Aggregate root: `CopyProfile` (memiliki `CopyDestination`).
- **Destination** — satu akun slave + ruleset lengkap untuk cara master dikopi ke dalamnya. Semua config per-destination, jadi satu master memberi makan slave konservatif + agresif secara bersamaan.
- **Copy engine host** — running worker untuk profile (`CopyEngineHost`). Berlangganan stream eksekusi master, menerapkan setiap event ke setiap destination.
- **Supervisor** — `CopyEngineSupervisor`, layanan latar belakang pada setiap node. Host profile yang ditugaskan, self-heals di seluruh cluster (lihat [scaling](../deployment/scaling.md)).

## Apa yang dicerminkan

| Event master | Aksi slave |
|--------------|--------------|
| Posisi open pasar / market-range | Buka copy berukuran (berlabel dengan id posisi sumber) |
| Pending order limit / stop / stop-limit | Tempatkan matching pending order |
| Amend pending order | Amend mirrored pending order di tempat |
| Batalkan pending order / expiry | Batalkan mirrored pending order |
| Partial close | Tutup proporsi yang sama dari posisi slave |
| Scale-in (volume increase) | Buka volume yang ditambahkan (opt-in) |
| Stop-loss / trailing-stop change | Amend perlindungan posisi slave |
| Full close | Tutup copy slave |

Setiap copy **diberi label dengan id posisi/order sumber**. Setelah reconnect host membangun kembali state dari reconcile: membuka copy master yang dipegang tetapi slave hilang, menutup slave "orphans" master tidak lagi pegang — **tanpa menduplikasi trades**.

## Membuat profile

Dialog **New Profile** pada halaman Copy Trading mengumpulkan semuanya di awal: nama profile, source (master) account, destination (slave) accounts (multi-select dengan tombol **Select all**; master yang dipilih dikecualikan dari daftar slave), + option set per-destination lengkap di bawah. Semua input **divalidasi sebelum menyimpan** — nama/source/destination yang hilang, sizing param non-positif, lot bounds negatif/inkonsisten, drawdown % out-of-range, tidak ada tipe order yang diaktifkan, symbol filter kosong, atau malformed symbol-map pairs muncul sebagai daftar error + blok save. Pada confirm, profile dibuat + setiap slave yang dipilih ditambahkan dengan pengaturan yang dipilih.

Row actions menghormati lifecycle: **Start** diaktifkan hanya ketika tidak berjalan, **Stop** + **Pause** hanya ketika berjalan, **Delete** dinonaktifkan saat berjalan + meminta konfirmasi sebelum menghapus profile + destinations.

## Opsi per-destination

Setel dalam dialog New Profile, di panel per-destination halaman Copy Trading, atau via `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot, lot/notional multiplier, proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Plus min/max lot bounds + force-min-lot. **Risk-from-stop** ukuran destination sehingga risiko percent yang dikonfigurasi dari *keseimbangannya sendiri*, berasal dari **master's stop-loss distance** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master open **tanpa** stop-loss tidak memiliki jarak untuk mengukur terhadap → menggunakan **max-risk fallback lot** yang dikonfigurasi (M7) jika diatur, else dilewati (`no_stop_loss`) tidak ditebak. Proportional-**equity**/**free-margin** size dari **equity** akun nyata (`balance + Σ floating P&L`, berasal per cTrader Open API yang tidak memberikan equity), bukan plain balance — jadi master duduk di open profit/loss ukuran copy dengan benar. Used margin tidak diekspos oleh reconcile API, jadi free-margin diperlakukan sebagai equity (proxy available-funds jujur); mode lain baca balance + lewati extra revaluation round-trip.
- **Direction filter**: both / long-only / short-only. **Reverse**: balik side (+ swap SL↔TP) untuk contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes, partial closes + perubahan protection pada sudah-copied positions, tetapi open **no** posisi/pending orders baru (dilewati `manage_only`). Gunakan untuk menggulung destination tanpa memotong copy yang ada.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): pada **pertama kali** profile's **resync**, apakah membuka copy untuk master's pre-existing positions, + apakah menutup copy master ditutup saat profile dihentikan. Keduanya hanya berlaku pada start — mid-run reconnect selalu reconcile fully jadi desync pulih terlepas.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Setiap entri symbol-map membawa opsional **per-symbol volume multiplier** (cMAM per-symbol override) scaling copy size untuk symbol itu pada destination's sizing (1 = no change). Seluruh map import/export sebagai **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; kolom `Source,Destination,VolumeMultiplier`) — setiap baris divalidasi melalui domain value objects, jadi file malformed tidak dapat menghasilkan invalid map.
- **Trading-hours window** (C18) — per-destination daily UTC window (`start`/`end` minutes-of-day, end exclusive; `start == end` = all-day). Pembukaan baru di luar window dilewati (`trading_hours`); window dengan `start > end` membungkus past midnight (misalnya 22:00–06:00). Posisi yang ada tetap dikelola.
- **Source-label filter** (C18, cTrader equivalent MT magic-number filter) — ketika diatur, copy hanya master trades yang label sesuai **exactly** (misalnya satu bot's trades, atau manual-only label); else dilewati (`source_label`). Kosong = copy semua. Dibawa pada `ExecutionEvent.SourceLabel` dari master position/order's `TradeData.Label`, dihormati pada resync juga.
- **Account protection** (ZuluGuard / Global Account Protection) — tonton **live equity** destination (`balance + Σ floating P&L`, polled setiap `CopyDefaults.EquityGuardInterval`) terhadap lantai `StopEquity` dan/atau opsional ceiling `TakeEquity`. Pada breach, terapkan mode: **CloseOnly** (stop pembukaan baru, keep managing existing), **Frozen** (stop opening), **SellOut** (tutup **setiap** copy pada destination segera). Sekali fired, destination latched — tidak ada pembukaan baru hingga host restart — + alert `CopyAccountProtectionTriggered` dinaikkan. `SellOut` memerlukan `StopEquity`; `TakeEquity` harus duduk di atas `StopEquity`. **Caveat tidak ada jaminan:** sell-out menggunakan eksekusi pasar — seperti setara setiap kompetitor, tidak dapat menjamin harga fill dalam pasar cepat/gapped.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` segera menutup **setiap** copied position pada setiap destination + mengunci terhadap pembukaan baru. Dirutekan cross-process: API menetapkan flag, supervisor mengirim ke running host (menggunakan kembali channel token-rotation), yang meratakan di tempat; flag dihapus jadi fires exactly once (`CopyFlattenAll` alert). Pengguna kemudian pause/stop profile.
- **Prop-firm rule guard** (C7) — penegakan copier pengguna prop-firm minta. Per destination, **daily-loss cap** (loss dari day's opening equity) dan/atau **trailing-drawdown** limit (loss dari running peak equity), keduanya dalam currency deposit. Pada breach destination **auto-flattened** (setiap copy ditutup) + **locked out** sisa UTC day (pembukaan baru dilewati `prop_lockout`); alert `CopyPropRuleBreached` fires. Lockout clears ketika UTC day rolls over (fresh baseline/peak taken). Berbagi same live-equity poll sebagai account protection.
- **Execution jitter** (C11, off by default) — random `0..N` ms delay sebelum menempatkan setiap copy, untuk de-correlate near-identical order timestamps di seluruh **own** accounts pengguna. **Compliance caveat:** aid untuk prop firms yang *permit* copying — **bukan** tool untuk evade firm yang forbids it; staying dalam rule firm Anda adalah tanggung jawab Anda.
- **Config lock** (C9) — freeze destination's settings untuk periode (`POST …/destinations/{id}/lock` dengan menit). Saat locked, destination tidak dapat dihapus (aggregate rejects dengan `CopyDestinationConfigLocked`) — deliberate guard terhadap impulsive changes selama drawdown. Lock expires otomatis pada timestamp-nya.
- **Consistency pre-alert** (C10) — warn (once per UTC day) ketika destination's **daily profit** mencapai percent yang dikonfigurasi dari day's opening equity (`CopyConsistencyThresholdApproaching`), jadi prop-firm consistency rule dihormati *sebelum* trips. Profit-side, independent dari loss-side lockout; runs off same day baseline sebagai prop-rule guard.
- **Order-type filter** — pilih exactly master order types mana yang dikopi: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; default all). cMAM-style selectivity.
- **Copy SL / Copy TP** — mirror master's stop-loss / take-profit, atau manage protection secara independen.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — masing-masing toggleable secara independen.
- **Copy pending expiry** (default on) — mirror master pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (default on) — untuk market-range + stop-limit orders, tempatkan slave order dengan exact master's slippage-in-points (base price diambil dari slave's live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy jika slave price bergerak beyond N pips dari master entry). **Max copy delay** diukur terhadap master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) via injected `TimeProvider`: signal lebih tua dari configured max-lag dilewati, jadi stale copy tidak pernah ditempatkan terlambat (sebelumnya delay selalu zero + guard dead).
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices dibulatkan ke **destination** symbol's digit precision sebelum amend, jadi master price pada precision lebih halus (atau cross-broker digit mismatch) tidak pernah trips server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — destination menolak `CopyDefaults.RejectionBudget` opens berturut-turut adalah **tripped**: tidak ada pembukaan baru untuk cooldown window (`CopyDestinationTripped` alert fires), menghentikan rejection storm dari hammering (prop-firm) account. Posisi yang ada masih managed + ditutup saat tripped; breaker auto-resets setelah cooldown + successful copy clears counter.
- **Lot sanity ceiling** (C14) — absolute max copy size dan/atau multiple-of-master cap. Computed copy exceeding absolute cap, atau exceeding `N×` master's own lot size, **hard-blocked** (surfaced sebagai `lot_sanity` skip, dihitung pada `cmind.copy.skipped`) tidak ditempatkan — mempertahankan terhadap catastrophic-oversize class (0.23-lot master berubah menjadi 3 lots pada setiap receiver via runaway multiplier atau rounding bug). Kedua dimensi default `0` (off).

## Keandalan & edge cases

Engine dibangun untuk kenyataan bahwa apa pun dapat gagal kapan saja:

- **Slave-pending fill-correlation timeout** (C13) — mirrored slave pending yang master pending hilang (baik resting maupun newly filled) dibatalkan setelah correlation timeout, jadi slave copy tidak dapat fill uncorrelated menjadi unmanaged position (`CopyPendingTimedOut`). Resync juga membersihkan order-id-labelled filled-pending orphan.
- **Robust close/flatten** (M8) — closing orphan pada resync, atau flattening pada guard breach, tolerates position broker sudah ditutup (`POSITION_NOT_FOUND`): setiap close berjalan secara independen, jadi satu stale id tidak pernah membatalkan resync atau leaves rest dari account un-flattened.

- **Start dengan master sudah dalam trades** — pada start host reconciles + membuka copy untuk master's existing positions.
- **Connection drops / desync** — pada reconnect host reconciles: opens missing copies, closes orphans, re-labels pendings. Tidak ada duplicate orders.
- **Order placement failure** — kegagalan pada satu destination logged, tidak pernah blocks destination lain.
- **Single valid token per cID** — cTrader invalidates cID's old access token moment new one issued. cMind swaps running host's token **in place** (re-auth pada live socket) jadi copying continues tanpa dropping stream. Lihat [token lifecycle](token-lifecycle.md).

## Auditability

Setiap aksi emits structured, source-generated log event (`LogMessages`) dengan profile id, destination cID, order/position ids, + values — order placed/skipped (dengan reason), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored, market-range slippage mirrored, token swapped, resync summary. Ini adalah audit trail untuk compliance + dispute resolution.

Alongside logs, engine emits **OpenTelemetry metrics** pada `cMind.Copy` meter (registered dalam shared OTel pipeline, exported over OTLP / ke Azure Monitor seperti rest): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out ke semua destinations, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged by reason), + `cmind.copy.failed`. Ini membuat latency/slippage regression measurable, tidak hanya visible dalam log line — live suite asserts them terhadap budget.

## API

- `GET /api/copy/profiles` — daftar.
- `POST /api/copy/profiles` — buat (dengan opsional destination account ids).
- `GET /api/copy/profiles/{id}` — detail lengkap incl. setiap destination option.
- `POST /api/copy/profiles/{id}/destinations` — tambahkan destination dengan option set lengkap.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — hapus.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Runs terhadap `FakeTradingSession`, cTrader-faithful in-memory simulator.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation pada real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip melalui API + UI, full lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` ke quiescence + assert convergence invariants. Lihat [testing/stress-testing.md](../testing/stress-testing.md). Suite ini surfaced + fixed real startup race: `OnReconnected` wired sebelum initial reference-load + resync, jadi socket flap selama startup bisa run second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync sekarang run under `_stateGate`.
- **Live** — real cTrader demo accounts; lihat [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Lihat [dev-credentials.md](../testing/dev-credentials.md) untuk single credentials file live + E2E tiers read.
