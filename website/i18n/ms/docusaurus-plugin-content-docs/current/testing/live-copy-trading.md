---
description: "Suite ujian salinan perdagangan penuh yang boleh dihasilkan semula. Dua lapisan:"
---

# Suite ujian salinan perdagangan (deterministik + langsung)

Suite ujian salinan perdagangan penuh yang boleh dihasilkan semula. Dua lapisan:

1. **Ujian deterministik** (xUnit, tiada rangkaian) — matematik salinan + logik enjin. Pantas, CI, tiada rahsia. Meliputi setiap mod pengurusan wang, setiap penapis/pilihan, keteguhan enjin.
2. **Ujian E2E langsung** (akaun demo cTrader sebenar) — `CopyEngineHost` meletakkan + menyalin pesanan sebenar antara akaun sebenar. Sepenuhnya automatik, boleh ulang seperti ujian unit: baca kredensi di-cache dari fail gitignored tempatan, menyegarkan sendiri token akses, langkau kemas apabila rahsia tiada (CI kekal hijau).

Tidak pernah lari terhadap akaun yang didanai langsung — setiap akaun **demo**, setiap ujian langsung menutup posisi nó dibuka.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — setiap mod saiz + pembundaran + lot min/max
  CopyDecisionEngineTests.cs     — arah/songsang/slippage/lengah/penapis simbol/bola kosong
  CopyEngineHostTests.cs         — logik salinan hos terhadap sesi tiruan dalam-memori
  FakeTradingSession.cs          — IOpenApiTradingSession deterministik (rekod pesanan/tutup/pinda)
  OpenApiConnectionTests.cs      — sambung / sambung semula / backoff / fault maut (keteguhan)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — muat rahsia gitignored, simpan token yang disegarkan
  LiveTokenBootstrapTests.cs     — sekali sahaja: nyahsulit token dari DB apl ke cache token
  LiveCopyFixture.cs             — memutar token akses, dedahkan senarai akaun demo
  LiveCopyScenario.cs            — jalankan satu senario salinan end to end (buka → salin → sahkan → bersih)
  CopyTradingLiveTests.cs        — senario langsung (1:1, 1:banyak, songsang, …)
```

## Rahsia (tempatan, gitignored — tidak pernah dikomit)

Semua kredensi di bawah `<repo>/secrets/` (sudah dalam `.gitignore`). Dev menulis **dua fail pertama sahaja**; ketiga (token) auto-dihasilkan oleh aboard.

`secrets/openapi-test-app.local.json` — Apl Open API:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — Kredensi log masuk cID untuk mengesahkan (satu atau banyak):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **ditulis oleh aboard**, pelbagai cID, disegarkan setiap lari:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Token penyegaran **tidak pernah tamat**, jadi selepas aboard sekali ujian langsung berfungsi selama-lamanya: setiap lari menukar token akses setiap cID untuk token akses baharu (putaran) — tiada pelayar, tiada prompt. Setiap cID mestilah tidak ada 2FA/captcha pada daftar masuk untuk automasi melengkapkan.

## Satu-kali aboard (Sepenuhnya automatik — tiada interaksi dev selain menyimpan kredensi)

Aboard menghidupkan daftar masuk ID cTrader sebenar dalam pelayar tanpa kepala dari kredensi cID yang disimpan, menangkap OAuth callback pada'écouteur HTTPS tempatan di redirect yang berdaftar apl (`https://localhost:7080/openapi/callback`), menukar kod untuk token, memuatkan senarai akaun, menulis cache token pelbagai cID. Jalankan sekali per mesin (atau apabila menambah cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Mengesahkan setiap cID dalam `openapi-cids.local.json`, menulis `openapi-tokens.local.json`. Selepas itu ujian salinan langsung tidak perlukan apa-apa lagi. (cID's cTrader ID account must have no 2FA/captcha on login for automation to complete.)

**Alteratif bootstrap** (jika akaun sudah dibenarkan dalam apl yang berjalan): nyahsulit token terus dari isipadu Postgres apl dan bukan Meneruskan:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Keselamatan — demo sahaja

