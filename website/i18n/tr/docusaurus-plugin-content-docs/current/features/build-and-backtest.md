---
description: "cTrader cBot'larını (C# ve Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın, geri test edin; resmi ghcr.io/spotware/ctrader-console görüntüsünde çalıştırın."
---

# cBot'ları derleyin ve geri test edin

cTrader cBot'larını (C# **ve** Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın, geri test edin; resmi `ghcr.io/spotware/ctrader-console` görüntüsünde çalıştırın.

## Derleme

- **Builder** sayfası Monaco editörünü barındırır; `CBotBuilder` projeyi `dotnet build` ile derler **geçici kapsayıcıda** (`AppOptions.BuildImage`, çalışma dizini `/work`'de bağlanır), böylece güvenilmeyen kullanıcı MSBuild hedefleri ana makineye erişemez. NuGet geri yüklemesi paylaşılan birim aracılığıyla derlemeler arasında önbelleğe alınır. Web ana bilgisayarı Docker soket erişimine ihtiyaç duyar.
- C# + Python başlangıç şablonları `src/Nodes/Builder/Templates/` dosyasında yer alır.

## Çalıştırma ve geri test

- **Instances** = TPH durum hiyerarşisi (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Geçiş varlığı değiştirir (kimlik değişikliği),
  kapsayıcı kimliği taşınır.
- `NodeScheduler` en az yüklü uygun düğümü seçer; `ContainerDispatcherFactory` uzak düğüm HTTP aracısına veya yerel Docker göndericisine yönlendirir.
- Tamamlama yoklamaları çıkış kapsayıcılarını uzlaştırır (geri test kapsayıcıları `--exit-on-stop` aracılığıyla kendiliğinden çıkar); rapor mevcut → tamamlandı (depo `ReportJson`), eksik → başarısız.
- Canlı kapsayıcı günlükleri SignalR üzerinden tarayıcıya akış yapılır; geri test öz sermaye eğrileri rapor tarafından ayrıştırılır + grafik olarak gösterilir.

## Geri test pazar verileri hesaba göre önbelleğe alınır

cTrader Console, tarihsel kene/bar verilerini `--data-dir` içine indirir. Bu dizin, **ticari hesap tarafından keylenen bir kararlı, kalıcı önbellektir** (hesap numarası) — düğümün diskinden kendi kapsayıcı yolunda bağlanır (`/mnt/data`), **örnek başına çalışma dizininden ayrı, iç içe olmayan bir bağlamadır**. Böylece aynı hesapta her geri test, **yeniden indir** yerine zaten indirilen verileri **yeniden kullanır**. (Daha önceden veri dizini, her çalıştırmada kimliği değişen örnek başına çalışma dizini altında yaşardı; bu da her geri testi yeni bir indirmeye zorladı.) Geçici örnek başına çalışma dizini hâlâ algo, parametreler, şifreyi ve raporu tutar; paylaşılan veri önbelleği bir düğümün geri test veri kullanımında sayılır ve düğüm temizleme işlemi tarafından temizlenir.

## Geri test ayarları

**Backtest** iletişim kutusu, cTrader Console geri test CLI'ının kabul ettiği her ayarı ortaya koymak için komut satırını asla dokunmanız gerekmez:

- **From / To** — geri test penceresi (`--start` / `--end`).
- **Data mode** — `m1` (1 dakikalık çubuk) veya `tick` (`--data-mode`).
- **Starting balance** — `10000` (`--balance`) olan varsayılan. Bir **0 bakiye hiçbir ticaret yapılmamasına neden olur ve cTrader'ı boş bir rapor yayınlattırır ve çöker** ("Message expected"), bu nedenle sıfır olmayan bir bakiye her zaman gönderilir.
- **Commission** ve **Spread** (`--commission` / `--spread`, yayılma pips cinsinden).
- **Advanced options** — cTrader'ın desteklediği diğer geri test seçeneği için satır başına serbest form `name=value` kutusu (örn. `applyCommissionAutomatically=true`); her satır bir `--name value` CLI bağımsız değişkeni olur.

## Örnek ayrıntı sayfası

Bir örneği açmak (`/instance/{id}`) canlı durumunu, günlükleri ve — geri test için — öz sermaye eğrisini gösterir. **Tarayıcı sekmesi başlığı** belirli örneği yansıtır (**cBot adı · tür · sembol**, örn.
`TrendBot · Backtest · EURUSD`), böylece canlı çalışma sekmesi ile geri test sekmesi bir bakışta ayırt edilebilir.
Aynı cBot'un bir çalıştırması ve bir geri testi **soy** olarak izlenir (durum geçişleri arasında taşınan kararlı bir soy kimliği), bu nedenle sayfa tam olarak bir örneği izler ve hiçbir zaman bir çalışmanın verilerini geri testle karıştırmaz.

## Örnek yaşam döngüsü kontrolleri

Her örnek satırı (ve ayrıntı sayfası) durum-doğru kontrollere sahiptir. **Etkin** bir örnek **Stop** gösterir; **terminal** olanı (Stopped / Completed / Failed) **Start (▶)** gösterir, aynı cBot, hesap, sembol, zaman çerçevesi, parametre seti ve görüntüsü ile yeniden başlatmak için (bir çalışma çalışma olarak yeniden başlar, geri test geri test olarak). Stop'a tıklamak "Stopping…" bildirimi gösterir ve çözüme kadar simgeyi devre dışı bırakır ve yeni oluşturulan çalışma hemen listede görünür — sayfa yeniden yüklenmesi yok.

Konsol günlükleri **bir örnek sonlandırıldığında kalıcı** — çalışma için (Stop'ta) ve **geri test** için (tamamlamada) de — böylece son çalışmanın günlükleri ayrıntı sayfasında görülebilir kalır ve günlük araç çubuğu aracılığıyla **panoya kopyalanır** (Günlükleri Kopyala simgesi) veya **indirilir** (Günlükleri İndir simgesi) kapsayıcı gittikten sonra bile. Her ikisi de örneğin tam konsol günlüğü üzerinde hareket eder, yalnızca ekrandaki kuyruk değil.

Yüklenen `.algo`, burada asla derlenmemiştir, bu nedenle cBot'lar sayfasındaki **Last Build** sütunu boş bırakılır (tarayıcıda derlediğiniz cBot'lar için yalnızca yapı süresi gösterilir).

## Durdurulmuş bir örneği düzenleyin ve yeniden çalıştırın

**Durdurulmuş** bir örnek (çalışma veya geri test) bir **Edit** denetimine sahiptir — listede satırında bir simge **ve** ayrıntı sayfasında Start/Stop'un yanında — mevcut konfigürasyonuyla **önceden doldurulmuş** bir iletişim kutusu açar.
**Trading account, symbol, timeframe, parameter set ve image tag** değiştirebilirsiniz (ve geri test için **pencere ve yukarıdaki tüm geri test ayarları**), ardından **Save & start** yeni ayarlarla yeniden başlatır (durdurulmuş örneği değiştirir). Denetim **örnek etkinken devre dışı** — yalnızca durdurulmuş bir örnek düzenlenebilir.

## Kod editöründen çalıştırın

Kod editöründe **Run**'a tıklamak kör, sabit kodlu bir çalıştırmayı başlatmak yerine bir iletişim kutusu açar:

- **Trading account** (gerekli) — cBot'un bağlandığı cTrader hesabı.
- **Parameter set** (isteğe bağlı) — varolan bir set seçin veya cBot'un **varsayılan parametre değerleriyle** çalıştırmak için boş bırakın. Seçicinin yanındaki **+** düğmesi, yeni bir parametre seti satır içinde oluşturur (aşağıya bakın) ve seçer.
- **Symbol / Timeframe** varsayılan olarak `EURUSD` / `h1`'dir ve değiştirilebilir; **Cancel** veya **Run**.

**Run** üzerinde editör geçerli kaynağı kaydeder + derler, seçilen hesapta seçilen parametrelerle örneği başlatır, ardından canlı kapsayıcı günlüklerini izler. (Günlük akışı, oturum açmış kullanıcının auth tanımlama bilgisini `/hubs/logs` SignalR hub'ına iletir, böylece `Invalid negotiation response received` başarısız olmak yerine bağlanır.)

## Parametre setleri

**Parameter set**, her parametre adını skaler bir değerle eşleyen düz bir JSON nesnesi olarak depolanan cBot parametresi geçersiz kılmalarının adlandırılmış, yeniden kullanılabilir bir setidir, örn. `{"Period": 14, "Label": "trend"}`. Çalışma/geri test sırasında cTrader `params.cbotset` dosyasına dönüştürülür
(`{ "Parameters": { … } }`). cBot'un **Parameter sets** iletişim kutusundan bir seti ham JSON olarak oluşturabilir/düzenleyebilir veya Çalıştır iletişim kutusundan satır içinde oluşturabilirsiniz.

Her parametre seti **bir cBot'a aittir**: Yeni Parametre Seti iletişim kutusu tüm cBot'larınızı listeler ve **bir tane seçmelisiniz** — bir cBot seçilene kadar oluşturma engellenir. Bir setin **adı cBot başına benzersizdir**: Aynı cBot'un başka bir seti tarafından zaten kullanılan bir ada bir set oluşturmak veya yeniden adlandırmak reddedilir (iletişim kutusunda net bir hata, API'de `409 Conflict`). Aynı ad **farklı** bir cBot'ta yeniden kullanılabilir.

JSON **kaydetmede doğrulanır**: tek düz nesnesi olan ve değerleri tüm skaler (string / sayı / bool) olması gerekir. Nesne olmayan kök, bir dizi, iç içe geçmiş nesne, `null` değeri veya hatalı biçimlendirilmiş JSON reddedilir (iletişim kutusunda net bir hata, API'de `400 Bad Request`). Boş nesne `{}` izin verilir ve "geçersiz kılma yok" anlamına gelir.

## cTrader Console CLI notları

Geri testler `--data-mode` (varsayılan `m1`), tarihler `dd/MM/yyyy HH:mm` olarak ve `params.cbotset` JSON konumsal arg gerektirir; `run` `--data-dir` reddeder (yalnızca geri test). Bkz. `ContainerCommandHelpers`.

## Düğümler ve ölçek

Yürütme kapasitesi düğüm aracıları eklenerek ölçeklenebilir (kendiliğinden kayıt + kalp atışı). Bkz. [node discovery](../operations/node-discovery.md) ve [scaling](../deployment/scaling.md).

## Ticari hesap gereklidir

Bir cBot'u çalıştırmak veya geri test etmek, bağlanacağı bir cTrader ticari hesabına ihtiyaç duyar. **Trading accounts** altında bir tane ekleyene kadar, **Run New cBot** / **Backtest New cBot** düğmeleri devre dışıdır (bir araç ipucu ile) ve sayfa hesap kurulumuna bağlantı veren bir istem gösterir — artık bot'lar olmayan ham `stream connect failed` hatasından vurmaz hesapla.
