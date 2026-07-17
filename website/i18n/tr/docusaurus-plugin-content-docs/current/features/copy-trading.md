---
description: "Master cTrader hesabını bir veya daha fazla slave hesaba yansıt — broker ve cID arası — hedef başına kontrol + para seviyesi mutabakat ile."
---

# Kopya ticaret

**Master** cTrader hesabını bir veya daha fazla **slave** hesaba yansıt — broker ve cID arası — hedef başına kontrol + para seviyesi mutabakat ile.

## Kavramlar

- **Kopya profili** — bir master (`SourceAccountId`) + bir veya daha fazla **hedef**. Yaşam döngüsü: `Taslak → Çalışıyor → Duraklatılmış → Durdurulmuş` (hata durumunda `Hata`). Toplam kök: `CopyProfile` (`CopyDestination` sahibi).
- **Hedef** — bir slave hesap + master'ın üzerine nasıl kopyalanacağını gösteren tam kural seti. Tüm yapılandırma hedef başına, bu yüzden bir master hem tutucu hem de agresif slave'leri aynı anda besleyebilir.
- **Kopya motoru ana bilgisayarı** — profil için çalışan işçi (`CopyEngineHost`). Master yürütme akışına abone olur, her olayı her hedefe uygular.
- **Denetçi** — `CopyEngineSupervisor`, her düğüm üzerinde arka plan servisi. Atanan profilleri barındırır, küme genelinde kendini onartır (bkz. [ölçeklendirme](../deployment/scaling.md)).

## Neler yansıtılır

| Master olayı | Slave işlemi |
|--------------|--------------|
| Pazar / pazar aralığı pozisyon açılması | Boyutlu bir kopya aç (kaynak pozisyon kimliği ile etiketli) |
| Limit / stop / stop-limit beklemede olan emir | Eşleşen beklemede olan emri yaz |
| Beklemede olan emir değiştirme | Yansıtılan beklemede olan emri yerinde değiştir |
| Beklemede olan emir iptal / sona erme | Yansıtılan beklemede olan emri iptal et |
| Kısmi kapatma | Slave pozisyonun aynı oranını kapat |
| Ölçekte giriş (hacim artırma) | Eklenen hacmi aç (katılım seçeneği) |
| Stop-loss / trailing-stop değişikliği | Slave pozisyonun korumasını değiştir |
| Tam kapatma | Slave kopyası kapat |

Her kopya **kaynak pozisyon/emir kimliği ile etiketli**. Yeniden bağlantıdan sonra host reconcile'dan durumu yeniden oluşturur: master'ın tuttuğu ancak slave'in kaçırdığı kopyaları açar, master'ın artık tutmadığı slave "yetim"leri kapatır — **işlemleri çoğaltmadan**.

## Profil oluşturma

**Yeni Profil** adanmış bir **tam sayfa** formu açar (`/copy-trading/new`), iletişim kutusu değil — seçenek seti telefon ve masaüstünde sayfanın daha iyi okunmasını sağlayacak kadar büyüktür. Her şeyi önceden toplar: profil adı, kaynak (master) hesap, hedef (slave) hesaplar (tüm seçeneği seç düğmesi ile çoklu seçim; seçilen master slave listesinden dışlanır), + tam hedef başına seçenek seti. **Her kontrol, ne yaptığını ve nasıl kullanılacağını açıklayan bir yardım ipucunda taşır**. Yapılandırılmış girdiler **uygun doğrulanan kontroller** kullanır — sayılar/yüzde sayısal alan aracılığıyla, modlar/yön/filtre seçenekler aracılığıyla, sembol filtresi sembol yongaları ekleme/kaldırma listesi aracılığıyla ve sembol haritası `Kaynak → Hedef (× çarpan)` satırlarının ekleme/kaldırma tablosu aracılığıyla — asla virgülle ayrılmış metin blobu değil. Tüm girdiler **kaydedilmeden önce doğrulanır** — eksik ad/kaynak/hedef, negatif olmayan boyutlandırma parametresi, negatif/tutarsız lot sınırları, aralık dışında çekiliş %, hiçbir emir türü etkinleştirilmemiş, veya boş sembol filtresi bir hata listesi olarak ortaya çıkar + kaydetmeyi engeller. Oluşturmada, profil oluşturulur + seçilen her slave seçilen ayarlarla eklenir, sonra sayfa Kopya Ticaret listesine döner.

