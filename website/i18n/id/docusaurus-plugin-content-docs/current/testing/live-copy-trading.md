---
description: "Suite test copy-trading lengkap yang dapat direproduksi. Dua lapisan: test deterministik (tanpa jaringan) dan test E2E langsung (akun demo nyata)."
---

# Suite test copy-trading (deterministik + langsung)

Suite test copy-trading lengkap yang dapat direproduksi. Dua lapisan:

1. **Test deterministik** (xUnit, tanpa jaringan) — matematika copy + logika engine. Cepat, CI, tidak ada rahasia. Mencakup setiap mode manajemen uang, setiap filter/Opsi, ketahanan engine.
2. **Test E2E langsung** (akun demo cTrader nyata) — `CopyEngineHost` produksi menempatkan + menyalin order nyata antara akun nyata. Sepenuhnya otomatis, dapat dijalankan ulang seperti test unit: baca kredensial yang di-cache dari file lokal yang di-gitignore, auto-refresh access token, skip rapi saat rahasia tidak ada (CI tetap hijau).

Tidak pernah berjalan melawan akun funded langsung — setiap akun **demo**, setiap test langsung menutup posisi yang dibukanya.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — setiap mode sizing + pembulatan + lot min/max
  CopyDecisionEngineTests.cs     — arah/balek/slippage/delay/filter simbol/size-nol
  CopyEngineHostTests.cs         — logika copy host terhadap sesi fake in-memory
  FakeTradingSession.cs          — IOpenApiTradingSession deterministik (merekam order/close/amend)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fault fatal (ketahanan)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — memuat rahasia dari gitignore, menyimpan token yang di-refresh
  LiveTokenBootstrapTests.cs     — sekali jalan: dekrip token dari app DB ke token cache
  LiveCopyFixture.cs             — merotasi access token, memapar daftar akun demo
  LiveCopyScenario.cs            — menjalankan satu skenario copy nyata end-to-end (buka → copy → verifikasi → bersihkan)
  CopyTradingLiveTests.cs        — skenario langsung (1:1, 1:banyak, balik, …)
```

## Rahasia (lokal, gitignore — tidak pernah di-commit)

Semua kredensial di `<repo>/secrets/` (sudah ada di `.gitignore`). Dev menulis **hanya dua file pertama**; file ketiga (token) diproduksi otomatis oleh onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — kredensial login cID untuk authorize (satu atau banyak):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **ditulis oleh onboarding**, multi-cID, di-refresh setiap jalan:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Refresh token **tidak pernah kedaluwarsa**, jadi setelah onboarding sekali jalan test copy langsung berjalan selamanya: setiap jalan menukar refresh token setiap cID untuk access token baru (rotasi) — tidak ada browser, tidak ada prompt.

## Onboarding sekali jalan (sepenuhnya otomatis — tidak ada interaksi dev selain menyimpan kredensial)

Onboarding mengemudi login cTrader ID nyata di headless browser dari kredensial cID tersimpan, menangkap OAuth callback di local HTTPS listener pada redirect terdaftar app (`https://localhost:7080/openapi/callback`), menukar kode untuk token, memuat daftar akun, menulis token cache multi-cID. Jalan sekali per mesin (atau saat menambah cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Mengotorisasi setiap cID di `openapi-cids.local.json`, menulis `openapi-tokens.local.json`. Setelah itu test copy langsung tidak membutuhkan apa-apa lagi. (Akun cTrader ID harus tidak memiliki 2FA/captcha saat login agar otomasi selesai.)

**Bootstrap alternatif** (jika akun sudah diotorisasi di app yang berjalan): dekrip token tersimpan langsung dari volume Postgres app alih-alih mengotorisasi ulang:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Keamanan — demo saja

Test langsung hanya trading **akun demo**: fixture menyaring token cache ke akun dengan `IsLive == false` dan menghubungkan ke gateway demo, sehingga order tidak pernah mendarat di akun funded langsung meskipun akun langsung diotorisasi. Setiap posisi yang dibuka test ditutup di cleanup.

## Menjalankan

```bash
# Test copy deterministik saja (cepat, tanpa rahasia, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Test copy langsung terhadap akun demo nyata (membutuhkan dua file rahasia)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Semua
dotnet test
```

Tanpa file rahasia, test langsung mencetak alasan skip + pass sebagai no-op, jadi suite aman dijalankan di mana saja.

## Cakupan

