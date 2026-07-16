---
description: "cTrader cBotlarını (C# ve Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın, backtest yapın ve resmi ghcr.io/spotware/ctrader-console görüntüsünde çalıştırın."
---

# cBotları derleme ve backtest

cTrader cBotlarını (C# **ve** Python, her ikisi de .NET) tarayıcı içi Monaco IDE'den derleyin, çalıştırın ve backtest yapın; resmi `ghcr.io/spotware/ctrader-console` görüntüsünde çalıştırın.

## Derleme

- **Builder** sayfası Monaco editörü barındırır; `CBotBuilder` projeyi atılabilir konteyner içinde `dotnet build` ile derler (`AppOptions.BuildImage`, çalışma dizini `/work`'de bağlanır), böylece güvenilmeyen kullanıcı MSBuild hedefleri ana makineye ulaşamaz. NuGet restore paylaşılan bir hacim aracılığıyla derleler arası önbelleğe alınır. Web sunucusu Docker soket erişimine ihtiyaç duyar.
- C# + Python başlangıç şablonları `src/Nodes/Builder/Templates/` içinde bulunur.

## Çalıştırma ve backtest

- **Instances** = TPH durum hiyerarşisi (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Geçiş varlığı değiştirir (id değişimi),
  konteyner id taşınır.
- `NodeScheduler` en az yüklü uygun düğümü seçer; `ContainerDispatcherFactory` uzak düğüm HTTP aracısına veya yerel Docker dağıtıcısına yönlendirir.
- Tamamlanma polleyerleri çıkış konteynerleri uzlaştırır (backtest konteynerleri `--exit-on-stop` aracılığıyla kendinden çıkar); rapor mevcutsa → tamamlandı (store `ReportJson`), eksikse → başarısız.
- Canlı konteyner günlükleri SignalR üzerinden tarayıcıya akış halinde; backtest eşitlik eğrileri rapor ve grafiklerden ayrıştırılır.

## Backtest pazar verileri hesap başına önbelleğe alınır

cTrader Console tarihsel tick/bar verileri `--data-dir` dizinine indirir. Bu dizin **ticaret hesabında tutulan bir kararlı, kalıcı önbellek** (hesap numarası) — düğümün diskinden kendi konteyner yolunda bağlanır (`/mnt/data`), örnek başına çalışma dizininden **ayrı, iç içe olmayan bir bağlama**. Bu nedenle aynı hesapta yapılan her backtest **zaten indirilen verileri yeniden kullanır** ve her çalıştırmada yeniden indirmek yerine kullanır. (Daha önce veri dizini, kimliği her çalıştırmada değişen örnek başına çalışma dizini altında bulunuyordu, bu da her backtest için yeni bir indirmeyi zorladı.) Geçici örnek başına çalışma dizini yine de algo, parametreler, şifre ve rapor tutar; paylaşılan veri önbelleği bir düğümün backtest-veri kullanımında sayılır ve düğüm-temizle eylemi tarafından temizlenir.

## Backtest ayarları

**Backtest** iletişim kutusu, kullanıcı tarafından ayarlanabilir cTrader Console backtest ayarlarını ortaya koymak için tasarlanmıştır, böylece komut satırına asla dokunmanız gerekmez:

- **Symbol / Timeframe** — zaman çerçevesi, cTrader döneminin her birinin **açılır listesi** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` ve Renko/Range/Heikin dönemleri), konsolun kanonik büyük/küçük harf yazımında, böylece her zaman geçerli bir `--period` seçersiniz.
- **From / To** — backtest penceresi (`--start` / `--end`).
- **Data mode** — üç cTrader modundan biri (`--data-mode`): **Tick data** (`tick`, doğru),
  **m1 bars** (`m1`, hızlı) veya **Open prices only** (`open`, en hızlı).
- **Starting balance** — varsayılan olarak `10000` (`--balance`). **0 bakiye hiçbir işlem yapmaz ve cTrader'in boş bir rapor yayınlamasına neden olur ve sonra kilitlenmeye** ("Message expected"), bu nedenle her zaman sıfır olmayan bir bakiye gönderilir.
- **Commission** ve **Spread** — `--commission` / `--spread` (pips cinsinden spread).

Veri dizini (`--data-file` / `--data-dir`) uygulama tarafından kendisi yönetilir (hesap başına cache, yukarıya bakın), iletişim kutusunda açığa çıkmaz.

## Örnek detay sayfası

Bir örneği açma (`/instance/{id}`) canlı durumunu, günlükleri ve — bir backtest için — eşitlik eğrisini gösterir. **Tarayıcı sekmesi başlığı** belirli örneği yansıtır (**cBot adı · tür · symbol**, örn.
`TrendBot · Backtest · EURUSD`), böylece canlı çalışma sekmesi ve bir backtest sekmesi bir bakışta ayırt edilir.
Aynı cBotun çalıştırılması ve backtesti **lineages** olarak izlenir (durum geçişleri arasında taşınan kararlı bir lineage id), böylece sayfa tam olarak bir örneği takip eder ve asla çalıştırmanın verilerini backtest'in verileriyle karıştırmaz.

## Örnek yaşam döngüsü kontrolleri

Her örnek satırı (ve ayrıntı sayfası) durum açısından doğru kontrollere sahiptir. **Etkin** bir örnek **Durdur**'u gösterir; **terminal** olan bir örnek (Durduruldu / Tamamlandı / Başarısız) aynı cBot, hesap, symbol, zaman çerçevesi, parametre seti ve görüntü ile **Başlat (▶)** gösterir (bir çalıştırma çalıştırma olarak yeniden başlatılır, bir backtest backtestdir). Durdur'a tıklamak "Durduruluyor..." bildirimi gösterir ve çözülene kadar ikonu devre dışı bırakır ve yeni oluşturulan çalıştırma hemen listede görünür — sayfa yeniden yüklenmez.

Konsol günlükleri **bir örnek sonlandırıldığında kalıcı hale getirilir** — çalıştırma için (Durdur'da) ve **backtest** için (tamamlama sırasında) — böylece son çalıştırmanın günlükleri detay sayfasında görüntülenebilir kalır ve **günlük araç çubuğu aracılığıyla, panoya kopyalanabilir** (Günlükleri kopyala simgesi) veya **indirilir** (Günlükleri indir simgesi) konteyner gittikten sonra bile. Her ikisi de ekran üzerindeki kuyruk değil, örneğin tam konsol günlüğü üzerinde hareket eder.

Yüklenen bir `.algo` burada hiçbir zaman derlendiğinden, cBots sayfasında **Son Derleme** sütunu boş bırakılır (tarayıcıda derlediğiniz cBotlar için yalnızca derleme zamanı gösterir).

## Durdurulan bir örneği düzenleme ve yeniden çalıştırma

Durdurulan bir örneğin (çalıştırma veya backtest) **Edit** kontrolü vardır — listede satırda bir simge **ve**
detay sayfasındaki Başlat/Durdur'un yanında — bir iletişim kutusu açar **önceden doldurulmuş** mevcut yapılandırmasıyla.
**Ticaret hesabını, symbolu, zaman çerçevesini, parametre setini ve görüntü etiketini** değiştirebilirsiniz (ve backtest için, **pencereyi ve yukarıdaki tüm backtest ayarlarını**), sonra **Kaydet ve başlat** yeni ayarlarla yeniden başlatır (durdurulan örneği değiştirir). Kontrol **örnek etkin iken devre dışı bırakılır** — yalnızca durdurulan bir örnek düzenlenebilir.

## Kod editöründen çalıştırma

Kod editöründe **Çalıştır**'a tıklamak kör, hard-coded bir çalıştırma yerine bir iletişim kutusu açar:

- **Ticaret hesabı** (gerekli) — cBotun bağlandığı cTrader hesabı.
- **Parametre seti** (isteğe bağlı) — mevcut bir seti seçin veya cBotun **varsayılan parametre değerleriyle** çalıştırmak için boş bırakın. Seçicinin yanında bir **+** düğmesi yeni bir parametre seti oluşturur (aşağıya bakın) ve seçer.
- **Symbol / Timeframe** varsayılan olarak `EURUSD` / `h1` olarak ayarlanır ve değiştirilebilir; **İptal** veya **Çalıştır**.

**Çalıştır**'da editör geçerli kaynağı kaydeder + derler, seçilen hesapta seçilen parametrelerle örneği başlatır, sonra canlı konteyner günlüklerini izler. (Günlük akışı oturum açan kullanıcının kimlik doğrulama tanımlama bilgisini `/hubs/logs` SignalR hub'ına iletir, böylece `Invalid negotiation response received` ile başarısız olmak yerine bağlanır.)

## Parametre setleri

**Parametre seti** adlandırılmış, yeniden kullanılabilir bir cBot parametre geçersiz kılma seti olarak depolanan düz bir JSON nesnesi - her parametre adını bir skaler değerle eşleştiren, örn. `{"Period": 14, "Label": "trend"}`. Çalıştırma/backtest zamanında cTrader `params.cbotset` dosyasına dönüştürülür
(`{ "Parameters": { … } }`). cBotun **Parametre setleri** iletişim kutusundan ham JSON olarak bir set oluşturabilir/düzenleyebilir veya Çalıştır iletişim kutusundan satır içi olarak oluşturabilirsiniz.

Her parametre seti **bir cBota aittir**: Yeni Parametre Seti iletişim kutusu tüm cBotlarınızı listeler ve **bir tane seçmeniz gerekir** — seçim yapılana kadar oluşturma engellenir. Bir setin **adı cBot başına benzersizdir**:
bir seti, aynı cBotun başka bir setinin zaten kullandığı bir ada oluşturmak veya yeniden adlandırmak reddedilir (iletişim kutusunda net bir hata, API'de `409 Conflict`). Aynı ad **farklı** bir cBota yeniden kullanılabilir.

JSON **kaydedilirken doğrulanır**: kök değerleri tüm skalerler olan (string / number / bool) tek bir düz nesne olmalıdır. İç içe olmayan bir kök, bir dizi, iç içe bir nesne, bir `null` değeri veya hatalı biçimlendirilmiş JSON reddedilir (iletişim kutusunda net bir hata, API'de `400 Bad Request`). Boş bir nesne `{}` izin verilir ve "geçersiz kılma yok" anlamına gelir.

## cTrader Console CLI notları

Backtestler `--data-mode` gerektirir (varsayılan `m1`), tarihler `dd/MM/yyyy HH:mm` olarak ve
`params.cbotset` JSON pozisyonal argüman; `run` `--data-dir` reddeder (sadece backtest). `ContainerCommandHelpers` bölümüne bakın.

## Düğümler ve ölçek

Yürütme kapasitesi düğüm aracıları eklenerek ölçeklenir (kendinden kaydol + kalp atışı). [node discovery](../operations/node-discovery.md) ve [scaling](../deployment/scaling.md) bölümüne bakın.

## Ticaret hesabı gereklidir

cBotu çalıştırmak veya backtest yapmak için cTrader ticaret hesabına bağlanmanız gerekir. **Ticaret hesapları** altında bir tane ekleyene kadar, **Yeni cBot Çalıştır** / **Yeni cBot Backtest** düğmeleri devre dışı bırakılır (bir ipucu ile) ve sayfa hesap kurulumuna bağlayan bir istem gösterir — artık hesabı olmayan bot'dan ham `stream connect failed` hatasına çarpmazsınız.
