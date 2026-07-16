---
description: "cTrader cBotlarını (C# ve Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın ve geri test edin; resmi ghcr.io/spotware/ctrader-console görüntüsünde çalıştırın."
---

# cBotları derleme ve geri test etme

cTrader cBotlarını (C# **ve** Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın ve geri test edin; resmi `ghcr.io/spotware/ctrader-console` görüntüsünde çalıştırın.

## Derleme

- **Builder** sayfası Monaco editörünü barındırır; `CBotBuilder` projeyi `dotnet build` ile **tek kullanımlık kapsayıcıda** derler (`AppOptions.BuildImage`, çalışma dizini `/work` adresinde bind-mount edilir), böylece güvenilmeyen kullanıcı MSBuild hedefleri ana bilgisayara ulaşamaz. NuGet geri yüklemesi paylaşılan birim aracılığıyla derlemeler arasında önbelleğe alınır. Web ana bilgisayarının Docker soket erişimine ihtiyacı vardır.
- C# ve Python başlangıç şablonları `src/Nodes/Builder/Templates/` içinde bulunur.

## Çalıştırma ve geri test etme

- **Instances** = TPH durum hiyerarşisi (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Geçiş varlığı değiştirir (id değişimi),
  kapsayıcı id taşınır.
- `NodeScheduler` en az yüklenen uygun düğümü seçer; `ContainerDispatcherFactory` rotayı
  uzak düğüm HTTP aracısına veya yerel Docker gönderici'ye yönlendirir.
- Tamamlama yoklayıcıları çıkılmış kapsayıcıları uzlaştırır (geri test kapsayıcıları `--exit-on-stop` aracılığıyla kendi kendine çıkarlar); rapor mevcut → tamamlandı (depolama `ReportJson`), eksik → başarısız.
- Canlı kapsayıcı günlükleri SignalR üzerinden tarayıcıya akışı yapılır; geri test öz sermaye eğrileri rapordan ayrıştırılır ve grafik hâline getirilir.

## Geri test pazar verileri hesaba göre önbelleğe alınır

cTrader Console, geçmiş kene/çubuk verilerini `--data-dir` içine indirir. Bu dizin, **ticaret hesabı tarafından anahtarlanan (hesap numarasına göre) istikrarlı, kalıcı bir önbellektir** — düğümün diskinden düğümün kendi kapsayıcı yolunda (`/mnt/data`) bind-mount edilir, **örnek başına çalışma dizininden ayrı, iç içe olmayan bir mounttur**. Böylece aynı hesapta her geri test **zaten indirilmiş verileri yeniden kullanır** yerine her çalıştırmada yeniden indirmez. (Daha önce veri dizini, kimliği her çalıştırmada değişen örnek başına çalışma dizininin altında yaşıyordu, bu da her geri testte yeni bir indirmeyi zorladı.) Geçici örnek başına çalışma dizini hâlâ algoritmayı, parametreleri, şifreyi ve raporu tutar; paylaşılan veri önbelleği, bir düğümün geri test veri kullanımında sayılır ve düğüm temiz eyleminde temizlenir.

## Geri test ayarları

**Backtest** iletişim kutusu, kullanıcı tarafından ayarlanabilen cTrader Console geri test ayarlarını ortaya koymaktadır, böylece hiç komut satırına dokunmanız gerekmez:

- **Symbol / Timeframe** — zaman çerçevesi **her cTrader döneminin açılır listesidir** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` ve Renko/Range/Heikin dönemleri), konsolun kanonik büyük harf yazısında, böylece her zaman geçerli bir `--period` seçersiniz.
- **From / To** — geri test penceresi (`--start` / `--end`).
- **Data mode** — üç cTrader modundan biri (`--data-mode`): **Tick data** (`tick`, doğru),
  **m1 bars** (`m1`, hızlı), veya **Open prices only** (`open`, en hızlı).
- **Starting balance** — varsayılan olarak `10000` (`--balance`). **0 bakiye hiç ticaret yapmaz ve cTrader'ın boş bir rapor yayması nedeniyle çökmesine neden olur** ("Message expected"), bu nedenle sıfır olmayan bir bakiye her zaman gönderilir.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **0'ın altına gidemeyecek pips cinsinden sayısal bir alandır**. **Tick data modunda gizlidir**, cTrader yayılmayı kene verilerinden kendisi türetir (`--spread` gönderilmez).

Veri dizini (`--data-file` / `--data-dir`) uygulama tarafından yönetilir (hesaba göre önbellek, yukarıya bakınız), iletişim kutusunda gösterilmez.

:::note cTrader boş geri testte çöker
Geri test **sonuç üretmezse** — hiç ticaret yok veya seçilen tarihler/sembol için pazar verisi yok —
cTrader Console'un kendi rapor yazıcısı `Message expected` atar ve rapor olmadan çıkar. Uygulama bunu yukarı akışlı hatayı düzeltemez, ancak bunu algılar ve örneği **Başarısız** olarak işaretler, uygulanabilir bir nedenle
("seçilen aralık için geri test sonuçları yok…") yerine ham bir yığın izlemesi. Seçili aralığı genişletin
mevcut pazar verisi olan ve yeniden deneyin.
:::

## Örnek detay sayfası

Bir örneği açmak (`/instance/{id}`), canlı durumunu, günlükleri ve — geri test için — öz sermaye eğrisini gösterir. **Tarayıcı sekmesi başlığı** belirli örneği yansıtır (**cBot adı · tür · sembol**, örn.
`TrendBot · Backtest · EURUSD`) böylece canlı bir çalıştırma sekmesi ve geri test sekmesi bir bakışta ayırt edilebilir.
Aynı cBot'un bir çalıştırması ve geri testi, farklı **lineages** olarak izlenir (durum geçişleri arasında taşınan istikrarlı bir lineage id), bu nedenle sayfa tam olarak bir örneği takip eder ve hiçbir zaman bir çalıştırmanın verilerini geri testle karıştırmaz.

## Örnek yaşam döngüsü kontrolleri

Her örnek satırı (ve detay sayfası), durum açısından doğru kontrollere sahiptir. Bir **etkin** örnek
**Stop** gösterir; **terminal** olanı (Stopped / Completed / Failed) aynı cBot, hesap, sembol, zaman çerçevesi, parametre seti ve görüntü ile yeniden başlatmak için **Start (▶)** gösterir (bir çalıştırma çalıştırma olarak yeniden başlar, geri test geri test olarak). Stop'a tıklamak "Stopping…" bildirimi gösterir ve çözülene kadar simgeyi devre dışı bırakır ve yeni oluşturulan çalıştırma listeye hemen görünür — sayfa yeniden yükleme yok.

Konsol günlükleri **bir örnek sonlandığında kalıcıdır** — bir çalıştırma (Stop'ta) ve **geri test** (tamamlandığında) için — böylece son çalıştırmanın günlükleri detay sayfasında görüntülenebilir kalır ve,
günlük araç çubuğu aracılığıyla, **panodan kopyalanmış** (Günlükleri Kopyala simgesi) veya **indirilmiş** (Günlükleri İndir simgesi) kapsayıcı gittikten sonra bile. Her ikisi de örneğin tam konsol günlüğü üzerinde hareket eder, sadece ekran üzerindeki kuyruk değil.

**Yüklenen** bir `.algo` hiçbir zaman burada derlenmediyse, cBots sayfasındaki **Last Build** sütunu boş bırakılır (yalnızca tarayıcıda derlediğiniz cBotlar için derleme zamanı gösterir).

## Durdurulmuş örneği düzenleyin ve yeniden çalıştırın

Durdurulmuş bir örneğin (çalıştırma veya geri test) **Edit** denetimi vardır — listedeki satırında ve detay sayfasında Start/Stop'un yanında bir simge —
mevcut yapılandırması ile **önceden doldurulmuş** bir iletişim kutusunu açar.
**Ticaret hesabı, sembol, zaman çerçevesi, parametre seti ve görüntü etiketini** değiştirebilirsiniz (ve, geri test için,
**pencere ve yukarıdaki tüm geri test ayarları**), ardından **Save & start** yeni ayarlarla yeniden başlatır (durdurulmuş örneği değiştirir). Denetim **örnek etkinken devre dışıdır** —
sadece durdurulmuş bir örnek düzenlenebilir.

## Kod editöründen çalıştırma

Kod editöründe **Run** tuşuna tıklamak, kör, sabit kodlanmış çalışmayı ateşlemek yerine bir iletişim kutusu açar:

- **Trading account** (gerekli) — cBot'un bağlandığı cTrader hesabı.
- **Parameter set** (isteğe bağlı) — mevcut bir seti seçin veya cBot'un
  **varsayılan parametre değerleriyle** çalışmak için boş bırakın. Seçicinin yanında bir **+** düğmesi yeni bir parametre seti oluşturur
  satır içinde (aşağıya bakınız) ve seçer.
- **Symbol / Timeframe** varsayılan olarak `EURUSD` / `h1` ve değiştirilebilir; **Cancel** veya **Run**.

**Run** üzerine editör geçerli kaynağı kaydetme + derler, seçilen hesapta örneği başlatır
seçilen parametrelerle, ardından canlı kapsayıcı günlükleri izler. (Günlük akışı oturum açmış kullanıcının auth tanımlama bilgisini `/hubs/logs` SignalR hub'ına iletir, bu nedenle `Invalid negotiation response received` nedeniyle başarısız olmak yerine bağlanır.)

## Parametre setleri

Bir **parameter set**, her parametre adını skaler bir değerle eşleştiren flat JSON nesnesi olarak depolanan, adlandırılmış, yeniden kullanılabilir bir cBot parametresi geçersiz kılma setidir, örn. `{"Period": 14, "Label": "trend"}`. Çalıştırma/geri test zamanında cTrader `params.cbotset` dosyasına dönüştürülür
(`{ "Parameters": { … } }`). cBot'un **Parameter sets** iletişim kutusundan ham JSON olarak bir set oluşturabilir/düzenleyebilir veya Run iletişim kutusundan satır içi.

Her parametre seti **bir cBot'a aittir**: Yeni Parametre Seti iletişim kutusu tüm cBotlarınızı listeler ve **bir sete seçmelisiniz** — bir cBot seçilene kadar oluşturma engellenir. Setin **adı cBot başına benzersizdir**:
bir seti, aynı cBot'un başka bir setinin zaten kullandığı bir ada oluşturmak veya yeniden adlandırmak reddedilir (iletişim kutusunda açık hata, API'de `409 Conflict`). Aynı ad, **farklı** bir cBot'ta yeniden kullanılabilir.

JSON **kaydedilirken doğrulanmıştır**: tek bir düz nesnesi olmalı ve değerleri tüm skalerlerin (string / number / bool) olmalıdır. Kök olmayan nesne, bir dizi, iç içe nesne, bir `null` değeri veya hatalı biçimlendirilmiş
JSON reddedilir (iletişim kutusunda açık hata, API'de `400 Bad Request`). Boş nesne `{}`
izin verilir ve "geçersiz kılma yok" anlamına gelir.

## cTrader Console CLI notları

Backtests `--data-mode` gerektirir (varsayılan `m1`), tarihler `dd/MM/yyyy HH:mm` ve
`params.cbotset` JSON konumsal argüman; `run` `--data-dir` reddeder (sadece geri test). Bakınız
`ContainerCommandHelpers`.

## Düğümler ve ölçek

Yürütme kapasitesi düğüm aracıları eklenerek ölçeklendirir (kendi kendine kaydolma + kalp atışı). Bakınız
[node discovery](../operations/node-discovery.md) ve [scaling](../deployment/scaling.md).

## Ticaret hesabı gereklidir

Bir cBot'u çalıştırmak veya geri test etmek cBot'un bağlanabileceği bir cTrader ticaret hesabı gerektirir. **Trading accounts** altında bir tane ekleyene kadar,
**Run New cBot** / **Backtest New cBot** düğmeleri devre dışıdır (bir ipucu ile) ve sayfa, hesap kurulumuna bağlantı gösteren bir istem gösterir — artık ham bir
`stream connect failed` hatası almazsınız hesabı olmayan bir bottan.