### Manajemen uang / sizing (deterministik — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** dan **down** untuk ketidaksesuaian balance/leverage/kapasitas (the "golden rule") · pembulatan
lot-step · skip lot-min vs force-to-min · cap lot-max · batas yang lebih ketat dari bound-vs-spec min & max · skip
master balance nol.

### Filter keputusan (deterministik — `CopyDecisionEngineTests`)
Allowlist/denylist simbol · LongOnly / ShortOnly · balik membalik sisi efektif ·
slippage over limit skip + tepat-di-limit diizinkan · skip sinyal staleness (max delay) · skip size-nol ·
reconnect reconciliation (dedupe open-missing, close-orphaned).

### Copy engine host (deterministik — `CopyEngineHostTests`, sesi in-memory)
Open mencerminkan market order (sisi / volume / label) · **reverse** membalik sisi dan ** menukar SL/TP** ·
**symbol mapping** menyelesaikan simbol tujuan · **order-gagal pada satu slave tetap menyalin ke yang lain** · source
close menutup salinan yang di-mirror · reconnect resync menutup salinan orphan.

### Ketahanan koneksi (deterministik — `OpenApiConnectionTests`)
Mencapai Connected setelah auth app · koneksi drop reconnects dan re-auths · fatal auth error faults ·
exponential backoff.

### Langsung, akun demo cTrader nyata (`CopyTradingLiveTests`)
Token refresh + listing akun · copy **1:1** dieksekusi · copy **1:banyak** memirror ke setiap slave ·
**reverse** mengubah master buy menjadi slave sell · **cross-cID** copy (master di bawah satu cID memirror ke slave di bawah cID lain, masing-masing mengautentikasi dengan token sendiri). Setiap membuka posisi lot min nyata di master, menunggu engine memirror (dicocokkan oleh source-position-id label di slave),断言, menutup segalanya. Pasar yang tutup dilaporkan **Inconclusive**, bukan gagal.

## Logging & auditabilitas

Setiap operasi copy trading di-log melalui event terstruktur source-generated (`Core/Logging/LogMessages.cs`, event ID 1043–1055), jejak penuh dapat di-audit:

| Event | Id | Arti |
|-------|----|------|
| CopyHostStarted | 1046 | engine sebuah profil berjalan (source + destination count) |
| CopySourceOpen | 1047 | master membuka posisi (symbol / side / lots) |
| CopyOrderPlaced | 1048 | order copy dikirim ke slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | sebuah copy di-skip dan alasannya (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP diterapkan ke salinan slave |
| CopyOpenFailed | 1051 | pembukaan salinan slave gagal (terisolasi — slave lain lanjut) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master menutup → salinan slave ditutup |
| CopyCloseFailed | 1054 | penutupan salinan slave gagal |
| CopyResync | 1055 | reconnect reconciliation (source open count, orphan ditutup) |
| CopyPartialClose | 1056 | partial close master di-mirror — irisan proporsional ditutup di slave |
| CopyScaleIn | 1057 | scale-in master di-mirror (opt-in) — volume ditambahkan disalin ke slave |
| CopyPendingOrderPlaced | 1058 | pending limit/stop di-mirror ke slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | pending master dibatalkan → pending slave dibatalkan |
| CopyTrailingApplied | 1060 | trailing stop diterapkan ke salinan slave (opt-in) |
| CopyStopLossAmended | 1061 | move SL source re-amends salinan slave |
| CopyHostTokenRotated | 1062 | supervisor me-restart host yang berjalan setelah access token-nya dirotasi |

Log dimuat sebagai Serilog compact JSON (properti terstruktur: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), dikirim ke OTLP ketika `OTEL_EXPORTER_OTLP_ENDPOINT` disetel. **Sepenuhnya terkonfigurasi** per kategori via config standar — contoh: naikkan/turunkan verbositas copy-engine tanpa menyentuh kode:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // audit trail CopyEngineHost
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host test membuktikan trail dipicu untuk open, order, protection, close.

## Edge case (divalidasi terhadap cara platform copy/MAM nyata gagal)

Slippage & latency, suffix/mismatch simbol, duplicate trade pada reconnect, ketidaksesuaian leverage & sizing aman margin, perbedaan deposit-currency/contract-size, min/max lot & pembulatan, order ditolak, filter arah, orphan cleanup setelah disconnect — semua tercakup di atas. Sumber:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Cakupan mirroring lanjutan (partial close · pending orders · SL-trailing)

Host memirror lebih dari market open/close. Setiap perilaku = opt-in flag per-destinasi pada `CopyDestination` (`MirrorPartialClose` default on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` default off), dijaga oleh metode niat, jsonb-persist (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Perilaku | Test deterministik (`CopyEngineHostTests`) | Test langsung |
|-----------|-------------------------------------------|-------------|
| Partial close → irisan proporsional | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 menutup 60%) + disabled path | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop ditempatkan | `Pending_order_is_placed_on_the_slave_when_enabled` (Teori: Limit+Stop) + disabled path | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (test langsung yang sama — membatalkan di master, membuktikan slave membatalkan) ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Move SL source re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Event audit menyala | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Semua test langsung di atas **terverifikasi hijau terhadap akun demo cTrader nyata** (1:1, 1:banyak, balik, cross-cID, partial close, pending+cancel, trailing).

Penambahan wire di `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, flag trailing di `AmendPositionSltpAsync`, field order/pending di `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, digunakan oleh test pending/trailing langsung untuk menempatkan resting order jauh dari pasar), `StopLoss`/`TrailingStopLoss` di `OpenPositionSnapshot` (state trailing copy observable melalui reconcile). Salinan destination tetap dilabeli oleh **source position id** (pending copy oleh source **order id**) sehingga reconnect reconcile tetap id-based, tidak pernah menduplicate trade.

**cTrader event gotcha (terverifikasi langsung):** `ORDER_ACCEPTED`/`ORDER_CANCELLED` execution event dari resting pending order membawa **non-open `Position` placeholder** ditambah `Order`. Stream harus mengklasifikasikannya sebagai event *order* **sebelum** cabang position (di-gate pada position bukan `OPEN`), jika tidak penempatan pending mirroring salah baca sebagai posisi close. `SourceExecutionsAsync` melakukan ini; tidak ada akan secara diam-diam menjatuhkan semua pending mirroring.

## Rotasi token + afinitas node

- **Rotasi ke host yang berjalan.** `CopyEngineSupervisor` mencatat signature token pada setiap host yang berjalan dan, setiap reconcile, membangun ulang plan dari DB (di-rotasi baru oleh `OpenApiTokenRefreshService`). Signature yang berubah me-restart host (`CopyHostTokenRotated`, 1062); host baru `ResyncAsync` membangun ulang state tanpa menduplicate trade. Rotasi paksa mid-run via `IOpenApiTokenClient.RefreshAsync` untuk memverifikasi host langsung tetap menyalin.
- **Afinitas node (tanpa double-copy).** Kedua node lokal Web dan worker `CopyAgent` menjalankan supervisor. Setiap profil yang berjalan diklaim oleh tepat satu node (`CopyProfile.AssignedNode`, atomic `ExecuteUpdate` claim dikunci dari `CopyOptions.NodeName`, nama mesin default). Supervisor hanya host profil yang dimiliki; stop/pause melepaskan klaim. Cakupan:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integration (Postgres nyata, Testcontainers)**: `CopyNodeAffinityTests` menjalankan supervisor `ClaimUnassignedProfilesAsync` yang nyata — membuktikan node pertama mengklaim semua 3 profil yang berjalan, node kedua mengklaim **0** (tanpa double-host), pause→restart membebaskan klaim untuk node lain.
  - Deteksi rotasi (`TokenRotationSignatureTests`): `TokenSignature` supervisor berubah ketika token source atau destination dirotasi, stabil jika tidak (host yang berjalan me-restart hanya pada rotasi nyata).

### Refresh token single-use (penting)

cTrader **refresh token adalah single-use** — setiap refresh mengembalikan *refresh token baru*, membatalkan yang lama. Live fixture me-refresh di awal, menyimpan token yang di-rotasi ke `secrets/openapi-tokens.local.json`. Konsekuensi:
- Jika me-refresh tetapi **tidak dapat menyimpan** token baru (mis. mount read-only), token cache mati, jalan berikutnya gagal `ACCESS_DENIED`. Regenerasi dengan onboarding headless:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` menelan kegagalan tulis sehingga cache read-only tidak crash jalan, tapi **langsung** in-cluster suite tetap membutuhkan cache **writable** (K8s Job menyalin Secret ke emptyDir — lihat doc deployment).

## Menjalankan suite di cluster Kubernetes

Seluruh suite berjalan in-cluster terhadap app yang di-deploy Helm, jadi regresi tertangkap in-cluster sama seperti secara lokal. Lihat [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, suite deterministik (tanpa rahasia)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # langsung
```

`Dockerfile.tests` membangun image runner; `tests-job.yaml` Helm (di-gate `tests.enabled=false`) menjalankannya terhadap Postgres in-cluster + Web. **Default = suite copy deterministik** (tanpa rahasia, tanpa token berotasi). Untuk suite langsung, set `tests.copySecret` ke Secret yang memegang `openapi-*.local.json` yang di-gitignore; init-container menyalinnya ke **writable** emptyDir di `/app/secrets` (dibutuhkan — refresh token single-use harus persistable). Test copy hanya butuh Web + Postgres + token cache — tidak ada agent node yang diprivilege. Script membuktikan Job exit 0 dan log berisi `Passed!`.

**Diverifikasi di sini (Docker, tanpa cluster):** image test menjalankan suite deterministik (`101 passed`) dan, dengan mount `secrets/` yang writable, suite **langsung** penuh (`8 passed`) — path Job persis tanpa Kubernetes. `kind`/`kubectl`/`helm` tidak tersedia di lingkungan penulisan, jadi jalan cluster penuh `k8s-e2e.sh` adalah satu langkah yang tidak dijalankan di sini.

## Opsi matrix langsung + chaos (LiveCopyMatrix / LiveCopyChaos)

Dua suite langsung data-driven membangun di atas `LiveCopyScenario` / `LiveCopyFixture`, rekan langsung dari suite stress test deterministik DST:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` option matrix: satu open master nyata per baris terhadap akun demo, masing-masing dengan destinasi yang berbeda konfigurasinya, membuktikan hasil emas. Baris: `one_to_one`, `half_multiplier`, `reverse` (sisi berlawanan), `manage_only` (tidak membuka apa-apa), `trading_hours_closed` (jendela mengecualikan sekarang → tidak ada copy), `source_label_block` (filter label → tidak ada copy), `lot_sanity_block` (ceiling → tidak ada copy).
- **`LiveCopyChaos`** — copy engine terhadap start yang hostile: master sudah memegang posisi sebelum host dimulai, jadi konvergensi hanya bisa datang dari start-up resync (`Sync-Open-on-Start`), ditambah kasus negatif dengan toggle off. Chaos socket-flap / token-rotation / penolakan deterministik tercakup secara ekshaustif oleh suite DST; ini membuktikan path resync langsung.

Keduanya **skip dengan rapi** tanpa kredensial, melaporkan **Inconclusive** pada pasar tutup, setiap baris membersihkan posisi yang dibuka (akun demo saja).

**Ketahanan fixture:** `LiveCopyFixture` sekarang terdegradasi ke skip rapi ketika token cache tidak dapat di-refresh (refresh token cTrader single-use — cache basi atau jalan offline tidak lagi fault seluruh collection langsung; jalankan ulang onboarding OAuth untuk me-refresh cache).

## Menjalankan suite langsung + cluster sendiri (TIDAK ada alasan — semuanya self-serviceable)

Semuanya di bawah tersedia di lingkungan ini. Jangan mengatakan "kredensial tidak tersedia", "token basi", atau "kind/helm tidak terinstal" — set them up dan jalankan.

### 1. Dapatkan token Open API segar (OAuth headless, tanpa interaksi browser)

Refresh token cTrader single-use, jadi token cache AKAN basi. Buat ulang dari username/password cID tersimpan (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, atau unified `secrets/dev-credentials.local.json`). Test onboarding menjalankan **headless Edge** OAuth via Playwright, menulis `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13 detik; mengotorisasi setiap cID, cache token segar. Jalankan ulang kapanpun suite langsung melaporkan fixture unavailable karena kegagalan refresh.

### 2. Jalankan suite copy langsung (akun demo cTrader nyata)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # mirroring inti (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Tempatkan + bersihkan order DEMO nyata (bukan akun langsung), laporan **Inconclusive** pada pasar tutup. Terverifikasi hijau end to end.

### 3. Bootstrap token dari volume app yang berjalan (alternatif)

Jika app jalan + cID tertaut di-app, ekstrak refresh token terbaru langsung dari volume `app-pg-data` Postgres alih-alih mengotorisasi ulang — lihat `LiveTokenBootstrapTests`, set `CMIND_VOLUME_CONN`.

### 4. E2E cluster Kubernetes

`kind`, `helm`, Docker tersedia (instal kind/helm via `go install`/release binaries atau `choco install kind kubernetes-helm` jika tidak di PATH). Script satu-shot membangun+memasang images, men-deploy chart, menjalankan Job test in-cluster, membuktikan exit 0:

```bash
scripts/k8s-e2e.sh                                 # suite copy deterministik (tanpa rahasia)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # langsung in-cluster
```

Lihat [../deployment/kubernetes.md](../deployment/kubernetes.md).
