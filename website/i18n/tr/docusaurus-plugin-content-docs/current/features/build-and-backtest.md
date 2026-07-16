---
description: "cTrader cBot'larını (C# ve Python, her ikisi de .NET) tarayıcı içi Monaco IDE'sinden oluşturun, çalıştırın, geriye dönük test edin; resmi ghcr.io/spotware/ctrader-console görüntüsü üzerinde çalıştırın."
---

# cBot'ları oluşturun ve geriye dönük test edin

cTrader cBot'larını (C# **ve** Python, her ikisi de .NET) tarayıcı içi Monaco IDE'sinden oluşturun, çalıştırın ve geriye dönük test edin; resmi `ghcr.io/spotware/ctrader-console` görüntüsü üzerinde çalıştırın.

## Oluşturma

- **Builder** sayfası Monaco düzenleyici barındırır; `CBotBuilder` projeyi **geçici kapsayıcı içinde** (`AppOptions.BuildImage`, çalışma dizini `/work` adresinde bağlanmıştır) `dotnet build` ile derler, böylece güvenilmeyen kullanıcı MSBuild hedefleri ana bilgisayara erişemez. NuGet geri yüklemesi paylaşılan birim aracılığıyla derlemeler arasında önbelleğe alınır. Web ana bilgisayarı Docker soketine erişim gerekir.
- C# ve Python başlangıç şablonları `src/Nodes/Builder/Templates/` içinde bulunur.

## Çalıştırma ve geriye dönük test