Ujian langsung berdagang **hanya akaun demo**: fixture menapis cache token kepada akaun dengan `IsLive == false` dan bersambung ke geteway demo, jadi pesanan tidak pernah mendarat pada akaun langsung/dibiayai. Setiap posisi yang dibuka ujian ditutup dalam pembersihan.

## Menjalankan

```bash
# Ujian salinan deterministik sahaja (pantas, tiada rahsia, CI-selamat)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Ujian salinan langsung terhadap akaun demo sebenar (memerlukan dua fail rahsia)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Segalanya
dotnet test
```

Tanpa fail rahsia ujian langsung cetak sebab langkau + lulus sebagai no-ops, jadi suite selamat untuk lari di mana-mana.

## Perlindungan

### Pengurusan wang / saiz (deterministik — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (saiz-kontrak / mata wang) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
skala **naik** dan **turun** untuk percanggahan baki/leverage/kapasiti (("golden rule")) · pembundaran lot-step · langkau lot-min vs paksa-ke-min · had lot-max · had yang lebih ketatvs-spec min & max · langkau baki master kosong.

### Penapis keputusan (deterministik — `CopyDecisionEngineTests`)
Senarai putih / senarai hitam simbol · LongOnly / ShortOnly · songsang flip effective side ·
slippage lebih had langkau + tepat-pada-had dibenarkan · isyarat basi (lewat max) langkau · langkau saiz-sifar ·
rekon nyersingkir (buka-hilang dedup, tutup orphan).

### Hos enjin salinan (deterministik — `CopyEngineHostTests`, sesi dalam-memori)
Buka cermin pesanan pasaran (sisi / volum / label) · **songsang** flip side dan **tukar SL/TP** ·
**pemetaan simbol** menyelesaikan simbol destinasi · **kegagalan pesanan pada satu slave masih menyalin kepada yang lain** · sumber tutup menutup salinan · reconnnect resync menutup orphan.

### Keteguhan sambungan (deterministik — `OpenApiConnectionTests`)
Mencapai Connected selepas auth apl · sambungan jatuh bersambung dan re-auth · ralat auth maut faults ·
exponential backoff.

### Langsung, akaun demo cTrader sebenar (`CopyTradingLiveTests`)
Penyegaran token + senarai akaun · **1:1** salinan melaksanakan · **1:banyak** salinan memirror setiap slave ·
**songsang** bertukar master beli kepada slave jual · **lintas-cID** (master di bawah satu cID memirror ke slave di bawah cID lain, setiap mengesahkan dengan token sendiri). Setiap membuka posisi lot min sebenar pada master, tunggu enjin memirrornya (dicocok oleh source-position-id label pada slave), menegaskan, menutup segalanya. Paseo tutup pasaran dilaporkan **Inconclusive**, bukan gagal.

## Pembalakan & keboleh审计

Setiap operasi salinan perdagangan dibalak melalui peristiwa berstruktur yang dijana sumber (`Core/Logging/LogMessages.cs`, ID peristiwa 1043–1055), jejak penuh boleh di审计:

| Peristiwa | Id | Makna |
|-------|----|---------|
| CopyHostStarted | 1046 | enjin profil naik (sumber + bilang destinasi) |
| CopySourceOpen | 1047 | master membuka posisi (simbol / sisi / lot) |
| CopyOrderPlaced | 1048 | pesanan salinan dihantar ke slave (simbol / sisi / volum / id sumber) |
| CopySkipped | 1049 | salinan dilangkau dan mengapa (slippage / arah / penapis_simbol / saiz_sifar / …) |
| CopyProtectionApplied | 1050 | SL/TP digunakan pada salinan slave |
| CopyOpenFailed | 1051 | pembukaan salinan slave gagal (terasing — slave lain terus) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master tutup → salinan slave ditutup |
| CopyCloseFailed | 1054 | penutupan salinan slave gagal |
| CopyResync | 1055 | penyelesaian semula selepas sambung (bilang posisi terbuka sumber, orphan ditutup) |
| CopyPartialClose | 1056 | tutup separa master dicerminkan — hirisan berkadaran ditutup pada slave |
| CopyScaleIn | 1057 | scale-in master dicerminkan (pilihan) — volum ditambah disalinkan ke slave |
| CopyPendingOrderPlaced | 1058 | pesanan limit/stop tertunda dicerminkan ke slave (pilihan) |
| CopyPendingOrderCancelled | 1059 | pembatalan pesanan tertunda sumber → pesanan slave dibatalkan |
| CopyTrailingApplied | 1060 | trailing stop digunakan pada salinan slave (pilihan) |
| CopyStopLossAmended | 1061 | SL sumber bergerak meminda salinan slave |
| CopyHostTokenRotated | 1062 | penyelia restart hos yang berjalan selepas token akses berputar |

