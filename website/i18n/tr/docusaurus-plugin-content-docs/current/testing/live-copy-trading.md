---
description: "Tam yeniden üretilebilir kopyalama-alım-satım test paketi. İki katman:"
---

# Kopyalama-alım-satım test paketi (deterministik + canlı)

Tam yeniden üretilebilir kopyalama-alım-satım test paketi. İki katman:

1. **Deterministik testler** (xUnit, ağ yok) — kopyalama matematiği + motor mantığı. Hızlı, CI, gizli değer yok. Her para-yönetimi modunu, her filtreyi/seçeneği, motor dayanıklılığını kapsar.
2. **Canlı E2E testleri** (gerçek cTrader demo hesapları) — gerçek hesaplar arasında gerçek emirler veren + kopyalayan üretim `CopyEngineHost`. Tamamen otomatik, bir birim testi gibi yeniden çalıştırılabilir: yerel gitignore edilmiş dosyalardan önbelleğe alınmış kimlik bilgilerini okur, erişim belirtecini kendi kendine yeniler, gizli değerler yokken temiz atlar (CI yeşil kalır).

Asla canlı-fonlu hesaba karşı çalışmaz — her hesap **demo**, her canlı test açtığı pozisyonları kapatır.

## Düzen

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — every sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logic against an in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (records orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — loads the gitignored secrets, saves refreshed tokens
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens from the app DB into the token cache
  LiveCopyFixture.cs             — rotates the access token, exposes the demo account list
  LiveCopyScenario.cs            — runs one real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — the live scenarios (1:1, 1:many, reverse, …)
```

## Gizli değerler (yerel, gitignore edilmiş — asla işlenmez)

Tüm kimlik bilgileri `<repo>/secrets/` altında (zaten `.gitignore`'da). Geliştirici **yalnızca ilk iki dosyayı** yazar; üçüncüsü (belirteçler) onboarding tarafından otomatik üretilir.

`secrets/openapi-test-app.local.json` — Open API uygulaması:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — yetkilendirilecek cID giriş kimlik bilgileri (bir veya çok):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **onboarding tarafından yazılır**, çok-cID'li, her çalıştırmada yenilenir:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

Yenileme belirteci **asla süresi dolmaz**, bu yüzden bir kerelik onboarding'den sonra canlı testler süresiz çalışır: her çalıştırma, her cID'nin yenileme belirtecini yeni bir erişim belirteciyle değiştirir (rotasyon) — tarayıcı yok, istem yok.

## Bir kerelik onboarding (tamamen otomatik — kimlik bilgilerini kaydetmenin ötesinde geliştirici etkileşimi yok)

Onboarding, kaydedilmiş cID kimlik bilgilerinden headless tarayıcıda gerçek cTrader ID girişini yürütür, uygulamanın kayıtlı yeniden yönlendirmesinde (`https://localhost:7080/openapi/callback`) yerel HTTPS dinleyicisinde OAuth geri çağrısını yakalar, kodu belirteçlerle değiştirir, hesap listesini yükler, çok-cID'li belirteç önbelleğini yazar. Makine başına bir kez çalıştırın (veya cID eklerken):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

`openapi-cids.local.json`'daki her cID'yi yetkilendirir, `openapi-tokens.local.json`'ı yazar. Bundan sonra canlı kopyalama testlerinin başka hiçbir şeye ihtiyacı yoktur. (Otomasyonun tamamlanması için cID'nin cTrader ID hesabının girişte 2FA/captcha'sı olmamalıdır.)

**Alternatif önyükleme** (hesaplar çalışan uygulamada zaten yetkilendirilmişse): yeniden yetkilendirmek yerine saklanmış belirteçleri doğrudan uygulamanın Postgres birim'inden çözün:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Güvenlik — yalnızca demo

Canlı testler **yalnızca demo hesaplarla** işlem yapar: fixture, belirteç önbelleğini `IsLive == false` olan hesaplara filtreler ve demo ağ geçidine bağlanır, böylece canlı hesap yetkilendirilmiş olsa bile emir asla canlı/fonlu hesaba düşemez. Bir testin açtığı her pozisyon temizlemede kapatılır.

## Çalıştırma

```bash
# Deterministic copy tests only (fast, no secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests against the real demo accounts (needs the two secrets files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Everything
dotnet test
```

Gizli-değer dosyaları olmadan canlı testler atlama nedenini yazdırır + no-op olarak geçer, böylece paket her yerde çalıştırmak için güvenlidir.

## Kapsam

### Para yönetimi / boyutlandırma (deterministik — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (sözleşme-boyutu / para birimi) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
bakiye/kaldıraç/kapasite uyumsuzluğu için **yukarı** ve **aşağı** ölçekle ("altın kural") · lot-adımı
yuvarlama · min-lot atla vs min'e-zorla · max-lot üst sınırı · sınır-vs-şartname min & max'in daha
sıkısı · sıfır ana bakiye atlama.

### Karar filtreleri (deterministik — `CopyDecisionEngineTests`)
Sembol beyaz-liste / kara-liste / izin · LongOnly / ShortOnly · reverse etkin tarafı ters çevirir ·
limit üzeri kayma atla + tam-limitte izin verilir · bayat-sinyal (maksimum gecikme) atla · sıfır-boyut
atla · yeniden-bağlanma mutabakatı (eksik-açık tekilleştirme, yetim-kapatma).

### Kopyalama motoru host'u (deterministik — `CopyEngineHostTests`, bellek-içi oturum)
Açılış bir piyasa emrini yansıtır (taraf / hacim / etiket) · **reverse** tarafı ters çevirir ve
**SL/TP'yi takas eder** · **sembol eşleme** hedef sembolü çözer · **bir slave'de emir hatası yine de
diğerlerine kopyalar** · kaynak kapatma, yansıtılan kopyayı kapatır · yeniden-bağlanma yeniden-senkronu
yetim kopyaları kapatır.

### Bağlantı dayanıklılığı (deterministik — `OpenApiConnectionTests`)
Uygulama kimlik doğrulamasından sonra Connected'a ulaşır · düşen bağlantı yeniden-bağlanır ve
yeniden-kimlik-doğrular · ölümcül kimlik doğrulama hatası arızalanır · üstel geri-çekilme.

### Canlı, gerçek cTrader demo hesapları (`CopyTradingLiveTests`)
Belirteç yenileme + hesap listeleme · **1:1** kopya çalışır · **1:çok** kopya her slave'e yansır ·
**reverse** ana alışı slave satışına çevirir · **cID-arası** kopya (bir cID altındaki ana, her biri kendi belirteci ile kimlik doğrulayarak başka bir cID altındaki slave'e yansır). Her biri anada gerçek min-lot pozisyonu açar, motorun onu yansıtmasını bekler (slave'de kaynak-pozisyon-id etiketiyle eşleşir), doğrular, her şeyi kapatır. Kapalı piyasa, başarısız değil **Inconclusive** olarak raporlanır.

## Günlük kaydı ve denetlenebilirlik

Her kopyalama-alım-satım işlemi kaynak-üretilen yapılandırılmış olaylar aracılığıyla günlüğe kaydedilir (`Core/Logging/LogMessages.cs`, olay kimlikleri 1043–1055), tam iz denetlenebilir:

| Olay | Id | Anlamı |
|-------|----|---------|
| CopyHostStarted | 1046 | bir profilin motoru başladı (kaynak + hedef sayısı) |
| CopySourceOpen | 1047 | ana bir pozisyon açtı (sembol / taraf / lot) |
| CopyOrderPlaced | 1048 | bir slave'e kopya emri gönderildi (sembol / taraf / hacim / kaynak id) |
| CopySkipped | 1049 | bir kopya atlandı ve nedeni (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | bir slave kopyasına SL/TP uygulandı |
| CopyOpenFailed | 1051 | bir slave kopya-açılışı başarısız oldu (izole — diğer slave'ler devam eder) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | ana kapandı → slave kopyası kapandı |
| CopyCloseFailed | 1054 | bir slave kopya-kapatması başarısız oldu |
| CopyResync | 1055 | yeniden-bağlanma mutabakatı (kaynak açık sayısı, kapatılan yetimler) |
| CopyPartialClose | 1056 | ana kısmi kapatma yansıtıldı — bir slave'de orantılı dilim kapatıldı |
| CopyScaleIn | 1057 | ana kademe-ekleme yansıtıldı (opt-in) — eklenen hacim bir slave'e kopyalandı |
| CopyPendingOrderPlaced | 1058 | bir slave'e bekleyen limit/stop yansıtıldı (opt-in) |
| CopyPendingOrderCancelled | 1059 | kaynak bekleyen iptal edildi → slave bekleyen iptal edildi |
| CopyTrailingApplied | 1060 | bir slave kopyasına takip eden stop uygulandı (opt-in) |
| CopyStopLossAmended | 1061 | bir kaynak SL taşıması slave kopyasını yeniden-değiştirdi |
| CopyHostTokenRotated | 1062 | süpervizör, erişim belirteci döndürüldükten sonra çalışan bir host'u yeniden başlattı |

Günlükler Serilog kompakt JSON olarak yayılır (yapılandırılmış özellikler: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), `OTEL_EXPORTER_OTLP_ENDPOINT` ayarlandığında OTLP'ye gönderilir. Standart yapılandırma aracılığıyla kategori başına **tamamen yapılandırılabilir** — örn. kodu değiştirmeden kopyalama-motoru ayrıntı düzeyini yükseltin/düşürün:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host testi, izin açılış, emir, koruma, kapatma için tetiklendiğini doğrular.

## Uç durumlar (gerçek kopyalama/MAM platformlarının nasıl başarısız olduğuna karşı doğrulanmış)

Kayma ve gecikme, sembol soneki/uyumsuzluğu, yeniden-bağlanmada yinelenen işlemler, kaldıraç uyumsuzluğu ve marjin-güvenli boyutlandırma, mevduat-para-birimi/sözleşme-boyutu farkları, min/max lot ve yuvarlama, reddedilen emirler, yön filtreleri, kesinti sonrası yetim temizliği — hepsi yukarıda kapsanmıştır. Kaynaklar:
[kaldıraç uyumsuzluğu](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[broker-arası kopyalama](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[kopyalayıcı tuzakları](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[kayma ve gecikme](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[kopyalama-alım-satım neden başarısız olur](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parametreleri](https://www.mt4copier.com/risk-parameters/).

## Gelişmiş yansıtma kapsamı (kısmi kapatma · bekleyen emirler · SL-takibi)

Host, piyasa açılış/kapanışından fazlasını yansıtır. Her davranış = `CopyDestination` üzerinde hedef-başına opt-in bayrağı (`MirrorPartialClose` varsayılan açık, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` varsayılan kapalı), niyet yöntemleriyle korunur, jsonb-kalıcılaştırılır (taşıma `CopyAdvancedMirroringAndNodeAffinity`).

| Davranış | Deterministik test (`CopyEngineHostTests`) | Canlı test |
|-----------|--------------------------------------------|-----------|
| Kısmi kapatma → orantılı dilim | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 %60 kapatır) + devre dışı yol | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Kademe-ekleme | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Bekleyen limit/stop verildi | `Pending_order_is_placed_on_the_slave_when_enabled` (Theory: Limit+Stop) + devre dışı yol | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Bekleyen iptal | `Source_pending_cancel_cancels_the_slave_pending` | (aynı canlı test — anada iptal eder, slave'in iptal ettiğini doğrular) ✅ |
| Dolan bekleyen çift-açılış yok | `Filled_pending_does_not_double_open` (emir-id → pozisyon-id tekilleştirme) | — |
| Takip eden stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Kaynak SL taşıma yeniden-değiştirme | `Source_stop_loss_move_re_amends_the_copy` | — |
| Denetim olayları tetiklenir | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Yukarıdaki tüm canlı testler **gerçek cTrader demo hesaplarına karşı yeşil doğrulanmıştır** (1:1, 1:çok, reverse, cID-arası, kısmi kapatma, bekleyen+iptal, takip).

`OpenApiTradingSession`'daki tel eklemeleri: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, `AmendPositionSltpAsync`'te takip bayrağı, `ExecutionEvent`'te emir/bekleyen alanları, `LoadSpotPriceAsync` (spot abone → bid/ask, canlı bekleyen/takip testleri tarafından piyasadan uzağa bekleyen emirler yerleştirmek için kullanılır), `OpenPositionSnapshot`'ta `StopLoss`/`TrailingStopLoss` (kopyanın takip durumu mutabakat aracılığıyla gözlemlenebilir). Hedef kopyalar **kaynak pozisyon id** ile etiketli kalır (bekleyen kopyalar kaynak **emir id** ile) böylece yeniden-bağlanma mutabakatı id-tabanlı kalır, işlemi asla yinelemez.

**cTrader olay tuzağı (canlı doğrulanmış):** bekleyen emrin `ORDER_ACCEPTED`/`ORDER_CANCELLED` yürütme olayı, `Order` artı **açık-olmayan `Position` yer tutucusu** taşır. Akış, bunu pozisyon dalından **önce** *emir* olayı olarak sınıflandırmalıdır (pozisyon `OPEN` değilse geçitli), aksi halde bekleyen yerleşim pozisyon kapatması olarak yanlış-okunur. `SourceExecutionsAsync` bunu yapar; bunu kaçırmak tüm bekleyen yansıtmayı sessizce düşürür.

## Belirteç rotasyonu + düğüm yakınlığı

- **Çalışan host'lara rotasyon.** `CopyEngineSupervisor`, her çalışan host'ta belirteç imzasını kaydeder ve her mutabakatta planı DB'den yeniden oluşturur (`OpenApiTokenRefreshService` tarafından yeni döndürülmüş). Değişen imza host'u yeniden başlatır (`CopyHostTokenRotated`, 1062); yeni host'un `ResyncAsync`'i işlemleri yinelemeden durumu yeniden oluşturur. Canlı host'un kopyalamaya devam ettiğini doğrulamak için `IOpenApiTokenClient.RefreshAsync` aracılığıyla çalışma-ortası rotasyonu zorlayın.
- **Düğüm yakınlığı (çift-kopya yok).** Hem Web yerel düğümü hem de `CopyAgent` çalışanı bir süpervizör çalıştırır. Her çalışan profil tam olarak bir düğüm tarafından talep edilir (`CopyProfile.AssignedNode`, `CopyOptions.NodeName`'e anahtarlanmış atomik `ExecuteUpdate` talebi, varsayılan makine adı). Süpervizör yalnızca sahip olduğu profilleri barındırır; durdurma/duraklatma talebi serbest bırakır. Kapsam:
  - Alan (birim): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Entegrasyon (gerçek Postgres, Testcontainers)**: `CopyNodeAffinityTests`, süpervizörün gerçek `ClaimUnassignedProfilesAsync`'ini yürütür — ilk düğümün 3 çalışan profilin tümünü talep ettiğini, ikincinin **0** talep ettiğini (çift-host yok), duraklat→yeniden-başlat'ın talebi başka bir düğüm için serbest bıraktığını doğrular.
  - Rotasyon algılama (`TokenRotationSignatureTests`): kaynak veya hedef belirteci döndüğünde süpervizörün `TokenSignature`'ı değişir, aksi halde kararlıdır (çalışan host yalnızca gerçek rotasyonda yeniden başlar).

### Tek-kullanımlık yenileme belirteçleri (önemli)

cTrader **yenileme belirteçleri tek-kullanımlıktır** — her yenileme *yeni* bir yenileme belirteci döndürür, eskisini geçersizleştirir. Canlı fixture başlangıçta yeniler, döndürülen belirteci `secrets/openapi-tokens.local.json`'a kalıcılaştırır. Sonuçlar:
- Bir çalıştırma yeniler ama yeni belirteci **kalıcılaştıramazsa** (örn. salt-okunur bağlama), önbelleğe alınan belirteç ölür, sonraki çalıştırma `ACCESS_DENIED` ile başarısız olur. Headless onboarding ile yeniden üretin:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` yazma hatalarını yutar, böylece salt-okunur önbellek çalıştırmayı çökertmez, ancak **canlı** küme-içi paket yine de **yazılabilir** önbelleğe ihtiyaç duyar (K8s Job, Secret'ı emptyDir'e kopyalar — dağıtım belgesine bakın).

## Paketi bir Kubernetes kümesinde çalıştırma

Tüm paket, Helm ile dağıtılmış uygulamaya karşı küme-içinde çalışır, böylece gerileme küme-içinde yerel olduğu gibi yakalanır. Bkz. [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, deterministic suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests`, çalıştırıcı görüntüsünü oluşturur; Helm `tests-job.yaml` (geçitli `tests.enabled=false`) onu küme-içi Postgres + Web'e karşı çalıştırır. **Varsayılan = deterministik kopyalama paketi** (gizli değer yok, dönen belirteç yok). Canlı paket için, `tests.copySecret`'i gitignore edilmiş `openapi-*.local.json`'ı tutan Secret'a ayarlayın; init-konteyner onu `/app/secrets`'te **yazılabilir** emptyDir'e kopyalar (gerekli — tek-kullanımlık yenileme belirteçleri kalıcılaştırılabilir olmalıdır). Kopyalama testlerinin yalnızca Web + Postgres + belirteç önbelleğine ihtiyacı vardır — ayrıcalıklı düğüm ajanları yok. Betik, Job'ın 0 ile çıktığını ve günlüklerin `Passed!` içerdiğini doğrular.

**Burada doğrulanmış (Docker, küme yok):** test görüntüsü deterministik paketi (`101 passed`) ve yazılabilir `secrets/` bağlaması ile tam **canlı** paketi (`8 passed`) çalıştırır — Kubernetes eksi tam Job yolu. `kind`/`kubectl`/`helm` yazım ortamında mevcut değil, bu yüzden tam `k8s-e2e.sh` küme çalıştırması burada yürütülmeyen tek adımdır.

## Canlı seçenek matrisi + kaos (LiveCopyMatrix / LiveCopyChaos)

`LiveCopyScenario` / `LiveCopyFixture` üzerine kurulu iki veri-güdümlü canlı paket, deterministik DST stres paketinin canlı karşılığı:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` seçenek matrisi: demo hesaplarına karşı satır başına bir gerçek ana açılış, her biri farklı-yapılandırılmış hedefle, altın sonucu doğrular. Satırlar: `one_to_one`, `half_multiplier`, `reverse` (karşı taraf), `manage_only` (hiçbir şey açmaz), `trading_hours_closed` (pencere şimdiyi hariç tutar → kopya yok), `source_label_block` (etiket filtresi → kopya yok), `lot_sanity_block` (tavan → kopya yok).
- **`LiveCopyChaos`** — düşmanca başlangıca karşı kopyalama motoru: host başlamadan önce ana zaten bir pozisyon tutar, böylece yakınsama yalnızca başlangıç yeniden-senkronundan (`Sync-Open-on-Start`) gelebilir, artı geçiş kapalıyken negatif durum. Deterministik soket-titremesi / belirteç-rotasyonu / reddetme kaosu DST paketi tarafından kapsamlı olarak kapsanır; bu, canlı yeniden-senkron yolunu doğrular.

Her ikisi de kimlik bilgileri olmadan **temiz atlar**, kapalı piyasada **Inconclusive** raporlar, her satır açtığı pozisyonları temizler (yalnızca demo hesaplar).

**Fixture sağlamlığı:** `LiveCopyFixture` artık önbelleğe alınmış belirteçler yenilenemediğinde temiz atlamaya iner (cTrader yenileme belirteçleri tek-kullanımlık — bayat önbellek veya çevrimdışı çalıştırma artık tüm canlı koleksiyonu arızalandırmaz; önbelleği yenilemek için OAuth onboarding'i yeniden çalıştırın).

## Canlı + küme paketlerini kendiniz çalıştırma (BAHANE YOK — her şey kendi kendine hizmet edilebilir)

Aşağıdaki her şey bu ortamda mevcuttur. "Kimlik bilgileri mevcut değil", "belirteç bayat" veya "kind/helm kurulu değil" **iddiasında bulunmayın** — bunları kurun ve çalıştırın.

### 1. Yeni bir Open API belirteci alın (headless OAuth, tarayıcı etkileşimi yok)

cTrader yenileme belirteçleri tek-kullanımlık, bu yüzden önbelleğe alınan belirteç bayatlayacaktır. Kaydedilmiş cID kullanıcı adı/parolasından (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, veya birleşik `secrets/dev-credentials.local.json`) onu kendiniz yeniden basın. Onboarding testi Playwright aracılığıyla **headless Edge** OAuth'u yürütür, `secrets/openapi-tokens.local.json`'ı yazar:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13 sn; her cID'yi yetkilendirir, yeni belirteçleri önbelleğe alır. Canlı paket yenileme hatası nedeniyle fixture'ın mevcut olmadığını her raporladığında yeniden çalıştırın.

### 2. Canlı kopyalama paketlerini çalıştırın (gerçek cTrader demo hesapları)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # core mirroring (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # option matrix (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos (2)
```

Gerçek DEMO emirleri yerleştirip temizler (asla canlı hesaplar), kapalı piyasada **Inconclusive** raporlar. Uçtan uca yeşil doğrulanmış.

### 3. Çalışan bir uygulama biriminden belirteçleri önyükleyin (alternatif)

Uygulama çalıştırıldıysa + cID uygulama-içinde bağlandıysa, yeniden yetkilendirmek yerine uygulamanın en son yenileme belirtecini doğrudan `app-pg-data` Postgres birim'inden çıkarın — bkz. `LiveTokenBootstrapTests`, `CMIND_VOLUME_CONN`'i ayarlayın.

### 4. Kubernetes kümesi E2E

`kind`, `helm`, Docker mevcut (PATH'te değilse `go install`/sürüm ikili dosyaları veya `choco install kind kubernetes-helm` aracılığıyla kind/helm kurun). Tek-atışlık betik görüntüleri oluşturur+yükler, chart'ı dağıtır, küme-içi test Job'ını çalıştırır, çıkış 0'ı doğrular:

```bash
scripts/k8s-e2e.sh                                 # deterministic copy suite (no secrets)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Bkz. [../deployment/kubernetes.md](../deployment/kubernetes.md).