- **Instances** = TPH durum hiyerarşisi (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Geçiş varlığı değiştirir (id değişikliği), kapsayıcı id taşınır.
- `NodeScheduler` en az yüklü uygun Node seçer; `ContainerDispatcherFactory` uzak Node HTTP aracısına veya yerel Docker dağıtıcısına yönlendirir.
- Tamamlama tarayıcıları çıkan kapsayıcıları uzlaştırır (geriye dönük test kapsayıcıları `--exit-on-stop` aracılığıyla kendileri çıkar); rapor mevcut → tamamlandı (depolanan `ReportJson`), eksik → başarısız.
- Canlı kapsayıcı günlükleri tarayıcıya SignalR üzerinden akışla iletilir; geriye dönük test öz sermaye eğrileri rapordan ayrıştırılır ve çizelgesi oluşturulur.

## Geriye dönük test pazar verileri hesap başına önbelleğe alınır

cTrader Console, geçmiş kene/bar verilerini `--data-dir` içine indirir. Bu dizin, **ticari hesap tarafından tutulan kararlı, kalıcı bir önbellek** (hesap numarası) — Node'un diskinden kendi kapsayıcı yolunda bağlanmıştır (`/mnt/data`), örnek başına çalışma dizininden **ayrı, iç içe olmayan bir bağlantı**. Bu nedenle aynı hesapta her geriye dönük test, zaten indirilen verileri **yeniden kullanır** ve her çalıştırmada yeniden indirmek yerine bu şekilde devam eder. (Daha önce veri dizini örnek başına çalışma dizini altında bulunuyordu; bu da her çalıştırmada id değiştiğinden ve taze indirme zorladığından.) Geçici örnek başına çalışma dizini yine de algoritmayı, parametreleri, parolayı ve raporu tutar; paylaşılan veri önbelleği Node'un geriye dönük test-veri kullanımında sayılır ve Node temizleme eylemi tarafından silinir.

## Geriye dönük test ayarları

**Backtest** diyaloğu, kullanıcı tarafından ayarlanabilir cTrader Console geriye dönük test ayarlarını ortaya çıkarır; böylece komut satırına dokunmanız asla gerekmez:

- **Symbol / Timeframe** — zaman dilimi, **her cTrader döneminin açılır listesi** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` ve Renko/Range/Heikin dönemleri), konsolun kanonik kasasında, böylece her zaman geçerli bir `--period` seçersiniz.
- **From / To** — geriye dönük test penceresi (`--start` / `--end`).
- **Data mode** — üç cTrader modundan biri (`--data-mode`): **Tick data** (`tick`, doğru), **m1 bars** (`m1`, hızlı) veya **Open prices only** (`open`, en hızlı).
- **Starting balance** — `10000` öğesine varsayılan değer (`--balance`). **0 bakiye hiçbir işlem yapmaz ve cTrader'ın boş bir rapor yayması halinde kilitlenmesine neden olur** ("Message expected"), bu nedenle sıfır olmayan bir bakiye her zaman gönderilir.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **0'ın altına inmesi mümkün olmayan pip cinsinden sayısal bir alan**. **Tick data modunda gizlidir**, burada cTrader kurul verilerinin kendisinden kurul türetir (hiçbir `--spread` gönderilmez).

Veri dizini (`--data-file` / `--data-dir`) uygulamanın kendisi tarafından yönetilir (hesap başına önbellek, yukarıya bakınız), diyaloglarda gösterilmez.

:::note cTrader boş geriye dönük testte kilitlenir
Bir geriye dönük test **hiçbir sonuç** üretirse — hiçbir işlem yok veya seçilen tarihler/sembol için pazar verileri yok — cTrader Console'un kendi rapor yazıcısı `Message expected` değerini atar ve raport olmadan çıkar. Uygulama bu yukarı akış hatasını düzeltemez, ama bunu algılar ve örneği **Failed** olarak işaretler; eylem yapılabilir bir nedenle ("seçilen aralık için hiçbir geriye dönük test sonucu...") ham yığın izlemesi yerine. Mevcut pazar verileri olan daha geniş bir tarih aralığı seçin ve yeniden deneyin.
:::

## Örnek ayrıntı sayfası

Bir örneği açma (`/instance/{id}`) canlı durumunu, günlükleri ve — geriye dönük test için — öz sermaye eğrisini gösterir. **Tarayıcı sekme başlığı**, belirli örneği yansıtır (**cBot adı · tür · sembol**, örn. `TrendBot · Backtest · EURUSD`), böylece bir canlı çalıştırma sekmesi ve bir geriye dönük test sekmesi bir bakışta ayırt edilebilir. Aynı cBot'un bir çalıştırması ve bir geriye dönük testi farklı **lineages** olarak izlenir (durum geçişlerine taşınan kararlı bir soy kimliği), böylece sayfa tam olarak bir örneği izler ve asla bir çalıştırmanın verilerini geriye dönük testin verileriyle karışmaz.

## Örnek yaşam döngüsü denetimleri

Her örnek satırı (ve ayrıntı sayfası) duruma uygun denetimler vardır. **Etkin** bir örnek **Stop** gösterir; **terminal** bir örnek (Stopped / Completed / Failed) **Start (▶)** gösterir; bunu aynı cBot, hesap, sembol, zaman dilimi, parametre seti ve görüntü ile yeniden başlatmak için (bir çalıştırma bir çalıştırma olarak yeniden başlatılır, geriye dönük test bir geriye dönük test olarak). Stop'a tıklamak "Stopping…" bildirimini gösterir ve çözdülene kadar simgeyi devre dışı bırakır ve yeni oluşturulan bir çalıştırma listede hemen görünür — sayfa yeniden yükleme yoktur.

Konsol günlükleri **bir örnek sonlandırıldığında saklanır** — çalıştırma için (Stop'da) ve **geriye dönük test** için (tamamlama sırasında) — böylece son çalıştırmanın günlükleri ayrıntı sayfasında görüntülenebilir kalır ve günlük araç çubuğu aracılığıyla **panoya kopyalanır** (Günlükleri kopyala simgesi) veya **indirilir** (Günlükleri indir simgesi) kapsayıcı gittikten sonra bile. Her ikisi de örneğin tam konsol günlüğü üzerinde işlem yapar, yalnızca ekranda görüntülenen kuyruk değil.

**Tamamlanmış geriye dönük test** ayrıca **cTrader raporunu** her iki biçimde saklar — ham **JSON** (öz sermaye eğrisi ve yapay zeka analizi okuyan aynı) ve tam **HTML** raporu. Her ikisi de geriye dönük test satırından **ve** ayrıntı sayfasından özel simgeleri aracılığıyla indirilebilir. Yalnızca **son çalıştırmanın** raporları tutulur ve simgeler **başlangıç yapılmayan, çalışan veya başarısız geriye dönük testler için devre dışı** bırakılır (ve bir çalıştırma örneği için hiçbir zaman gösterilmez) — yalnızca tamamlanmış bir geriye dönük testin indirilebilir bir raporu vardır.

Yüklenen `.algo` asla burada oluşturulmadığından, cBot'lar sayfasındaki **Last Build** sütunu boş bırakılır (tarayıcıda oluşturduğunuz cBot'lar için yalnızca bir oluşturma süresi gösterir).

## Durdurulmuş bir örneği düzenleyin ve yeniden çalıştırın

**Durdurulmuş** bir örnek (çalıştırma veya geriye dönük test) **Edit** denetimi vardır — listede bir satırında simge **ve** ayrıntı sayfasında Başlat/Durdur'un yanında — geçerli yapılandırması ile **önceden doldurulmuş** bir diyaloğu açar. **Ticari hesap, sembol, zaman dilimi, parametre seti ve görüntü etiketini** değiştirebilirsiniz (ve bir geriye dönük test için **pencereyi ve yukarıdaki tüm geriye dönük test ayarlarını**), ardından **Save & start** bunu yeni ayarlarla yeniden başlatır (durdurulmuş örneği değiştirir). Denetim **örnek etkin olsa devre dışı bırakılır** — yalnızca durdurulmuş bir örnek düzenlenebilir.

## Kod düzenleyicisinden çalıştırın

Kod düzenleyicisinde **Run** tıklamak kör, sabit bir çalıştırma tetiklemek yerine bir diyaloğu açar:

- **Ticari hesap** (gerekli) — cBot'un bağlandığı cTrader hesabı.
- **Parameter set** (isteğe bağlı) — mevcut bir seti seçin veya cBot'un **varsayılan parametre değerleriyle** çalıştırmak için boş bırakın. Seçicinin yanındaki **+** düğmesi satır içinde yeni bir parametre seti oluşturur (aşağıya bakınız) ve bunu seçer.
- **Symbol / Timeframe** `EURUSD` / `h1` öğesine varsayılan olur ve değiştirilebilir; **Cancel** veya **Run**.

**Run**'da düzenleyici geçerli kaynağı kaydetmeli + derlemeli, seçilen hesapta seçilen parametrelerle örneği başlatmalı ve sonra canlı kapsayıcı günlüklerini takip etmeli. (Günlük akışı, oturum açan kullanıcının kimlik doğrulama tanımlama bilgisini `/hubs/logs` SignalR hub'ına iletir; böylece `Invalid negotiation response received` hatası yerine bağlanır.)

## Parametre setleri

**Parametre seti**, her parametre adını bir skaler değerle eşleştiren düz JSON nesnesi olarak depolanan, adlandırılmış, yeniden kullanılabilir cBot parametre geçersiz kılmaları setidir; örn. `{"Period": 14, "Label": "trend"}`. Çalıştırma/geriye dönük test sırasında cTrader `params.cbotset` dosyasına çevrilir (`{ "Parameters": { … } }`). cBot'un **Parameter sets** diyaloğundan ham JSON olarak bir seti oluşturabileceğiniz veya Run diyaloğundan satır içinde düzenleyebilirsiniz.

Her parametre seti **bir cBot'a aittir**: New Parameter Set diyaloğu tüm cBot'larınızı listeler ve **bir seti seçmelisiniz** — cBot seçilene kadar oluşturma engellenir. Bir setin **adı cBot başına benzersizdir**: bir setini aynı cBot'un başka bir seti zaten kullanan bir ada oluşturmak veya yeniden adlandırmak reddedilir (diyaloglarda net bir hata, API'de `409 Conflict`). Aynı ad **farklı** cBot'da yeniden kullanılabilir.

JSON **save'de doğrulanır**: tek bir düz nesne olmalı ve değerleri tüm skaler (dize / sayı / bool) olmalıdır. Kök olmayan bir nesne, bir dizi, iç içe bir nesne, bir `null` değer veya yanlış biçimlendirilmiş JSON reddedilir (diyaloglarda net bir hata, API'de `400 Bad Request`). Boş nesne `{}` izin verilir ve "geçersiz kılma yok" anlamına gelir.

## cTrader Console CLI notları

Geriye dönük testler `--data-mode` (varsayılan `m1`) gerektirir; `dd/MM/yyyy HH:mm` olarak tarihler ve `params.cbotset` JSON konumsal argümanı; `run` `--data-dir` reddeder (yalnızca geriye dönük test). Bkz. `ContainerCommandHelpers`.

## Node'lar ve ölçek

Yürütme kapasitesi, Node aracıları ekleyerek ölçeklenir (kendileri kaydol + sinyal); bkz. [node discovery](../operations/node-discovery.md) ve [scaling](../deployment/scaling.md).

## Bir ticari hesap gereklidir

Bir cBot'u çalıştırmak veya geriye dönük test yapmak için bağlanacak bir cTrader ticari hesabı gerekir. **Ticari hesaplar** altında birini ekleyene kadar, **Run New cBot** / **Backtest New cBot** düğmeleri devre dışı bırakılır (araç ipucu ile) ve sayfa hesap kurulumuna bağlayan bir istem gösterir — artık bir hesabı olmayan bir bot'tan ham `stream connect failed` hatası almazsınız.