Log dipancarkan sebagai Serilog JSON ringkas (harta berstruktur: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), dihantar ke OTLP apabila `OTEL_EXPORTER_OTLP_ENDPOINT` ditetapkan. **Boleh dikonfigurasi sepenuhnya** setiap kategori melalui konfigurasi standard — cth naik/turunkan verbositi enjin salinan tanpa mengubah kod:

```jsonc
// appsettings.json — overrides tahap Serilog
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // jejak audit CopyEngineHost
  "Nodes.CopyTrading": "Information"        // penyelia / penyegaran token
} } }
```

`Audit_log_records_every_trading_operation` ujian hos assert jejak memecahkan untuk buka, pesanan, perlindungan, tutup.

## Kes tepi (divalidasi terhadap bagaimana platform salinan/MAM sebenar gagal)

Slippage & kependaman, akhiran/pertamaan simbol, perdagangan pendua pada sambung semula, percanggahan leverage & saiz yang selamat, perbezaan mata wang deposit/saiz-kontrak, lot min/max & pembundaran, pesanan ditolak, penapis arah, pembersihan orphan selepas putuskan — kesemua diliputi di atas. Sumber:
[percanggahan leverage](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[lintas-broker menyalin](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & kependaman](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Perlindungan advanced mirroring (tutup separa · pesanan tertunda · SL-trailing)

Hos memirror lebih daripada buka/tutu pasaran. Setiap kelakuan = tanda pilihan masuk setiap destinasi pada `CopyDestination` (`MirrorPartialClose` lalai aktif, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` lalai off), dijaga oleh kaedah niat, jsonb-bertekun (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Kelakuan | Ujian deterministik (`CopyEngineHostTests`) | Ujian langsung |
|-----------|--------------------------------------------|-----------|
| Tutup separa → hirisan berkadaran | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 menutup 60%) + laluan dilumpuhkan | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop diletakkan | `Pending_order_is_placed_on_the_slave_when_enabled` (Teori: Limit+Stop) + laluan dilumpuhkan | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pembatalan tertunda | `Source_pending_cancel_cancels_the_slave_pending` | (ujian langsung yang sama — batal pada master, assert slave batal) ✅ |
| Tertunda diisi tidak buka dua kali | `Filled_pending_does_not_double_open` (order-id → position-id dedup) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| SL sumber bergerak meminda | `Source_stop_loss_move_re_amends_the_copy` | — |
| Acara audit menyala | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Kesemua ujian langsung di atas **disahkan hijau terhadap akaun demo cTrader sebenar** (1:1, 1:banyak, songsang, lintas-cID, tutup separa, tertunda+cancel, trailing).

Penambahan wire dalam `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, bendera trailing pada `AmendPositionSltpAsync`, medan pesanan/tertunda pada `ExecutionEvent`, `LoadSpotPriceAsync` (langganan spot → bid/ask, digunakan oleh ujian langsung tertunda/trailing untuk meletakkan pesanan yang tinggal jauh dari pasaran), `StopLoss`/`TrailingStopLoss` pada `OpenPositionSnapshot` (keadaan trailing salinan boleh diperhatikan melalui reconcile). Salinan destinasi kekal dilabel oleh **source position id** (salinan tertunda oleh source **order id**) jadi reconnect reconcile kekal berdasarkan id, tidak pernah menduakan dagangan.

**Gotcha acara cTrader (disahkan langsung):** `ORDER_ACCEPTED`/`ORDER_CANCELLED` acara pelaksanaan untuk pesanan tertunda membawa **Position bukan OPEN** plus the `Order`. Stream mestilah mengklasifikasikannya sebagai *peristiwa pesanan* **sebelum** cawangan posisi (berpintasan pada posisi bukan `OPEN`), kalau tidak penempatan tertunda disalahtafsir sebagai penutupan posisi. `SourceExecutionsAsync` melakukan ini; meninggalkannya menyebabkansemua perlindungan tertunda senyap.

## Putaran token + afiniti nod

- **Putaran ke hos yang berjalan.** `CopyEngineSupervisor` merekodkan tandatangan token pada setiap hos yang berjalan dan, setiap kitaran sepadan, membina semula pelan dari DB (baharu disegarkan oleh `OpenApiTokenRefreshService`). Tandatangan berubah restart hos (`CopyHostTokenRotated`, 1062); hos baharu `ResyncAsync` membina semula keadaan tanpa menduakan dagangan. Putaran paksa tengah-lari melalui `IOpenApiTokenClient.RefreshAsync` untuk sahkan hos langsung terus menyalin.
- **Afiniti nod (tiada salinan berganda).** Kedua-dua nod tempatan Web dan pekerja `CopyAgent` menjalankan penyelia. Setiap profil berjalan dituntut oleh tepat satu nod (`CopyProfile.AssignedNode`, tuntutan `ExecuteUpdate` atomik berpalang pada `CopyOptions.NodeName`, lalai nama mesin). Penyelia menghos hanya profil yang nó miliki; stop/jeda melepaskan tuntutan. Perlindungan:
  - Domain (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integrasi (Postgres sebenar, Testcontainers)**: `CopyNodeAffinityTests` menghidupkan `ClaimUnassignedProfilesAsync` penyelia sebenar — menegaskan nod pertama menuntut kesemua 3 profil berjalan, nod kedua menuntut **0** (tiada hos berganda), jeda→restart membebaskan tuntutan untuk nod lain.
  - Pengesanan putaran (`TokenRotationSignatureTests`): `TokenSignature` penyelia berubah apabila sumber atau destinasi token berputar, stabil jika tidak (hos berjalan restart hanya pada putaran sebenar).

### Token penyegaran sekali-guna (penting)

cTrader **token penyegaran ialah sekali-guna** — setiap penyegaran mengembalikan token penyegaran *baharu*, membatalkan yang lama. Fixture langsung menyegarkan pada permulaan, mengekalkan token yang diputar ke `secrets/openapi-tokens.local.json`. Konsekuensi:
- Jika lari menyegarkan tetapi **tidak boleh mengekalkan** token baharu (cth mount baca sahaja), token cache mati, lari akan gagal `ACCESS_DENIED`. Jana semula dengan aboard utan pelayar:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` menelan kegagalan tulis supaya cache baca sahaja tidak merosakkan lari, tetapi **langsung** dalam-kluster masih memerlukan **cache boleh tulis** (Kerja K8s menyalin Rahsia ke emptyDir — lihat doc penempatan).

## Menjalankan suite dalam kluster Kubernetes

Seluruh suite lari dalam-kluster terhadap apl yang diletakkan Helm, jadi regresi ditangkap dalam-kluster sama seperti secara tempatan. Lihat [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, suite deterministik (tiada rahsia)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # langsung
```

`Dockerfile.tests` membina imej pelari; Helm `tests-job.yaml` (gerbang `tests.enabled=false`) melarikannya terhadap Postgres dalam-kluster + Web. **Lalai = suite deterministik salinan** (tiada rahsia, tiada token berputar). Untuk suite langsung, tetapkan `tests.copySecret` kepada Rahsia yang memegang `openapi-*.local.json`; init-container menyalin nó ke **emptyDir boleh tulis** di `/app/secrets` (diperlukan — token penyegaran sekali-guna harus boleh dikekalkan). Ujian salinan hanya perlukan Web + Postgres + cache token — tiada ejen nod berg privileges. Skrip assert Kerja keluar 0 dan log mengandungi `Passed!`.

**Disahkan di sini (Docker, tiada kluster):** imej ujian menjalankan suite deterministik (`101 lulus`) dan, dengan `secrets/` boleh tulis dipasang, suite **langsung** penuh (`8 lulus`) — laluan Kerja yang tepat tolak Kubernetes. `kind`/`kubectl`/`helm` tidak tersedia dalam persekitaran pengarangan, jadi `k8s-e2e.sh` penuh lari kluster ialah langkah yang tidak dilaksanakan di sini.

## Matriks pilihan langsung + chaos (LiveCopyMatrix / LiveCopyChaos)

Dua suite langsung yang dikendalikan data membina pada `LiveCopyScenario` / `LiveCopyFixture`, teman langsung kepada suite tekanan deterministik:

- **`LiveCopyMatrix`** — matriks pilihan `[Theory]`/`[MemberData]`: satu buka master sebenar setiap baris terhadap akaun demo, setiap dengan destinasi yang dikonfigurasi berbeza, menegaskan keputusan golden. Baris: `one_to_one`, `half_multiplier`, `reverse` (sisi songsang), `manage_only` (buka tiada apa), `trading_hours_closed` (jendela mengecualikan sekarang → tiada salinan), `source_label_block` (label penapis → tiada salinan), `lot_sanity_block` (ceiling → tiada salinan).
- **`LiveCopyChaos`** — enjin salinan terhadap hostilan bermula: master sudah pegang posisi sebelum hos bermula, jadi konvergensi hanya boleh datang dari resync permulaan (`Sync-Open-on-Start`), tambah kes negatif dengan togolan off. Soket slap / putaran token / chaos penolakan yang disiarkan melalui suite tekanan deterministik; ini menegaskan laluan resync langsung.

Keduanya **melangkau dengan kemas** tanpa kredensi, melaporkan **Inconclusive** pada pasaran tutup, setiap baris membersihkan posisi nó dibuka (demo sahaja).

**Keteguhan Fixture:** `LiveCopyFixture` sekarang mundur ke langkau bersih apabila token di-cache tidak boleh disegarkan (token penyegaran cTrader sekali-guna — cache basi atau lari luar talian tidak lagi rosak seluruh koleksi langsung; jalankan semula OAuth aboard untuk menyegarkan cache).

## Menjalankan suite langsung + kluster sendiri (TIDAK ada alasan — segalanya layan diri)

Segalanya di bawah tersedia dalam persekitaran ini. Jangan menuntut "kredensi tidak tersedia", "token basi", atau "kind/helm tidak dipasang" — sediakan nó dan jalankan.

### 1. Dapatkan token Open API baharu (OAuth utan pelayar)

cTrader token penyegaran sekali-guna, jadi token cache AKAN menjadi basi. Jana semula nó dari kata laluan/nama pengguna cID (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, atau bersatu `secrets/dev-credentials.local.json`). Ujian aboard menghidupkan **OAuth utan pelayar Edge** melalui Playwright, menulis `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; mengesahkan setiap cID, cache token baharu. Jalankan semula apabila suite langsung melaporkan fixture tidak tersedia kerana kegagalan segar.

### 2. Jalankan suite salinan langsung (akaun demo cTrader sebenar)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # mirroring teras (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matriks pilihan (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Letak + bersihkan pesanan DEMO sebenar (tidak pernah akaun langsung), laporkan **Inconclusive** pada pasaran tutup. Disahkan hijau end to end.

### 3. Bootstrap token dari isipadu apl yang berjalan (alternatif)

Jika apl lari + cID dipaut dalam apl, ekstrak token segar terkini terus dari `app-pg-data` Postgres volume berbanding Meneruskan — lihat `LiveTokenBootstrapTests`, tetapkan `CMIND_VOLUME_CONN`.

### 4. E2E kluster Kubernetes

`kind`, `helm`, Docker tersedia (pasang kind/helm melalui `go install`/rilis binari atau `choco install kind kubernetes-helm` jika tidak pada PATH). Skrip sekali sahaja membina+muatkan imej,menterimaprabola, menjalankan Kerja ujian dalam-kluster, assert keluar 0:

```bash
scripts/k8s-e2e.sh                                 # suite deterministik salinan (tiada rahsia)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # langsung dalam-kluster
```

Lihat [../deployment/kubernetes.md](../deployment/kubernetes.md).
