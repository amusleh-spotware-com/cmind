---
description: "Kopya başına yürütme gerçekleri — gecikme, gerçekleşen kayma, dolum vs başarısızlık — her kopya girişiminde yakalanır, profil başına şeffaflık raporu olarak sunulur. Varsayılan…"
---

# Kopya yürütme şeffaflığı (Faz 3)

Kopya başına yürütme gerçekleri — gecikme, gerçekleşen kayma, dolum vs başarısızlık — her kopya
girişiminde yakalanır, profil başına şeffaflık raporu olarak sunulur. **Varsayılan olarak kapalı**;
`App:Copy:TransparencyEnabled=true` ile etkinleştirin. Kapalıyken, kopya motoru bayt bayt değişmez: host
no-op sink'e yayar, hiçbir şey yazılmaz.

## Nasıl çalışır

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (şeffaflık kapalı) NullCopyEventSink   → atar (varsayılan; sıfır sıcak-yol maliyeti)
             (şeffaflık açık)  ChannelCopyEventSink → sınırlı bellek-içi kanal (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  her App drenaj aralığında gruplar
                                   ▼
                          CopyExecution yalnızca-ekleme tablosu  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Sıcak yol G/Ç'den arınmış kalır.** Host `ICopyEventSink.Record(...)` çağırır — bloklamayan,
  asla-atmayan sıraya alma. Asla beklemez, asla DB'ye dokunmaz, asla emir yürütmeyi engellemez.
- **Kayıp, geri-basınca tercih edilir.** Kanal `DropOldest` ile sınırlıdır (`CopyExecutionChannelCapacity`):
  DB drainer takılırsa, bir kopyayı geciktirmek yerine *en eski* şeffaflık satırları düşürülür. Şeffaflık =
  en iyi çaba telemetrisi, işlem bağımlılığı değil.
- **Bant-dışı kalıcılık.** `CopyExecutionDrainer`, kanalı `CopyExecutionDrainInterval`'de gruplar hâlinde
  (`CopyExecutionDrainBatchSize`) boşaltır, `CopyExecution` satırlarını kapsamlı `DataContext` üzerinden yazar.
  Kapanışta son boşaltma.
- **Gerçekler, komutlar değil.** `CopyExecution` = yalnızca-ekleme günlüğü (`InstanceLog`/`AuditLog` gibi),
  toplam değil. Okuma modeli onu doğrudan sorgular (CQRS-lite), bellekte toplar.

## Ne kaydedilir

Bir hedefte kopya girişimi başına bir `CopyExecutionRecord`:

| Tür | Ne zaman | Taşır |
|------|------|---------|
| `Opened` | kopya emri verildi | sembol, yön, tel hacmi, master fiyatı, gerçekleşen kayma (puan), gecikme (ms) |
| `Failed` | kopya açılışı istisna attı/reddedildi | sembol, yön, master hacim/fiyat, gecikme, başarısızlık nedeni (istisna türü) |

(`Closed`/`Skipped`/`Reconciled` gelecekteki genişleme için enum'da mevcuttur.)

## Rapor

`GET /api/copy/profiles/{id}/transparency` (sahip kapsamlı), en yeni 500 gerçek üzerinden şunları döndürür:

- **Özet** — toplam, açılan, başarısız, **dolum oranı**, **ortalama gecikme (ms)**, **ortalama kayma (puan)**.
- **Son** — ham son gerçekler (hedef, kaynak pozisyon, sembol, yön, hacim, master fiyatı, kayma, gecikme, neden, zaman damgası).

## Yapılandırma (`App:Copy`)

| Ayar | Varsayılan | Etki |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Düğüm için kopya başına gerçek yakalama + drainer'ı aç. |

Kanal kapasitesi, drenaj grup boyutu, drenaj aralığı = `CopyDefaults` sabitleri
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Testler

- **Birim** (`CopyTransparencyTests`) — başarılı açılış doğru sembol/yön/hacim/gecikme ile `Opened` gerçeği
  yayar; reddedilen açılış nedenli `Failed` gerçeği yayar. Yakalayan sink üzerinden yürütülür.
- **Entegrasyon** (`CopyExecutionDrainerTests`, gerçek Postgres) — drainer arabelleğe alınan gerçekleri
  `CopyExecution` günlüğüne kalıcılaştırır; boş sink hiçbir şey yazmaz.
- **DST** — host değişikliği no-op varsayılan sink ile at-ve-unut, böylece deterministik kopya stres paketi
  yeşil kalır (23/23).
