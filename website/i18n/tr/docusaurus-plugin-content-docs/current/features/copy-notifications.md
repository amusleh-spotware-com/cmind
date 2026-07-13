---
description: "Sahip başına, güvenlikle ilgili kopya olaylarının beslemesi — hedefin ret devre kesicisini tetiklemesi, hesap-koruma veya prop-kural ihlali, panik düzleştirme. Varsayılan…"
---

# Kopya operasyonel bildirimleri (Faz 2b)

Sahip başına, güvenlikle ilgili kopya olaylarının beslemesi — hedefin ret devre kesicisini tetiklemesi, hesap-koruma veya prop-kural ihlali, panik düzleştirme. **Varsayılan olarak açık** (`App:Copy:NotificationsEnabled`, varsayılan `true`); susturmak için false yapın. Kopya bağlamında kendi kavramı, piyasa/AI `AlertRule` toplamından ayrıdır.

## Nasıl çalışır

Yürütme-şeffaflığı günlüğüyle aynı bant-dışı host→sink→drainer deseni:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (bildirimler kapalı) NullCopyNotificationSink   → atar (no-op; motor değişmez)
             (bildirimler açık)  ChannelCopyNotificationSink → sınırlı DropOldest kanalı
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  her profilin sahibini çözer, gruplar
                                     ▼
                            CopyNotification beslemesi  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` bloklamayan, asla istisna atmayan — asla DB'ye dokunmaz, kopyayı asla geciktirmez.
- Drainer, sahip olan `UserId`'yi her bildirimin profilinden çözer; profili kaybolan (sahibi çözülemeyen) bildirim yetim bırakılmaz, düşürülür.
- `CopyNotification` = yalnızca-ekleme, satır başına onaylanabilir besleme (toplam değil).

## Ne yükseltilir

| Tür | Önem | Ne zaman |
|------|----------|------|
| `DestinationTripped` | Uyarı | G8 ret bütçesi tükendi; yeni açılışlar bekleme süresi boyunca duraklatılır. |
| `AccountProtectionTriggered` | Kritik | ZuluGuard özkaynak taban/tavanı ihlal edildi; açılışlar mandallandı (SellOut tasfiye eder). |
| `PropRuleBreached` | Kritik | Prop günlük-kayıp / iz-süren-düşüş ihlal edildi; hedef düzleştirildi + gün boyu kilitlendi. |
| `FlattenAll` | Kritik | Panik düzleştirme yürütüldü; her hedef kapatıldı + kilitlendi. |
| `TokenInvalidated` | (ayrılmış) | Bir hedefin belirteci geçersiz kılındı; rotasyon bekleniyor. |

## API

- `GET /api/copy/notifications` (sahip kapsamlı) — kullanıcının tüm profillerdeki son bildirimleri (en yeni 200), artı **onaylanmamış** sayısı.
- `POST /api/copy/notifications/{id}/acknowledge` — birini okundu olarak işaretle.

## Yapılandırma (`App:Copy`)

| Ayar | Varsayılan | Etki |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Güvenlik bildirimleri yay + drainer'ı çalıştır. `false` → no-op sink. |

## Testler

- **Birim** (`CopyNotificationTests`) — tetiklenen hedef `DestinationTripped` yükseltir; panik düzleştirme profil-düzeyi `FlattenAll` yükseltir. Yakalayan sink üzerinden.
- **Entegrasyon** (`CopyNotificationDrainerTests`, gerçek Postgres) — drainer sahibi çözer + kalıcılaştırır; bilinmeyen profil bildirimi düşürülür.
- **DST** — host, no-op varsayılan sink ile at-ve-unut yayar, böylece kopya stres paketi yeşil kalır (23/23).
