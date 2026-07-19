# Commitment of Traders (COT)

cMind, yerleşik bir **Commitment of Traders** raporuyla gelir — ABD vadeli işlem piyasasında kim
uzun ve kısa konumda olduğunun (ticari koruma sağlayanlar, büyük spekülatörler, fonlar) haftalık
CFTC dökümü, etkileşimli tarihsel grafikler, normalleştirilmiş **COT endeksi**, cBots için
kimliği doğrulanmış REST API ve AI istemcileri için MCP araçları ile birlikte geliyor. Veriler
doğrudan **CFTC halka açık Socrata veri setlerinden** geliyor — API anahtarı yok, toplayıcı yok.
Ekonomik takvim gibi, bu da ticari çekirdeği etkilemeden devre dışı bırakılabilen ayrılmış bir
modüldür.

## Size Ne Verir

- **Üç rapor ailesi de, yalnızca vadeli işlem ve vadeli işlem + seçenekler birleştirildi:**
  - **Miras** — Ticari olmayan (büyük spekülatörler), Ticari (koruma sağlayanlar), Raporlanmayan.
  - **Kırılmış** — Üretici/Tüccar, Takas Yönetmenleri, Yönetilen Para, Diğer Raporlananlar.
  - **Finansal Vadeli İşlemler Tüccarları (TFF)** — Dilercik, Varlık Yöneticisi, Kaldıraçlı Fonlar,
    Diğer Raporlananlar.
- **Seçilmiş bir pazar kataloğu** — FX majörleri, altın/gümüş/bakır, ham petrol ve doğal gaz,
  Hazineler, hisse senedi endeksleri, kripto ve ana tahıllar/yumuşak emtialar — her biri kararlı
  CFTC sözleşme koduna eşlendi ve burada belirsizse, ticareti yapılabilen bir sembole (örn. Euro FX
  → `EURUSD`, Altın → `XAUUSD`).
- **COT endeksi (0–100)** — mevcut spekülatör net pozisyonu tarihsel aralığında nerede oturuyor
  (varsayılan ~3 yıl geri bakış). Uç değerlere yakın okumalar, genellikle bir dönüşüyü önceden
  bildiren kalabalık konumlandırmayı işaret eder; rapor bir **uzun ekstrem** (≥80) veya **kısa
  ekstrem** (≤20) etiketler.
- **Zamanın belirli bir noktasında doğruluk.** Haftalık bir rapor Salı günü ölçülür ancak ancak
  ertesi Cuma günü kamuya açık hale gelir; her okuma bu yayın anını onurlandırır, bu nedenle
  geriye dönük test edilen bir konumlandırma sinyali asla raporu yayınlanmadan önce görmez (ileriye
  bakmaz).

## Sayfayı Kullanma

Sol gezintiden **Commitment of Traders**'ı açın. Bir **pazar**, bir **rapor türü** (Miras /
Kırılmış / Mali) seçin ve **Vadeli işlem + seçenekler**'i değiştirerek yalnızca vadeli işlem ile
birleştirilmiş varyant arasında geçiş yapın. Sayfa gösterir:

- **Zamanla Net Konumlandırma** — her tüccar kategorisinin net konumunun (uzun − kısa) tarih
  penceresi boyunca etkileşimli bir çizgi grafiği.
- **COT endeksi** — 0–100 endeksinin çizgi grafiği, en son okumasıyla ve aşırı etiketi ile.
- **Son anlık görüntü** — tüccar kategorisi başına uzun / kısa / net / açık faiz yüzdesi tablosu, ayrıca
  toplam açık faiz ve rapor tarihi.

Her grafiğin **yakınlaştır/uzaklaştır** (ve sıfırla) araç çubuğu düğmeleri var ve zaman ekseni üzerinde sürükleyerek yakınlaştırabilirsiniz. **CSV Dışa Aktar**, seçili pazar ve rapor türünün tam haftalık geçmişini elektronik tablo için hazır bir dosya olarak indirir. Birden fazla pazarı tek bir grafikte örtüştürmek için **Pazarları Karşılaştır**'ı kullanın — karşılaştırma grafikler, seçili her pazarın spekülatör net konumunu ve COT endeksini yan yana çizerek, pazarlar arası konumlandırmayı bir bakışta okumanızı sağlar.

## Veriler Nasıl Akar

Veritabanı önbellektir. Haftalık yutma işçisi, izlenen pazarlar için altı CFTC veri setini çeker, pazar kataloğunu upsert eder ve her yeni raporu **idempotently** ekler (yeniden çalıştırma hiçbir zaman anlık görüntüyü çoğaltmaz). Ayrıca veriler **talep üzerine yüklenir**: bir pazar ilk kez istendiğinde CFTC kaynağından alınır ve depolanır ve sonraki her istek doğrudan veritabanından sunulur. Önbellek **yeni haftalık raporlar yayınlandığında yenilenir** — depolanan en yeni rapor bir haftadan daha yaşlı olduğunda, sonraki istek başarısız ve en son verileri şeffaf bir şekilde ekler (kaynağın asla aşırı yüklenmemesi için kısıtlanır). İlk yükleme birkaç yıl geçmişini geri doldurur; kaynak kesintisi en iyi önbelleğe alınmış verileri sunmaya düşer. Anahtar olmadan kutudan çıktı gibi her şey çalışır; isteğe bağlı Socrata uygulaması jetonlaması oran sınırını yükseltir.

## Yapılandırma

Tüm tuşlar `App:Cot` altında yaşıyor (bkz. [özellik geçişleri](./feature-toggles.md) ve
[beyaz etiket sahibi ayarları](./white-label-owner-settings.md)):

| Anahtar | Varsayılan | Amaç |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Haftalık yutma işçisinin çalışıp çalışmadığı. |
| `PollInterval` | `6h` | İşçi CFTC veri setlerini ne sıklıkta yoklama. |
| `BackfillYears` | `5` | İlk çalıştırmada çekilen geçmiş yılları. |
| `ReconcileLookbackWeeks` | `4` | Her döngüde revizyonları yakalamak için yeniden senkronize edilen son haftalar. |
| `SocrataAppToken` | — | Anonim oran sınırını yükselten isteğe bağlı jeton. |
| `CotIndexLookbackWeeks` | `156` | COT-endeks aralığı olarak kullanılan haftalık raporlar (~3 yıl). |

## Kapı

Görünürlük iki katmanlı bir kapıdır, ekonomik takvime özdeştir: yapı seviyesi sabit kapı
`App:Branding:EnableCot` **ve** çalışma zamanı geçişi `App:Features:Cot`. Her ikisi kapatıldığında
nav bağlantısı, sayfası, REST API'si ve MCP araçları kaybolur (API `404` döndürür). Veri kaynağı
anahtarsız olduğundan veri kaynağı anahtarı kapısı yoktur — etkinleştirilmiş, görünür anlamına gelir.

## Geliştiriciler İçin

- Etki alanı: `Core.Cot` — `CotMarket` ve `CotReport` bölümleri, `CotPositions` değer nesnesi,
  `CotIndexCalculator` etki alanı hizmeti ve `ICotReports` / `ICotSource` portları.
- Altyapı: `Infrastructure.Cot` — `CftcSocrataSource` yolsuzluğa karşı koruma ayrıştırıcısı, oran
  kapısı, yalnızca ekleme yazma hizmeti, okundu tarafı ve haftalık yutma işçisi (EF `cot` şeması).
- cBot & AI erişimi: [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) ve MCP araçları
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
