---
description: "Bir master cTrader hesabını bir+ slave hesaba yansıtın — brokerlar arası, cID'ler arası — hedef başına kontrol + para düzeyinde uzlaştırma ile."
---

# Kopya ticaret

Bir **master** cTrader hesabını bir+ **slave** hesaba yansıtın — brokerlar arası, cID'ler arası — hedef başına kontrol + para düzeyinde uzlaştırma ile.

## Kavramlar

- **Kopya profili** — bir master (`SourceAccountId`) + bir+ **hedef**. Yaşam döngüsü: `Draft → Running → Paused → Stopped` (arıza durumunda `Error`). Küme kökü: `CopyProfile` (`CopyDestination`'a sahiptir).
- **Hedef** — bir slave hesabı + master'ın ona nasıl kopyalandığına dair tam kural kümesi. Tüm yapılandırma hedef başınadır, böylece bir master aynı anda muhafazakâr + agresif slave'leri besler.
- **Kopya motoru host'u** — profil için çalışan işçi (`CopyEngineHost`). Master yürütme akışına abone olur, her olayı her hedefe uygular.
- **Süpervizör** — `CopyEngineSupervisor`, her düğümdeki arka plan hizmeti. Atanmış profilleri barındırır, küme boyunca kendini iyileştirir ([ölçeklendirme](../deployment/scaling.md)'ye bakın).

## Ne yansıtılır

| Master olayı | Slave eylemi |
|--------------|--------------|
| Market / market-range pozisyon açma | Boyutlandırılmış bir kopya aç (kaynak pozisyon id'si ile etiketli) |
| Limit / stop / stop-limit bekleyen emir | Eşleşen bekleyen emri yerleştir |
| Bekleyen emir değişikliği | Yansıtılan bekleyen emri yerinde değiştir |
| Bekleyen emir iptali / süre dolması | Yansıtılan bekleyen emri iptal et |
| Kısmi kapatma | Slave pozisyonun aynı oranını kapat |
| Ölçekleme (hacim artışı) | Eklenen hacmi aç (opt-in) |
| Stop-loss / trailing-stop değişikliği | Slave pozisyonun korumasını değiştir |
| Tam kapatma | Slave kopyayı kapat |

Her kopya **kaynak pozisyon/emir id'si ile etiketli**. Yeniden bağlanmadan sonra host, uzlaştırmadan durumu yeniden inşa eder: master'ın tuttuğu ama slave'in eksik olduğu kopyaları açar, master'ın artık tutmadığı slave "yetimlerini" kapatır — **işlemleri çoğaltmadan**.

## Bir profil oluşturma

Kopya Ticaret sayfasındaki **New Profile** iletişim kutusu her şeyi önden toplar: profil adı, kaynak (master) hesap, hedef (slave) hesaplar (**Select all** düğmesiyle çoklu seçim; seçilen master slave listesinden hariç tutulur) + aşağıdaki tam hedef-başına seçenek kümesi. Tüm girdiler **kaydetmeden önce doğrulanır** — eksik ad/kaynak/hedef, pozitif olmayan boyutlandırma parametresi, negatif/tutarsız lot sınırları, aralık-dışı düşüş %, hiçbir emir tipi etkin değil, boş sembol filtresi veya bozuk sembol-eşleme çiftleri bir hata listesi olarak yüzeye çıkar + kaydetmeyi engeller. Onayda profil oluşturulur + her seçilen slave, seçilen ayarlarla eklenir.

Satır eylemleri yaşam döngüsüne saygı gösterir: **Start** yalnızca çalışmıyorken etkin, **Stop** + **Pause** yalnızca çalışırken, **Delete** çalışırken devre dışı + profil + hedefleri kaldırmadan önce onay ister.

## Hedef başına seçenekler

New Profile iletişim kutusunda, Kopya Ticaret sayfasının hedef-başına panelinde veya `POST /api/copy/profiles/{id}/destinations` aracılığıyla ayarlanır:

- **Boyutlandırma** (`MoneyManagementMode` + parametre): sabit lot, lot/nominal çarpanı, orantısal bakiye/öz sermaye/serbest-teminat, sabit risk %, sabit kaldıraç, oto-orantısal, **stop'tan-risk-%** (M7). Artı min/maks lot sınırları + min-lot-zorla. **Stop'tan-risk**, hedefi kendi *öz* bakiyesinin yapılandırılmış yüzdesini riske atacak şekilde boyutlandırır ve bu **master'ın stop-loss mesafesinden** türetilir (`master %2 riske atar → slave otomatik %2 riske atar`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master açılışı stop-loss **olmadan** boyutlandırılacak bir mesafeye sahip değildir → ayarlanmışsa yapılandırılmış **maks-risk yedek lot**'u (M7) kullanır, aksi halde tahmin edilmez, atlanır (`no_stop_loss`). Orantısal-**öz sermaye**/**serbest-teminat**, düz bakiye değil, gerçek hesap **öz sermayesinden** boyutlandırır (`balance + Σ dalgalanan K/Z`, öz sermaye sunmayan cTrader Open API'ye göre türetilir) — böylece açık kâr/zarar üzerinde oturan master kopyaları doğru boyutlandırır. Kullanılan teminat uzlaştırma API'si tarafından açığa çıkarılmadığından, serbest-teminat öz sermaye olarak ele alınır (dürüst mevcut-fon vekili); diğer modlar bakiyeyi okur + ekstra yeniden-değerleme gidiş-dönüşünü atlar.
- **Yön filtresi**: her ikisi / yalnızca-long / yalnızca-short. **Reverse**: karşıt kopya için tarafı çevir (+ SL↔TP takas et).
- **Manage-only** (Yeni-İşlemleri-Yoksay / Yalnızca-Kapat): zaten kopyalanmış pozisyonlarda kapatmaları, kısmi kapatmaları + koruma değişikliklerini yansıt, ama **hiçbir** yeni pozisyon/bekleyen emir açma (atlanır `manage_only`). Mevcut kopyaları kesmeden bir hedefi kademeli olarak kapatmak için kullanın.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (varsayılan açık): profilin **ilk** yeniden senkronizasyonunda, master'ın önceden var olan pozisyonları için kopya açılıp açılmayacağı + profil durdurulmuşken master'ın kapattığı kopyaların kapatılıp kapatılmayacağı. Her ikisi de yalnızca başlangıçta uygulanır — çalışma-ortası yeniden bağlanma her zaman tam olarak uzlaşır, böylece senkron kaybı yine de kurtarılır.
- **Sembol eşlemesi** + **sembol filtresi** (izin listesi / kara liste). Her sembol-eşleme girdisi, o sembol için kopya boyutunu hedefin boyutlandırmasının üzerinde ölçeklendiren isteğe bağlı bir **sembol-başına hacim çarpanı** (cMAM sembol-başına geçersiz kılma) taşır (1 = değişiklik yok). Tüm eşleme **CSV** olarak içe/dışa aktarılır (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; sütunlar `Source,Destination,VolumeMultiplier`) — her satır alan değer nesneleriyle doğrulanır, böylece bozuk bir dosya geçersiz bir eşleme üretemez.
- **Ticaret-saatleri penceresi** (C18) — hedef başına günlük UTC penceresi (`start`/`end` günün dakikaları, son hariç; `start == end` = tüm gün). Pencere dışındaki yeni açılışlar atlanır (`trading_hours`); `start > end` olan pencere gece yarısını aşar (örn. 22:00–06:00). Mevcut pozisyonlar yönetilmeye devam eder.
- **Kaynak-etiket filtresi** (C18, MT sihirli-sayı filtresinin cTrader eşdeğeri) — ayarlandığında, yalnızca etiketi **tam olarak** eşleşen master işlemlerini kopyalar (örn. bir botun işlemleri veya yalnızca-manuel etiket); aksi halde atlanır (`source_label`). Boş = tümünü kopyala. Master pozisyon/emrin `TradeData.Label`'ından `ExecutionEvent.SourceLabel` üzerinde taşınır, yeniden senkronizasyonda da onurlandırılır.
- **Hesap koruması** (ZuluGuard / Küresel Hesap Koruması) — hedefin **canlı öz sermayesini** (`balance + Σ dalgalanan K/Z`, her `CopyDefaults.EquityGuardInterval`'da yoklanır) `StopEquity` tabanına ve/veya isteğe bağlı `TakeEquity` tavanına karşı izle. İhlalde modu uygula: **CloseOnly** (yeni kopyaları durdur, mevcutları yönetmeye devam et), **Frozen** (açmayı durdur), **SellOut** (hedefteki **her** kopyayı hemen kapat). Bir kez tetiklendiğinde hedef mandallanır — host yeniden başlayana kadar yeni açılış yok — + `CopyAccountProtectionTriggered` uyarısı yükseltilir. `SellOut` `StopEquity` gerektirir; `TakeEquity`, `StopEquity`'nin üzerinde olmalıdır. **Garanti-yok uyarısı:** sell-out market yürütmesi kullanır — her rakibin eşdeğeri gibi, hızlı/boşluklu piyasada dolum fiyatını garanti edemez.
- **Flatten-All panik düğmesi** (C8) — `POST /api/copy/profiles/{id}/flatten` her hedefteki **her** kopyalanmış pozisyonu hemen kapatır + yeni açılışlara karşı kilitler. Süreçler-arası yönlendirilir: API bir bayrak ayarlar, süpervizör onu çalışan host'a teslim eder (token-döndürme kanalını yeniden kullanarak), host yerinde düzleştirir; bayrak temizlenir böylece tam olarak bir kez tetiklenir (`CopyFlattenAll` uyarısı). Kullanıcı ardından profili duraklatır/durdurur.
- **Prop-firm kural muhafızı** (C7) — prop-firm kopyalayan kullanıcıların istediği uygulama. Hedef başına, **günlük-zarar tavanı** (günün açılış öz sermayesinden zarar) ve/veya **trailing-drawdown** limiti (çalışan zirve öz sermayesinden zarar), her ikisi de mevduat para biriminde. İhlalde hedef **otomatik-düzleştirilir** (her kopya kapatılır) + UTC gününün geri kalanı için **kilitlenir** (yeni açılışlar atlanır `prop_lockout`); `CopyPropRuleBreached` uyarısı tetiklenir. Kilit, UTC günü döndüğünde temizlenir (taze temel/zirve alınır). Hesap korumasıyla aynı canlı-öz-sermaye yoklamasını paylaşır.
- **Yürütme titremesi** (C11, varsayılan olarak kapalı) — her kopyayı yerleştirmeden önce rastgele `0..N` ms gecikme, kullanıcının **kendi** hesapları arasında neredeyse özdeş emir zaman damgalarını dekorrele etmek için. **Uyumluluk uyarısı:** kopyalamaya *izin veren* prop firmalar için bir yardım — kopyalamayı *yasaklayan* bir firmadan kaçınmak için bir araç **değil**; firmanızın kurallarına uymak sizin sorumluluğunuzdur.
- **Config kilidi** (C9) — hedefin ayarlarını bir süre dondur (`POST …/destinations/{id}/lock` dakikalarla). Kilitliyken hedef kaldırılamaz (küme `CopyDestinationConfigLocked` ile reddeder) — düşüş sırasında dürtüsel değişikliklere karşı kasıtlı bir koruma. Kilit zaman damgasında otomatik olarak sona erer.
- **Tutarlılık ön-uyarısı** (C10) — hedefin **günlük kârı** günün açılış öz sermayesinin yapılandırılmış yüzdesine ulaştığında (UTC günü başına bir kez) uyar (`CopyConsistencyThresholdApproaching`), böylece prop-firm tutarlılık kuralı tetiklenmeden *önce* onurlandırılır. Kâr-tarafı, zarar-tarafı kilitlemeden bağımsız; prop-kural muhafızı ile aynı gün temeliyle çalışır.
- **Emir-tipi filtresi** — hangi master emir tiplerinin kopyalanacağını tam olarak seçin: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` bayrakları; varsayılan tümü). cMAM tarzı seçicilik.
- **Copy SL / Copy TP** — master'ın stop-loss / take-profit'ini yansıt veya korumayı bağımsız olarak yönet.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — her biri bağımsız olarak açılıp kapanabilir.
- **Copy pending expiry** (varsayılan açık) — master bekleyen emrin Good-Till-Date süre-dolma zaman damgasını yansıt.
- **Copy master slippage** (varsayılan açık) — market-range + stop-limit emirleri için, slave emrini master'ın tam kayma-puanı ile yerleştir (temel fiyat slave'in canlı spot'undan alınır).
- **Muhafızlar**: maks düşüş %, günlük zarar tavanı, maks kopya gecikmesi, kayma filtresi (slave fiyatı master girişinden N pip ötesine hareket ettiyse kopyayı atla). **Maks kopya gecikmesi**, enjekte edilen `TimeProvider` aracılığıyla master olayının gerçek sunucu zaman damgasına (`ExecutionEvent.ServerTimestamp`) karşı ölçülür: yapılandırılmış maks-gecikmeden daha eski sinyal atlanır, böylece bayat bir kopya asla geç yerleştirilmez (önceden gecikme her zaman sıfır + muhafız ölüydü).
- **SL/TP hassasiyet normalleştirmesi** (M6) — kopyalanmış stop-loss/take-profit fiyatları değişiklikten önce **hedef** sembolün basamak hassasiyetine yuvarlanır, böylece daha ince hassasiyetteki master fiyatı (veya brokerlar-arası basamak uyumsuzluğu) asla sunucunun `INVALID_STOPLOSS_TAKEPROFIT`'ını tetiklemez.
- **Ret devre kesici / Takipçi Muhafızı** (G8) — arka arkaya `CopyDefaults.RejectionBudget` açılışını reddeden bir hedef **tetiklenir**: soğuma penceresi boyunca yeni açılış yok (`CopyDestinationTripped` uyarısı tetiklenir), bir ret fırtınasının (prop-firm) hesabı zorlamasını durdurur. Mevcut pozisyonlar tetiklenmişken hâlâ yönetilir + kapatılır; kesici soğumadan sonra otomatik sıfırlanır + başarılı bir kopya sayacı temizler.
- **Lot mantık tavanı** (C14) — mutlak maks kopya boyutu ve/veya master'ın katı tavanı. Mutlak tavanı aşan veya master'ın kendi lot boyutunun `N×`'ini aşan hesaplanan kopya **sert-engellenir** (`lot_sanity` atlaması olarak yüzeye çıkar, `cmind.copy.skipped`'de sayılır) yerleştirilmez — felaket-aşırı-boyut sınıfına karşı savunur (0.23-lot master, kaçak bir çarpan veya yuvarlama hatası aracılığıyla her alıcıda 3 lota dönüşür). Her iki boyut da varsayılan `0` (kapalı).

## Güvenilirlik & uç durumlar

Motor, her şeyin her an başarısız olabileceği gerçeklik için inşa edildi:

- **Slave-bekleyen dolum-korelasyon zaman aşımı** (C13) — master bekleyeni kaybolan (ne bekleyen ne de yeni dolan) yansıtılmış slave bekleyeni, korelasyon zaman aşımından sonra iptal edilir, böylece slave kopya korelasyonsuz bir şekilde yönetilmeyen bir pozisyona dolamaz (`CopyPendingTimedOut`). Yeniden senkronizasyon ayrıca emir-id-etiketli dolmuş-bekleyen yetimi temizler.
- **Sağlam kapat/düzleştir** (M8) — yeniden senkronizasyonda yetimi kapatmak veya muhafız ihlalinde düzleştirmek, brokerin zaten kapattığı bir pozisyonu tolere eder (`POSITION_NOT_FOUND`): her kapatma bağımsız çalışır, böylece bir bayat id asla yeniden senkronizasyonu iptal etmez veya hesabın geri kalanını düzleştirilmemiş bırakmaz.

- **Master zaten işlemdeyken başla** — başlangıçta host uzlaşır + master'ın mevcut pozisyonları için kopyaları açar.
- **Bağlantı kopmaları / senkron kaybı** — yeniden bağlanmada host uzlaşır: eksik kopyaları açar, yetimleri kapatır, bekleyenleri yeniden etiketler. Yinelenen emir yok.
- **Emir yerleştirme arızası** — bir hedefteki arıza günlüklenir, diğer hedefleri asla engellemez.
- **cID başına tek geçerli token** — cTrader, yeni bir token verildiği an cID'nin eski erişim token'ını geçersiz kılar. cMind, çalışan host'un token'ını **yerinde** takas eder (canlı sokette yeniden kimlik doğrulama) böylece kopyalama akışı düşürmeden devam eder. [token yaşam döngüsü](token-lifecycle.md)'ne bakın.

## Denetlenebilirlik

Her eylem, profil id'si, hedef cID, emir/pozisyon id'leri + değerlerle yapılandırılmış, kaynak-üretilmiş bir günlük olayı (`LogMessages`) yayar — emir yerleştirildi/atlandı (nedenle birlikte), kısmi kapatma, uygulanan koruma, uygulanan trailing, bekleyen yerleştirildi/değiştirildi/iptal edildi, süre dolması yansıtıldı, market-range kayması yansıtıldı, token takas edildi, yeniden senkronizasyon özeti. Bu, uyumluluk + anlaşmazlık çözümü için denetim izidir.

Günlüklerin yanı sıra, motor `cMind.Copy` sayacında (paylaşılan OTel işlem hattında kayıtlı, geri kalanı gibi OTLP üzerinden / Azure Monitor'a dışa aktarılır) **OpenTelemetry metrikleri** yayar: `cmind.copy.latency` (master-olayı → gönderim, ms), `cmind.copy.dispatch.duration` (tüm hedeflere yayılma, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (hedefe göre etiketli), `cmind.copy.skipped` (nedene göre etiketli) + `cmind.copy.failed`. Bunlar gecikme/kayma gerilemesini yalnızca günlük satırında görünür değil, ölçülebilir yapar — canlı süit bunları bütçeye karşı doğrular.

## API

- `GET /api/copy/profiles` — listele.
- `POST /api/copy/profiles` — oluştur (isteğe bağlı hedef hesap id'leriyle).
- `GET /api/copy/profiles/{id}` — her hedef seçeneği dahil tam ayrıntı.
- `POST /api/copy/profiles/{id}/destinations` — tam seçenek kümesiyle bir hedef ekle.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — kaldır.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — yaşam döngüsü.

## Testler

- **Birim** (`tests/UnitTests/CopyTrading`) — boyutlandırma modları, karar filtreleri, emir-tipi filtresi, süre-dolma kopyası, market-range/stop-limit kayması, SL/TP geçişleri, kısmi kapatma, bekleyen değişiklik/iptal, açık-ile-başla, bağlantı-kesme→senkron-kaybı→yeniden-senkronizasyon, yerinde token takası, cID'ler-arası geçersizleştirme. cTrader'a sadık bellek-içi simülatör olan `FakeTradingSession`'a karşı çalışır.
- **Entegrasyon** (`tests/IntegrationTests/CopyLive`) — düğüm-yakınlığı/kiralama talebi, gerçek Postgres'te token-sürüm yayılımı.
- **E2E** (`tests/E2ETests`) — API + UI aracılığıyla hedef-seçenek gidiş-dönüşü, tam yaşam döngüsü.
- **Stres / DST** (`tests/StressTests`) — deterministik-simülasyon testi: tohumlanmış rastgeleleştirilmiş iş yükleri + arıza enjeksiyonu (soket dalgalanması, emir reddi, market-range reddi, token döndürme, düğüm ölümü) `CopyEngineHost`'u sükunete sürükler + yakınsama değişmezlerini doğrular. [testing/stress-testing.md](../testing/stress-testing.md)'ye bakın. Bu süit gerçek bir başlatma yarışını yüzeye çıkardı + düzeltti: `OnReconnected` ilk referans-yüklemesi + yeniden senkronizasyondan önce bağlanmıştı, böylece başlatma sırasındaki soket dalgalanması ikinci bir yeniden senkronizasyonu eşzamanlı çalıştırabilir + host'un eşzamanlı-olmayan durum sözlüklerini bozabilirdi — başlatma yüklemesi + ilk yeniden senkronizasyon artık `_stateGate` altında çalışır.
- **Canlı** — gerçek cTrader demo hesapları; [testing/live-copy-trading.md](../testing/live-copy-trading.md)'ye bakın.

Canlı + E2E katmanlarının okuduğu tek kimlik-bilgileri dosyası için [dev-credentials.md](../testing/dev-credentials.md)'ye bakın.
## Profil kontrolleri ve hedef yönetimi

Başlat/durdur her profil satırındaki simge düğmeleridir (eylem uygulanmadığında devre dışı). Kaynak ve
hedef hesaplar bir dahili id ile değil, **hesap numaralarıyla** gösterilir. Bir profile tıklamak, hedef
hesaplarını yönetmek için bir **iletişim kutusu** açar (tam hedef-başına ayarlarla ekle/kaldır).
