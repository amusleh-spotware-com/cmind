---
description: "cTrader cBotlarını (C# ve Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın, geriye dönük test yapın; resmi ghcr.io/spotware/ctrader-console görüntüsünde çalıştırılır."
---

# Build & backtest cBotları

cTrader cBotlarını (C# **ve** Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın, geriye dönük test yapın; resmi `ghcr.io/spotware/ctrader-console` görüntüsünde çalıştırılır.

## Build

- **Builder** sayfası Monaco editörünü barındırır; `CBotBuilder` projeyi **atılabilir kapsayıcıda** `dotnet build` ile derler (`AppOptions.BuildImage`, çalışma dizini `/work` adresine bağlanır), böylece güvenilmeyen kullanıcı MSBuild hedefleri ana makineye erişemez. NuGet geri yüklemesi paylaşılan hacim aracılığıyla yapılar arasında önbelleğe alınır. Web ana makinesinin Docker soket erişimine ihtiyacı vardır.
- C# + Python başlangıç şablonları `src/Nodes/Builder/Templates/` içinde yer alır.

## Run & backtest

- **Instances** = TPH durum hiyerarşisi (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Geçiş varlığı değiştirir (id değişimi), kapsayıcı id'si taşınır.
- `NodeScheduler` en az yüklü uygun düğümü seçer; `ContainerDispatcherFactory` uzak düğüm HTTP aracısına veya yerel Docker gönderimcisine yönlendirir.
- Tamamlama yoklamacıları çıkmış kapsayıcıları uzlaştırır (geriye dönük test kapsayıcıları `--exit-on-stop` yoluyla kendiliğinden çıkar); rapor mevcut → tamamlandı (depo `ReportJson`), eksik → başarısız.
- Canlı kapsayıcı günlükleri SignalR üzerinden tarayıcıya akış yapılır; geriye dönük test özsermaye eğrileri rapor tarafından ayrıştırılır + grafik olarak gösterilir.

## Backtest market data is cached per account

cTrader Console, geçmiş onay işareti/bar verilerini `--data-dir` içine indirir. Bu dizin, **ticari hesap tarafından tutulan kararlı, kalıcı bir önbellek** (hesap numarası) — düğümün diskinden kendi kapsayıcı yolunda bağlanır (`/mnt/data`), örnek başına çalışma dizininden **ayrı, iç içe olmayan bir bağlantı**. Bu nedenle aynı hesapta yapılan her geriye dönük test, **zaten indirilmiş veriler yerine** her çalıştırmada yeniden indirmek zorunda kalmak yerine yeniden kullanır. (Daha önceden veri dizini, her çalıştırmada id'si değişen örnek başına çalışma dizini altında yaşıyordu; bu da her geriye dönük test yeniden indirilmeyi zorunlu kılıyordu.) Kısa ömürlü örnek başına çalışma dizini hala algo, parametreleri, şifreyi ve raporu tutar; paylaşılan veri önbelleği bir düğümün geriye dönük test verisi kullanımında sayılır ve node-clean eylemi tarafından temizlenir.

## Backtest settings

**Backtest** iletişim kutusu, cTrader Console geriye dönük test CLI'nın kabul ettiği her ayarı ortaya koymak için hiçbir zaman bir komut satırına dokunmanız gerekmez:

- **From / To** — geriye dönük test penceresi (`--start` / `--end`).
- **Data mode** — üç cTrader modundan biri (`--data-mode`): **Tick data** (`tick`, kesin), **m1 bars** (`m1`, hızlı) veya **Open prices only** (`open`, en hızlı).
- **Starting balance** — varsayılan olarak `10000` (`--balance`). **0 bakiye hiçbir ticaret yapmaz ve cTrader'in boş bir rapor yayması sebebiyle kilitlenir** ("Message expected"), bu nedenle sıfır olmayan bir bakiye her zaman gönderilir.
- **Commission** ve **Spread** — `--commission` / `--spread` (spread pips cinsinden).
- **Data file** (isteğe bağlı) — geçmiş veri dosyasının node tarafı yolu (`--data-file`); indirilmiş/önbelleğe alınmış veriler kullanmak için boş bırakın.
- **Expose environment variables** — konak ortam değişkenlerini cBot'a geçiren bir geçiş (`--environment-variables` bayrağı).

## Instance detail page

Bir örneğe açılması (`/instance/{id}`) canlı durumunu, günlükleri ve (bir geriye dönük test için) özsermaye eğrisini gösterir. **Tarayıcı sekmesi başlığı** belirli örneği yansıtır (**cBot adı · tür · sembol**, örneğin `TrendBot · Backtest · EURUSD`), böylece canlı çalışma sekmesi ve geriye dönük test sekmesi bir bakışta ayırt edilebilir olur. Aynı cBot'un bir çalıştırması ve bir geriye dönük testi farklı **soylar** (durum geçişleri arasında taşınan kararlı soy id'si) olarak izlenir, bu nedenle sayfa tam olarak bir örneği izler ve hiçbir zaman çalıştırma verilerini geriye dönük test verisiyle karıştırmaz.

## Instance lifecycle controls

Her örnek satırı (ve detay sayfası) durum-doğru denetimlere sahiptir. **Etkin** bir örnek **Stop** gösterir; **terminal** olanı (Stopped / Completed / Failed) aynı cBot, hesap, sembol, zaman dilimi, parametre seti ve görüntü ile yeniden başlatmak için **Start (▶)** gösterir (bir çalıştırma yeniden başlatma olarak yeniden başlatılır, geriye dönük test geriye dönük test olarak yeniden başlatılır). Stop'a tıklamak "Stopping…" bildirimi gösterir ve çözülene kadar simgeyi devre dışı bırakır ve yeni oluşturulan çalıştırma listede hemen görünür — sayfa yeniden yükleme yok.

Konsol günlükleri **bir örnek sona erdiğinde kalıcı** hale getirilir — bir çalıştırma (Durdur'da) ve **geriye dönük test** (tamamlama) gibi — bu nedenle son çalıştırma günlükleri detay sayfasında görüntülenebilir kalır ve günlük araç çubuğu aracılığıyla **panoya kopyalanır** (Günlükleri Kopyala simgesi) veya **indirilir** (Günlükleri İndir simgesi) kapsayıcı gittikten sonra bile. Her ikisi de ekran üzerindeki kuyruğun tamamı değil, örneğin tam konsol günlüğü üzerinde hareket eder.

Yüklenen `.algo` hiçbir zaman burada oluşturulmadığı için **Last Build** sütunu cBotlar sayfasında boş bırakılır (tarayıcıda oluşturduğunuz cBotlar için derleme saati gösterir).

## Edit & re-run a stopped instance

**Durdurulmuş** örnek (çalıştırma veya geriye dönük test) **Edit** denetimine sahiptir — listedeki satırında bir simge **ve** detay sayfasındaki Başlat/Durdur yanında — geçerli yapılandırması ile **önceden doldurulmuş** bir iletişim kutusu açar. **Ticari hesabı, sembolü, zaman dilimini, parametre setini ve görüntü etiketini** değiştirebilirsiniz (ve geriye dönük test için **pencere ve yukarıdaki tüm geriye dönük test ayarları**), ardından **Save & start** bunu yeni ayarlarla (durdurulmuş örneği değiştirerek) yeniden başlatır. Denetim **örnek etkinken devre dışı bırakılır** — yalnızca durdurulmuş örnek düzenlenebilir.

## Run from the code editor

Kod editöründe **Run** düğmesine tıklamak, kör bir sabit kodlanmış çalıştırma ateşlemek yerine bir iletişim kutusu açar:

- **Trading account** (zorunlu) — cBot'ın bağlandığı cTrader hesabı.
- **Parameter set** (isteğe bağlı) — varolan seti seçin veya cBot'ın **varsayılan parametre değerleriyle** çalıştırmak için boş bırakın. Seçicinin yanında bir **+** düğmesi satır içi olarak yeni parametre seti oluşturur (aşağıya bakın) ve seçer.
- **Symbol / Timeframe** varsayılan olarak `EURUSD` / `h1` olur ve değiştirilebilir; **Cancel** veya **Run**.

**Run** üzerinde editör geçerli kaynağı kaydeder + derler, seçilen hesapta seçilen parametrelerle örneği başlatır, ardından canlı kapsayıcı günlüklerini takip eder. (Günlük akışı imzalanmış kullanıcının yetkilendirme tanımlama bilgisini `/hubs/logs` SignalR hub'ına ileterek `Invalid negotiation response received` başarısızlığı yerine bağlanır.)

## Parameter sets

**Parameter set**, her parametre adını bir skaler değerle eşleştiren düz JSON nesnesi olarak depolanan adlandırılmış, yeniden kullanılabilir cBot parametre geçersiz kılmalarının seti, örneğin `{"Period": 14, "Label": "trend"}`. Çalıştırma/geriye dönük test zamanında cTrader `params.cbotset` dosyasına (`{ "Parameters": { … } }`) dönüştürülür. cBot'un **Parameter sets** iletişim kutusu gibi ham JSON'dan bir seti oluşturabilir/düzenleyebilir veya Çalıştır iletişim kutusundan satır içi olarak oluşturabilir.

Her parametre seti **bir cBot'a aittir**: Yeni Parametre Seti iletişim kutusu tüm cBotlarınızı listeler ve **birini seçmeniz gerekir** — seçim yapılmadığı sürece oluşturma engellenir. Bir setin **adı cBot başına benzersizdir**: bir seti aynı cBot'un zaten kullandığı bir ada oluşturmak veya yeniden adlandırmak reddedilir (iletişim kutusunda açık hata, API'de `409 Conflict`). Aynı ad **farklı** cBot'ta **yeniden kullanılabilir**.

JSON **kaydedildiğinde doğrulanır**: tek düz bir nesne olmalı ve değerleri tümü skaler olmalıdır (dize / sayı / bool). Kök olmayan bir nesne, bir dizi, iç içe nesne, `null` değer veya hatalı biçimlendirilmiş JSON reddedilir (iletişim kutusunda açık hata, API'de `400 Bad Request`). Boş nesne `{}` izin verilir ve "geçersiz kılma yok" anlamına gelir.

## cTrader Console CLI notes

Geriye dönük testler `--data-mode` (varsayılan `m1`), `dd/MM/yyyy HH:mm` olarak tarihler ve `params.cbotset` JSON konumsal bağımsız değişkenler gerektirir; `run` `--data-dir` reddeder (yalnızca geriye dönük test). `ContainerCommandHelpers` bölümüne bakın.

## Nodes & scale

Yürütme kapasitesi düğüm aracıları eklenerek ölçeklenir (kendi kendine kayıt + kalp atışı). Bkz. [node discovery](../operations/node-discovery.md) ve [scaling](../deployment/scaling.md).

## A trading account is required

Bir cBot'ı çalıştırmak veya geriye dönük test yapmak bağlanması için bir cTrader ticari hesabı gerekir. **Ticari hesaplar** altında bir tane ekleyene kadar **Run New cBot** / **Backtest New cBot** düğmeleri devre dışı bırakılır (bir ipucu ile) ve sayfa hesap kurulumuna bağlanan bir istem gösterir — artık hesapsız bir bot'tan ham `stream connect failed` hatası almayacaksınız.
