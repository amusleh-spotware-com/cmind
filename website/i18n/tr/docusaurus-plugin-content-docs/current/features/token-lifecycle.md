---
description: "cTrader'ın Open API'si, cTrader kimliği (cID) başına aynı anda bir geçerli erişim belirtecine izin verir. Yeni bir belirteç verildiği anda — zamanlanmış bir yenileme veya bir…"
---

# Open API belirteç yaşam döngüsü

cTrader'ın Open API'si, **cTrader kimliği (cID) başına aynı anda bir geçerli erişim belirtecine** izin
verir. Yeni bir belirteç verildiği anda — zamanlanmış bir yenileme veya kullanıcı aynı cID'de başka bir
hesap bağladığında yeniden yetkilendirme — önceki erişim belirteci geçersiz kılınır. Uzak bir düğümde
çalışan bir kopya motoru artık-ölü o belirteci tutuyor, bu yüzden yeni belirteç, canlı bağlantıyı düşürmeden
ona ulaşmalıdır.

## Model

- **`OpenApiAuthorization`**, bir cID'nin şifrelenmiş erişim + yenileme belirteçlerini tutan toplamdır.
  `(UserId, CtidUserId)` üzerindeki benzersiz bir indeks, **kullanıcı başına cID başına tam olarak bir
  yetkilendirme** zorunlu kılar.
- **`TokenVersion`** — belirteç her döndüğünde artırılan monoton bir sayaç (`Refresh()`, aynı cID'de başka
  bir hesap bağlandığında yeniden-yetkilendirme yolunu da kapsar). Tek-geçerli-belirteç kuralının sürüm
  işaretçisidir ve çalışan bir host'un, iki belirteç dizesi çakışsa bile bir değişikliği tespit etmek için
  kullandığı şeydir.
- Belirteçler `ISecretProtector` (`EncryptionPurposes.OpenApiAccessToken` / `OpenApiRefreshToken`)
  aracılığıyla durağan hâlde şifrelenir. Asla günlüğe kaydedilmez veya düz metin olarak saklanmaz.

## Yayılım (zarif yerinde değiştirme)

1. Bir belirteç döner → yeni belirteç + artırılmış `TokenVersion` kalıcılaştırılır.
2. Barındıran düğümdeki `CopyEngineSupervisor` her uzlaşma döngüsünde planı yeniden okur ve bir **belirteç
   imzası** (erişim belirteçleri + sürümler) hesaplar. Bir değişiklik bir rotasyon anlamına gelir.
3. Host'u yıkıp yeniden başlatmak (bu, master'ın yürütme akışını düşürürdü) yerine, süpervizör **yeni
   belirteci çalışan host'a iter**.
4. Host, etkilenen hesabı `SwapAccessTokenAsync` aracılığıyla **mevcut soket üzerinde** yeniden doğrular
   (tekrar `ProtoOAAccountAuthReq`), ardından hafif bir uzlaşma yapar. Eski belirteç ölür; kopya akışı asla durmaz.

Bu, çapraz-cID durumunu güvenli kılan şeydir: aynı cID'den ikinci bir hesap ekleyen bir kullanıcı çalışma
ortasında eski belirteci geçersiz kılar ve çalışan kopya profili yenisinde devam eder.

## Yenileme

`OpenApiTokenRefreshService` (arka plan), yetkilendirmeleri süre dolmadan önce proaktif olarak yeniler;
`OpenApiAuthorization.IsExpiring(threshold, now)` onu kapılar. cTrader her yenilemede **yenileme**
belirtecini döndürür, bu yüzden yeni yenileme belirteci hemen kalıcılaştırılır; kalıcılaştıramayan
salt-okunur bir önbellek kendini geçersiz kılardı (yazılabilir bir sır kopyası bağlayan küme-içi test İşine ilgili).

### Başarısızlık yükseltmesi

Başarısız bir yenileme sessiz değildir. `OpenApiAuthorization.MarkRefreshFailed(reason, now, criticalWindow)`,
`RefreshFailedAt`'i kaydeder, `ConsecutiveRefreshFailures`'i artırır ve her zaman `AccessTokenRefreshFailed`
(uyarı) yükseltir. Belirteç artık süre dolmasına `App:OpenApi:TokenRefreshCriticalWindow` (varsayılan 6s)
içindeyken ve yenileme hâlâ başarısızken, sahip kopya/prop-firm operasyonları belirteci kaybetmeden önce
yeniden yetkilendirebilsin diye **bir kez** bir `AccessTokenRefreshCritical` alan olayı + `Critical` günlüğü
ile yükseltir. Başarısızlık sayacı ve yükseltme mandalı bir sonraki başarılı `Refresh`'te sıfırlanır. Servis
her `TokenRefreshInterval`'de yeniden denemeye devam eder, böylece bir sağlayıcı/bakım kesintisi yenileme uç
noktası döndüğünde kendini iyileştirir.

## Geçersiz kılma uyarısı ve otomatik kurtarma (M1)

Bir cID'de kısmi/tekrar-yetkilendirme, çalışan bir kopya host'unun hâlâ tuttuğu belirteci geçersiz kılar.
Bir işlem çağrısı `OpenApiErrorKind.TokenInvalid` ile reddedildiğinde, host belirgin bir
**`CopyTokenInvalidated`** uyarısı yükseltir (günlük 1078) — genel bir başarısızlık değil — böylece bildirim
kanalı bir belirtecin dikkat gerektirdiğini bilir. Kurtarma otomatiktir: süpervizör yetkilendirmeyi her
döngüde yeniden okur ve yenilenen belirteç belirteç imzasını değiştirdiğinde, bir **yerinde değiştirme** için
onu çalışan host'a iter — kopyalama manuel yeniden-ekleme olmadan devam eder. Bir `NotLinkable` profil
(belirteç/yetki geçici olarak çözülemez) benzer şekilde her süpervizör döngüsünde yeniden değerlendirilir ve
planı tekrar oluştuğu anda barındırılır.

## Host canlılık gözcüsü (M2)

Süpervizör her barındırılan profilin çalışma görevini izler. Bir host, profili hâlâ bu düğüme atanmışken
çıkar veya arızalanırsa, gözcü onu iptal eder ve bir sonraki döngüde **yeniden başlatır** (günlük
`CopyHostRestarted`), böylece takılan bir host manuel yeniden başlatmaya ihtiyaç duymak yerine kendini
iyileştirir — ve bir profilin başarısızlığı asla diğerlerini durdurmaz (profil başına yalıtım).

## Testler

- **Birim** — `TokenVersion`, `Refresh`'te artar; host yeniden başlatma olmadan yerinde değiştirme yapar;
  çapraz-cID geçersiz kılma, kaynak ve hedef belirteçlerini değiştirir; **geçersiz kılınmış bir hedef
  belirteci `CopyTokenInvalidated` yükseltir ve bir sonraki belirteç itmesinde otomatik kurtarır** (M1); gözcü
  `IsHostDead` kararı tamamlanmış/arızalanmış bir host'u yeniden başlatır ve yeniden atanmış bir profili rahat
  bırakır (M2).
- **Entegrasyon** — `TokenVersion`, gerçek Postgres'te EF üzerinden kalıcılaşır + artar; dize değişmese bile
  belirteç imzası bir sürüm artışında değişir.
