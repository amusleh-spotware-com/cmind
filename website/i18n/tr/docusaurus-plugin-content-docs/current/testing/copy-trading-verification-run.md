---
description: "Kalan kopya-işlem çalışmasının tam doğrulaması — aşağıdakilerin tümü yalnızca yazılmadı, gerçekten yürütüldü."
---

# Kopya-işlem doğrulama çalışması (2026-07-10)

Kalan kopya-işlem çalışmasının tam doğrulaması — aşağıdakilerin tümü yalnızca yazılmadı, **gerçekten yürütüldü**.

## Canlı (gerçek cTrader demo hesapları) — 8/8 geçti
1:1 · 1:çok · ters · çapraz-cID · kısmi-kapama · **bekleyen limit + iptal** · **iz-süren stop** · belirteç-yenileme.
Canlı senaryolar `RunPendingAsync` / `RunTrailingAsync` eklendi (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Entegrasyon (gerçek Postgres, Testcontainers) — geçti
- `CopyNodeAffinityTests` — süpervizör gerçek atomik talep: ilk düğüm tüm çalışan profilleri talep eder, ikinci **0** talep eder (çift-kopya yok); duraklatma serbest bırakır + geri alır.
- `TokenRotationSignatureTests` — imza yalnızca gerçek belirteç rotasyonunda değişir.

## Küme-içi (kind + Helm) — geçti
`kind`/`kubectl`/`helm` kuruldu, gerçek kind kümesine karşı `scripts/k8s-e2e.sh` çalıştırıldı:
- **Deterministik İş: 101 geçti** küme-içi.
- **Canlı İş: 8 geçti** küme-içi (init-konteyner `seed-secrets` Secret → yazılabilir emptyDir kopyalar, gerçek demo hesapları).
- İş `Complete 1/1`, betik çıkış 0.

## Doğrularken bulunan hatalar (düzeltildi + yeniden doğrulandı)
- **Bekleyen olaylar**: cTrader, dinlenen limit/stop `ORDER_ACCEPTED`/`CANCELLED`'a *açık-olmayan Pozisyon yer tutucu* ekler. `SourceExecutionsAsync` artık yerleştirme/iptali pozisyon dalından önce emir olayı olarak sınıflandırır, ancak limit/stop *dolumunun* (örn. stop-loss-tetiklenen kapama) kapama yoluna düşmesine izin verir.
- **Tek-kullanımlık yenileme belirteçleri**: cTrader her yenilemede yenileme belirtecini döndürür. Kalıcılaştıramayan salt-okunur önbellek kendini geçersiz kılar. Bu yüzden Canlı K8s İşi Secret'i **yazılabilir** emptyDir'e kopyalar; İş deterministik pakete varsayılır. `SaveTokens` artık en-iyi-çaba. Canlı semboller FX'e zorlandı (BTCUSD iz-süren değişiklikleri broker-reddedildi).
- Betik imaj adlandırması Helm `registry/repository` bölünmesine + `pullPolicy=Never`'e uyacak şekilde düzeltildi.

## Gelişmiş aynalama + belirteç-yaşam-döngüsü + ölçekleme programı (2026-07-10) — deterministik katmanlar geçti

Takip programı emir-türü filtreleme, bekleyen-emir süre dolumu kopyalama, market-range / stop-limit kayma
aynalama, SL/TP kopya anahtarları, zarif yerinde belirteç değiştirme (cID başına tek geçerli belirteç),
cTrader-sadık simülatör, kendini-iyileştiren düğüm kirası, birleşik geliştirici-kimlik-bilgileri dosyası ekler.

- **Birim — 210 geçti** (`dotnet test tests/UnitTests`). Yeni kopya kapsamı: emir-türü filtresi (açık + bekleyen),
  market-range kayma aynası + baz fiyat, süre dolumu kopya açık/kapalı, stop-limit kayma, bekleyen değişiklik,
  master-açık-ile-başlama, bağlantı-kes→master-işledi→yeniden-bağlan yeniden-senk. (eksik açma + yetim kapama),
  yerinde belirteç değiştirme (yeniden başlatma yok), çapraz-cID geçersiz kılma, alan değişmezleri, kira
  sahipliği, belirteç-sürüm artışı.
- **Entegrasyon (gerçek Postgres, Testcontainers) — geçti**: `CopyNodeAffinityTests` (atomik talep, çift-kopya
  yok, duraklatma serbest bırakma, **başka bir düğümce süresi-dolmuş-kira geri alma**),
  `TokenRotationSignatureTests` (imza belirteç-sürüm artışında değişir), `OpenApiAuthorizationPersistenceTests`
  (TokenVersion kalıcılaşır + yenilemede artar).
- **E2E** (`tests/E2ETests`): hedef-seçenek gidiş-dönüş artık tam yaşam döngüsünün yanında emir-türü filtresi,
  kopya-süre-dolumu, kopya-kayma iddia eder.
- **Derleme**: `TreatWarningsAsErrors` altında temiz; değişen dosyalarda Rider `get_file_problems` temiz.

Canlı senaryolar (gerçek cTrader demo hesapları) bekleyen-stop, market-range, süre dolumu, açık-ile-başlama,
çalışma-ortası belirteç rotasyonu için aynı motora karşı yazıldı; [dev-credentials.md](dev-credentials.md) başına
birleşik `secrets/dev-credentials.local.json` ile çalıştırın.

## Bilinen takip
Küme-içi canlı çalıştırma tek-kullanımlık belirteci döndürdü; yerel önbelleği
`CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests` ile yeniden oluşturun
(cTrader çalıştırmadan hemen sonra OAuth sayfasını kısıtladı — temizlendiğinde yeniden deneyin).
