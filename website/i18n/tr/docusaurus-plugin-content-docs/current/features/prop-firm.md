---
description: "Perakende prop firmalar (FTMO tarzı) değerlendirme hesapları satar: yatırımcı, risk limitleri (maksimum günlük zarar, maksimum toplam/izleyen düşüş, tutarlılık, zaman limitleri) içinde kalarak kâr hedefine ulaşmalıdır…"
---

# Prop-firm challenge simülasyonu

Perakende prop firmalar (FTMO tarzı) **değerlendirme hesapları** satar: yatırımcı, fonlanmadan önce
risk limitleri (maksimum günlük zarar, maksimum toplam/izleyen düşüş, tutarlılık, zaman limitleri) içinde
kalarak kâr hedefine ulaşmalıdır. cMind, kullanıcının **herhangi bir endüstri şeklinde özel challenge**
oluşturmasına, `TradingAccount`'a bağlamasına, bir kopya-ticaret operasyonu gibi **çalıştırmasına** izin verir —
başlatılan/durdurulan, bir düğümde barındırılan, **cTrader Open API üzerinden canlı olarak** izlenen. Toplam,
her kuralı deterministik olarak değerlendirir; geçiş veya ihlal durumunda, challenge'ı sonlandırır, işaretler,
kullanıcıyı uyarır.

## Domain (sınırlı bağlam: PropFirm)

`PropFirmChallenge` = toplam kök (modül `Core.PropFirm`), `TradingAccount`'una yalnızca güçlü kimlikle
referans verir (çapraz-toplam FK yok). Kural değerlendirmesine, faz/durum makinesine, düğüm
kirasına sahiptir.

### Değer nesneleri ve kural seti

- **`Money`** (negatif olmayan), **`MoneyAmount`** (işaretli), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — toplama beslenen okuma.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — sermaye-dışı gerçekler.
- **`DailyLossLimit`** `(percent, basis)` — temel `Equity` (gün içi, değişken K&Z dahil) veya `Balance`
  (yalnızca gerçekleşen).
- **`DrawdownLimit`** — `Static` (başlangıç bakiyesinden), `TrailingPercent` (zirve sermayesinden) veya
  `TrailingThresholdDollar` (sermaye zirvesini sabit dolar tutarıyla izler, ardından sermaye eşiğe ulaştığında
  **başlangıç bakiyesinde kilitlenir** — vadeli işlem tarzı).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — bir gün toplam kârı domine ederken geçişi engeller.
- **`ChallengeRules`**, yukarıdakilere ek olarak `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep` taşır. Kural matematiği VO'larda bulunur
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); toplam
  orkestre eder.

### Challenge türleri ve şablonlar

`ChallengeTemplates.For(kind)`, `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding` veya `Custom`
(tam kontrol) için geçerli ön ayar oluşturur. UI şablonu önceden doldurur; kullanıcı herhangi bir alanı ayarlayabilir.

### Fazlar ve durum