**İçeri/Dışarı Aktar.** Tüm ayarlar bloğu **bir JSON dosyasına aktarılabilir** ve **tekrar yüklenebilir** formla ön doldurulabilir, böylece ayarlama birden fazla profilde yeniden yazılmadan yeniden kullanılabilir. Sembol haritası da benzer şekilde **bir CSV dosyası olarak aktarılabilir/yüklenebilir** (`Kaynak,Hedef,HacimÇarpanı`) — geniş bir broker-sembol haritasını bir elektronik tablodan hazırlayın ve tek adımda yükleyin. Aynı sembol kontrolleri ve CSV içeri/dışarı aktarma, Kopya Ticaret sayfasında hedef iletişim kutusunda da mevcuttur.

Satır işlemleri yaşam döngüsünü saygı duyar: **Başla** sadece çalışmadığında etkinleştirilir, **Durdur** + **Duraklat** sadece çalışırken, **Sil** çalışırken devre dışı + kaldırmadan önce profili + hedefleri kaldırmayı sorar.

## Hedef başına seçenekler

Yeni Profil sayfasında, Kopya Ticaret sayfasında hedef iletişim kutusunda veya `POST /api/copy/profiles/{id}/destinations` aracılığıyla ayarlayın:

- **Boyutlandırma** (`MoneyManagementMode` + parametre): sabit lot, lot/nominal çarpan, orantılı bakiye/öz sermaye/serbest marj, sabit risk %, sabit kaldıraç, otomatik orantılı, **risk-%-stop'tan** (M7). Artı min/max lot sınırları + min-lot'u zorlayın. **Stop'tan Risk** hedefi yapılandırılmış yüzdesini riske atmak için boyutlandırır *kendi* bakiyesi, **master'ın stop-loss mesafesinden** türetilir (`master %2'yi riske atar → slave otomatik %2'yi riske atar`): `lotlar = bakiye×% ÷ (stopMesafe × sözleşmeBoyutu)`. Master açık **stop-loss olmadan** boyutlandırılacak mesafeye sahip değildir → ayarlanmış **max-risk geri dönüş lot** (M7) kullanır varsa, aksi takdirde atlanır (`stop_loss_yok`) tahmini değildir. Orantılı-**öz sermaye**/**serbest marj** gerçek hesap **öz sermayesinden** boyutlandırılır (`bakiye + Σ kayan P&L`, cTrader Açık API'si tarafından türetilir öz sermayeyi teslim etmez), düz bakiye değil — bu yüzden master açık kar/zarar üzerinde oturan doğru boyutlandırılan kopyalar. Kullanılan marj reconcile API tarafından açığa çıkarılmadığından, serbest marj öz sermaye olarak işlem görür (dürüst mevcut fonlar proxy'si); diğer modlar bakiyeyi oku + ekstra revaluasyon tur atlayın.
- **Yön filtresi**: her ikisi / sadece uzun / sadece kısa. **Tersine Çevir**: tarafı çevir (+ SL↔TP'yi değiştir) muhalif kopya için.
- **Yalnızca Yönet** (Yeni İşlemleri Yoksay / Kapat'a Yalnızca): kopyalanan pozisyonlar üzerinde kapatmaları, kısmi kapatmaları + koruma değişikliklerini yansıt, fakat **hiçbir** yeni pozisyon/beklemede olan emirler açma (atlanır `manage_only`). Mevcut kopyaları kesmeden hedefi indir etmek için kullanın.
- **Başlangıç'ta Senkronize Aç** / **Başlangıç'ta Senkronize Kapat** (varsayılan açık): profil **ilk** resenkronizasyonunda, master'ın önceden var olan pozisyonları için kopyalar açıp açmayacak + profil durdurulduğu sırada master kapatmış kopyaları kapatıp kapatmayacak. Her ikisi sadece başlangıçta uygulanır — orta yolda yeniden bağlantı her zaman tam olarak mutabakat sağlar böylece desenkronizasyon iyileşir bağımsız olarak.
- **Sembol haritası** + **sembol filtresi** (beyaz liste / kara liste). Her sembol haritası girişi isteğe bağlı **sembol başına hacim çarpanı** (cMAM sembol başına geçersiz kılma) taşır hedef'in boyutlandırmasının üzerine kopyayı boyutlandırır (1 = değişiklik yok). Tüm harita **CSV** olarak içeri/dışarı aktar (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; sütunlar `Kaynak,Hedef,HacimÇarpanı`) — her satır alan değeri nesne aracılığıyla doğrulanır, bu yüzden hatalı biçim haritayı geçersiz kılamaz.
- **Ticaret saatleri penceresi** (C18) — hedef başına günlük UTC penceresi (`başlangıç`/`bitiş` günün dakikaları, bitiş hariç; `başlangıç == bitiş` = bütün gün). Pencere dışında yeni açılışlar atlanır (`trading_hours`); `başlangıç > bitiş` ile pencere gece yarısını geçer (örn. 22:00–06:00). Mevcut pozisyonlar yönetilen kalır.
- **Kaynak etiketi filtresi** (C18, MT sihirli sayı filtresinin cTrader eşdeğeri) — ayarlandığında, sadece master işlemleri etiketi **tam olarak** eşleştiren (örn. bir bot'un işlemleri, veya manual-sadece etiketi) kopyala; aksi takdirde atlanır (`source_label`). Boş = tümünü kopyala. Master pozisyon/emir'in `TradeData.Label` olan `ExecutionEvent.SourceLabel` üzerinde taşınır, resenkronizasyonda da onurlandırılır.
- **Hesap koruması** (ZuluGuard / Küresel Hesap Koruması) — hedef'in **canlı öz sermayesini** (`bakiye + Σ kayan P&L`, `CopyDefaults.EquityGuardInterval` her) `StopEquity` tabanı ve/veya isteğe bağlı `TakeEquity` tavanı karşı izleyin. İhlal üzerine, modu uygulayın: **Kapat'a Yalnızca** (yeni kopyaları durdur, mevcut olanları yönet), **Dondurulmuş** (açılmayı durdur), **Sat** (hedef üzerinde **her** kopyayı hemen kapat). Bir kez ateşlendiğinde, hedef kilitlenmiş — ev sahibi yeniden başlatılana kadar yeni açılış yok — + `CopyAccountProtectionTriggered` uyarısı yükseltilir. `SellOut` `StopEquity` gerektirir; `TakeEquity` `StopEquity` üzerine oturmalıdır. **Garanti yok uyarısı:** satış çıkışı pazar yürütmesini kullanır — her rakip eşdeğeri gibi, hızlı/boşluk pazar'ında doldurma fiyatını garanti edemez.
- **Tümünü Düzle panik düğmesi** (C8) — `POST /api/copy/profiles/{id}/flatten` hemen **her** kopyalanan pozisyonu her hedefte kapatır + yeni açılışlara karşı kilitler. Çapraz işlem yönlendirilir: API bayrağı ayarlar, denetçi çalışan ana bilgisayara teslim eder (token-rotasyon kanalını yeniden kullan), yerinde düzleştirir; bayrak temizlenir böylece tam bir kez ateşlenir (`CopyFlattenAll` uyarı). Kullanıcı ardından profili duraklat/durdur.
- **Prop-firma kuralı koruması** (C7) — prop-firma kopya-yapan kullanıcıların talep ettiği yaptırım. Hedef başına, **günlük kayıp sınırı** (günün açılış öz sermayesinden kayıp) ve/veya **trailing-drawdown** sınırı (çalışan tepe öz sermayesinden kayıp), her ikisi de para biriminde. İhlal üzerine hedef **otomatik düzleştirilir** (her kopya kapatılır) + **kilitli** geri kalan UTC günü (yeni açılışlar atlanır `prop_lockout`); `CopyPropRuleBreached` uyarısı ateşlenir. Kilit UTC günü değiştiğinde temizlenir (taze taban çizgisi/tepe alınır). Hesap koruması ile aynı canlı-öz sermaye anketi paylaşır.
- **Yürütme titremesi** (C11, varsayılan olarak kapalı) — her kopyayı yerleştirmeden önce rasgele `0..N` ms gecikme, **kendi** hesapları arasında benzer sipariş zaman damgaları ayrılmak. **Uyum uyarısı:** kopyalamaya *izin veren* prop firmalarına yardımcı — kopyalamayı yasaklayan firmayı kaçınmak için araç değil; firmanızın kurallarında kalmak sizin sorumluluğunuzdur.
- **Yapılandırma kilidi** (C9) — hedef'in ayarlarını dönem için dondur (`POST …/destinations/{id}/lock` dakikalarla). Kilitli iken, hedef kaldırılamaz (toplam `CopyDestinationConfigLocked` ile reddeder) — çekiliş sırasında dürtüsel değişikliklere karşı kasıtlı koruma. Kilit zaman damgasında otomatik olarak sona erer.
- **Tutarlılık ön uyarısı** (C10) — uyar (UTC günde bir kez) hedef'in **günlük kar** yapılandırılmış yüzdesine ulaştığında günün açılış öz sermayesinin (`CopyConsistencyThresholdApproaching`), bu yüzden prop-firma tutarlılık kuralı *öncesinde* saygı duyulur. Kar tarafı, kayıp tarafı kilidi bağımsız; prop-kural koruması ile aynı gün tabanında çalışır.
- **Emir türü filtresi** — tam olarak hangi master emir türlerinin kopyalanacağını seçin: pazar, pazar-aralığı, limit, stop, stop-limit (`CopyOrderTypes` bayrakları; varsayılan tümü). cMAM tarzı seçicilik.
- **SL Kopyala / TP Kopyala** — master'ın stop-loss / take-profit'i yansıt, veya korumayı bağımsız olarak yönet.
- **Trailing stop Kopyala**, **kısmi kapatmayı yansıt**, **ölçek girişi yansıt** — her biri bağımsız olarak açılıp kapatılabilir.
- **Sona ermeyi Kopyala** (varsayılan açık) — master beklemede olan emir'in Good-Till-Date sona erme zaman damgasını yansıt.
- **Master kaymasını Kopyala** (varsayılan açık) — pazar-aralığı + stop-limit emirleri için, slave emrini master'ın tam kayma-puan (taban fiyat slave'in canlı spottan alınır) ile yaz.
- **Korumalar**: max çekiliş %, günlük kayıp sınırı, max kopya gecikmesi, kayma filtresi (slave fiyat master girişinden N pips ötesine taşınmışsa kopyayı atla). **Max kopya gecikmesi** master olayı'nın gerçek sunucu zaman damgasına (`ExecutionEvent.ServerTimestamp`) karşı enjekte `TimeProvider` aracılığıyla ölçülür: yapılandırılan max-lag'dan daha eski sinyal atlanır, bu yüzden bayat kopya hiçbir zaman geç yürütülmez (daha önce gecikme her zaman sıfır + koruma ölü).
- **SL/TP hassas normalizasyonu** (M6) — kopyalanan stop-loss/take-profit fiyatları amend öncesinde **hedef** sembolün basamak hassasiyetine yuvarlanır, bu yüzden master fiyat daha ince hassasiyet (veya broker arası basamak uyuşmazlığı) asla sunucunun `INVALID_STOPLOSS_TAKEPROFIT` tetiklemez.
- **İmza devresi kırıcı / Takipçi Koruması** (G8) — hedef `CopyDefaults.RejectionBudget` emirleri sırasında reddediliyor **tetiklendi**: cooldown penceresi için yeni açılış yok (`CopyDestinationTripped` uyarısı ateşlenir), reddetme fırtınasının (prop-firma) hesabını çekiçlemesini durdurur. Mevcut pozisyonlar tetiklenirken yönetilir + kapatılır; devre breaker cooldown sonrasında ve başarılı kopya sonrasında otomatik olarak sıfırlanır.
- **Lot sağduyu tavanı** (C14) — mutlak maksimum kopya boyutu ve/veya multiple-of-master sınırı. Hesaplanan kopya mutlak sınırı aşan, veya master'ın kendi lot boyutunun `N×`sini aşan, **zor blok** (`lot_sanity` atla olarak yüzeylenmiş, `cmind.copy.skipped` sayılır) değil yürütülür — felaket-oversize sınıfına karşı savunur (0,23-lot master her alıcıda runaway çarpanı veya yuvarlama hatası aracılığıyla 3 lota dönüştürülür). Her iki boyut varsayılan `0` (kapalı).

## Güvenilirlik & kenar durumları

Motor herhangi bir zaman her şeyin başarısız olabileceği gerçeğine göre inşa edilmiştir:

- **Slave-pending doldurma-korelasyon zaman aşımı** (C13) — yansıtılan slave beklemede olan master beklemede olan kaybolmuş (ne istirahat ne de taze doldurulmuş) korelasyon zaman aşımından sonra iptal edilir, bu yüzden slave kopya korelasyonsuz yönetilmeyen pozisyona dolduramaz (`CopyPendingTimedOut`). Resenkronizasyon ayrıca emir-kimliği-etiketli doldurulmuş-beklemede olan yetimi temizler.
- **Güçlü kapatma/düzleştirme** (M8) — resenkronizasyonda yetimi kapatma, veya koruma ihlali üzerine düzleştirme, broker zaten kapatmış (`POSITION_NOT_FOUND`) tolere eder: her kapatma bağımsız çalışır, bu yüzden bir bayat kimlik hiçbir zaman resenkronizasyonu durdurmuyor veya hesabı un-flattened bırakmıyor.

- **Master zaten işlemlerde başla** — başlangıçta host mutabakat + master'ın mevcut pozisyonları için kopyaları açar.
- **Bağlantı kopması / desenkronizasyon** — yeniden bağlantıda host mutabakat sağlar: kopyaları aç, yetim kapatma, pendingler yeniden etiketle. Yinelenen emirler yok.
- **Emir yerleştirme hatası** — bir hedefte hata günlüğe kaydedilir, asla diğer hedefleri engellemiyor.
- **cID başına tek geçerli token** — cTrader yeni bir tane verildiği anda cID'in eski erişim tokenini geçersiz kılar. cMind çalışan ana bilgisayarın tokenini **yerinde** değiştirir (canlı soket üzerinde yeniden doğrulanır) böylece akış kesilmeden kopyalama devam eder. Bkz. [token yaşam döngüsü](token-lifecycle.md).

## Denetlenebilirlik

Her işlem yapılandırılmış, kaynak-oluşturulan günlük olayı (`LogMessages`) profil kimliği, hedef cID, emir/pozisyon kimlikleri, + değerleri — emir yürütüldü/atlandı (nedeni), kısmi kapatma, koruma uygulandı, trailing uygulandı, pending yürütüldü/değiştirildi/iptal edildi, sona erme yansıtıldı, pazar-aralığı kayması yansıtıldı, token değiştirildi, resenkronizasyon özeti ile yayınlar. Bu, uyum + anlaşmazlık çözümü için denetim izi'dir.

Günlüklerin yanında, motor **OpenTelemetry metriklerini** `cMind.Copy` işçide yayınlar (paylaşılan OTel boru hattında kayıtlı, OTLP / Azure Monitor'e diğeri gibi dışarı aktarılır): `cmind.copy.latency` (master-olay → gönderme, ms), `cmind.copy.dispatch.duration` (tüm hedeflere fan-out, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (hedef tarafından etiketli), `cmind.copy.skipped` (nedene tarafından etiketli), + `cmind.copy.failed`. Bunlar gecikme/kayma gerilemeleri sadece günlük satırında görülür değil — canlı test onları bütçeye karşı doğrular.

## API

- `GET /api/copy/profiles` — liste.
- `POST /api/copy/profiles` — oluştur (isteğe bağlı hedef hesap kimlikleri ile).
- `GET /api/copy/profiles/{id}` — tam detay her hedef seçeneği dahil.
- `POST /api/copy/profiles/{id}/destinations` — tam seçenek seti ile hedef ekle.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — kaldır.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — yaşam döngüsü.

## Testler

- **Unit** (`tests/UnitTests/CopyTrading`) — boyutlandırma modları, karar filtreleri, emir-türü filtresi, sona erme kopya, pazar-aralığı/stop-limit kayması, SL/TP açıp kapatma, kısmi kapatma, pending değiştir/iptal, açık başla, bağlantı kesilme→desenkronizasyon→resenkronizasyon, yerinde token değişim, çapraz-cID geçersiz kılma. `FakeTradingSession` karşı çalışır, cTrader-sadık bellek-içi benzeticisi.
- **Integration** (`tests/IntegrationTests/CopyLive`) — düğüm-ilişkisi/kira iddiası, gerçek Postgres'de token sürümü yayılması.
- **E2E** (`tests/E2ETests`) — hedef seçeneği tur API + UI aracılığıyla, tam yaşam döngüsü.
- **Stres / DST** (`tests/StressTests`) — belirlenmiş benzetim test: tohum rasgele iş yükleri + hata enjeksiyonu (soket çırpması, emir reddi, pazar-aralığı reddi, token rotasyonu, düğüm ölümü) `CopyEngineHost` sürüklenir ve yakınsama değişmezlerini doğrular. Bkz. [testing/stress-testing.md](../testing/stress-testing.md). Bu test paketi gerçek başlangıç yarışını keşfetti + düzeltti: `OnReconnected` ilk referans yüklemesi ve resenkronizasyondan önce kablolu, böylece başlangıç sırasında soket çırpması ikinci resenkronizasyonu eşzamanlı olarak çalıştırıp ana bilgisayarın eş zamansız olmayan durum sözlüklerini bozabilir — başlangıç yüklemesi + ilk resenkronizasyon şimdi `_stateGate` altında çalışır.
- **Canlı** — gerçek cTrader demo hesapları; bkz. [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Canlı + E2E katmanlarının okuduğu tek kimlik bilgisi dosyası için bkz. [testing/dev-credentials.md](../testing/dev-credentials.md).

## Profil kontrolleri ve hedef yönetimi

Başla/Durdur her profil satırında ikon düğmelerdir (işlem geçerli değilken devre dışı). Kaynak ve hedef hesapları **hesap numarası** tarafından gösterilir, asla iç kimliği değil. Bir profile tıklamak hedef hesaplarını yönetmek için **diyalog** açar (tam hedef başına ayarlarla ekle/kaldır).