- **Fazlar:** `Evaluation → Verification → Funded` (tek adım Verification'ı atlar).
- **Durum:** `Active`, `Passed`, `Failed`, artı yaşam döngüsü `Stopped` (izleme duraklatıldı) — `Create` challenge'ı
  `Active` başlatır; `Stop()`/`Resume()` `Active↔Stopped` arasında geçiş yapar.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Kural değerlendirmesi

- **`RecordEquity(EquitySnapshot, now)`** — gün sınırlarında ticaret gününü döndürür (tutarlılık kuralı için
  önceki günün kârını yakalar), zirve/günlük zirveleri günceller, ardından **ilk ihlalde başarısız olur**
  (günlük zarar → düşüş → zaman limiti → hareketsizlik, sırayla) veya kâr hedefi,
  minimum-ticaret-günü, tutarlılık gereksinimlerinin tümü karşılandığında fazı ilerletir. Sıra dışı anlık
  görüntüler ve terminal challenge'daki kayıtlar `DomainException` fırlatır.
- **`RecordActivity(ActivitySnapshot, now)`** — davranış kurallarını değerlendirir (maksimum açık pozisyonlar, hafta sonu
  tutma, haber ticareti), hareketsizlik kuralı için etkinliği damgalar.
- Yumuşak **`PropFirmDrawdownWarning`**, sermaye kullanımı yapılandırılabilir eşiği geçtiğinde bir kez tetiklenir.

Domain olayları: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Canlı izleme (Execution) — düğümde barındırılan, kendi kendini iyileştiren

İzleme, kopya-ticaret barındırma yığınını tam olarak yansıtır; prop tracker = kopya motorunun
**salt-okunur** kuzenidir.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — her düğümde bir `BackgroundService`,
  `App:PropFirm:Enabled` ile kapılıdır. Her döngü, aktif challenge'ları kendi kendini iyileştiren bir kirada
  **talep eder** (`AssignedNode` + `LeaseExpiresAt`; ölü düğümün challenge'ları kira sona erdiğinde geri alınır —
  kopya ticaretle aynı atomik `ExecuteUpdate` talebi, böylece iki düğüm asla çift-izleme yapmaz), kiraları yeniler,
  döndürülen token'ları yerinde iter, challenge'ı `Active` durumundan çıkan host'ları durdurur.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — challenge başına bir tane. Hesap için `IOpenApiTradingSession`
  açar ve `App:PropFirm:EquityPollInterval`'da canlı sermayeyi yeniden hesaplar, toplama
  besler. Rotasyonda erişim token'ını yerinde değiştirir (oturum düşüşü yok). Challenge artık
  `Active` olmadığında çıkar.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-uyumlu sermaye matematiği.
  Sermaye Open API tarafından **teslim edilmez**, bu yüzden türetilir: `equity = balance + Σ(unrealized P&L)`,
  burada her pozisyonun K&Z'si `priceDifference × units × quote→deposit rate + swap + commission`'dır
  (`units = wire volume / 100`; uzun bid'de, kısa ask'te yeniden değerlenir). Bakiye
  `ProtoOATrader`'dan; pozisyonlar (giriş fiyatı, swap, komisyon) reconcile'den; canlı bid/ask spot
  aboneliklerinden. Saf ve izole — para birimi-dönüşümü sıcak noktası kendi başına birim-test edilir.

## Uyarılar

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`), geçiş/ihlal/uyarı domain olaylarına abone olur
(`IDomainEventHandler<>` olarak kaydedilir, başarılı `SaveChanges`'ten sonra gönderilir), kullanıcıyı
yapılandırılmış uyarı/denetim izi aracılığıyla bildirir (`LogMessages`). Canlı UI aynı durum değişikliğini yansıtır. Bu
= çapraz-bağlam tepkisidir — challenge toplamını asla mutasyona uğratmaz.

## API (`/api/prop-firm`, özellik `PropFirm`, rol User+)

| Yöntem | Rota | Amaç |
|--------|-------|---------|
| GET | `/challenges` | kullanıcının challenge'larını listele (tür, faz, durum, canlı sermaye, kira) |
| GET | `/challenges/{id}` | bir challenge |
| GET | `/templates` | oluşturma iletişim kutusu için endüstri ön ayarları |
| POST | `/challenges` | şablondan **veya** tamamen özel kural setinden oluştur |
| POST | `/challenges/{id}/start` | izlemeyi sürdür (Stopped → Active) |
| POST | `/challenges/{id}/stop` | izlemeyi durdur (Active → Stopped, kirayı serbest bırak) |
| POST | `/challenges/{id}/equity` | sermaye anlık görüntüsünü kaydet → yeniden değerlendir (manuel/canlı-akış-yok yolu) |
| DELETE | `/challenges/{id}` | yumuşak-silme (Active iken engellenir) |

MCP: `Mcp/Tools/PropFirmTools.cs`, `PropFirm` özelliğiyle kapılı olarak list/create(şablondan)/record-equity/start/stop'u sunar.

UI: `/prop-firm` (nav *Prop Firm*, `PropFirm` bayrağıyla kapılı), **Start/Stop/Delete**
satır eylemleriyle challenge'ları listeler (Stopped iken Start, Active iken Stop, Active iken Delete devre dışı),
onları `NewPropFirmChallengeDialog` (şablon seçici + tam kural düzenleyici) aracılığıyla oluşturur. Tüm oluşturma/düzenleme MudBlazor iletişim kutusu aracılığıyla.

## Canlı sermaye akışı — çözüldü

Önceki "canlı hesap K&Z akışı yok" boşluğu kapatıldı: `App:PropFirm:Enabled` ayarlandığında, düğümler hesabı
Open API üzerinden canlı izler, sermayeyi otomatik olarak besler. Bunun olmadan (varsayılan), domain ve
**manuel-sermaye** yolu (`POST …/equity`) değişmeden çalışır — derleme/test/E2E için cTrader kimlik bilgisi gerekmez.

## Testler

- **Birim** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (faz ilerlemesi, min-günler, statik/izleyen
  düşüş, günlük zarar, terminal/sıra-dışı korumalar); `PropFirmChallengeRulesTests` (bakiye ve sermaye
  günlük-zarar temeli, izleyen-eşik-dolar izle+kilit, tutarlılık engelle/izin ver, zaman-limiti, hareketsizlik,
  maksimum-maruziyet, hafta sonu, haber, durdur/sürdür, kira sınırı, geçiş kirayı serbest bırakır, düşüş uyarısı);
  `PropFirmValueObjectTests` (VO aralıkları + kural-VO matematiği); `PropFirmEquityCalculatorTests` (uzun/kısa K&Z,
  swap/komisyon, quote→deposit dönüşümü, eksik fiyatlandırma); `PropFirmTrackingHostTests` (canlı sermaye
  genişletilmiş sahte oturuma karşı geçme/kalma'yı yönlendirir); `PropFirmAlertNotifierTests`. Zaman açık /
  `FakeTimeProvider` — duvar-saati okumaları yok.
- **Entegrasyon** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (gidiş-dönüş + record-equity +
  yumuşak-silme, zenginleştirilmiş-kurallar + kira gidiş-dönüşü) ve `PropFirmTrackingLeaseTests` (talep, itiraz edilen kira,
  iki düğüm kimliği arasında sona erdikten sonra geri alma) gerçek Postgres'te.
- **E2E** — `E2ETests/PropFirmTests.cs`: `Passed`'a oluştur + record-equity; stop→start→breach akışı;
  templates uç noktası.
- **Stres / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: birçok karışık-kurallı challenge boyunca
  seed'lenmiş rastgeleleştirilmiş sermaye/etkinlik akışları (gün dönüşleri, ani yükselişler, çöküşler, yinelenen + sıra-dışı
  anlık görüntüler, maruziyet/hafta sonu/haber), yapışkan tam-olarak-bir-kez terminal durumları, zirve-sınırları-geçerli
  değişmezi, gerekçelendirilmiş başarısızlıkları öne sürerek.

## Yapılandırma (`App:PropFirm`)

`Enabled` (varsayılan olarak kapalı), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
