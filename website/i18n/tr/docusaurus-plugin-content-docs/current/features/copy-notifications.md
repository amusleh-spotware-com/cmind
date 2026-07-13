---
title: Kopyalama Ticareti Bildirimleri
description: Profil durumu uyarıları — başlangıç, durum, hata, manuel durdurma.
sidebar_position: 28
---

# Kopyalama Profili Bildirimleri

Profil durumundaki değişiklikler — başlama, işlem, durdurma, hata — anlık bildirimler.

## Etkinlik Türleri

| Etkinlik | Neden |
|---|---|
| ProfileStarted | Profili etkinleştirildi |
| ProfileStopped | Kullanıcı veya sistem tarafından durduruldu |
| ExecutionError | Sipariş gönderimi başarısız oldu |
| BalanceWarning | Yüksek kayıp yaklaşıyor |
| ProfileCompleted | Target tutarı ulaşıldı |

## Bildirimleri Yapılandırın

Kullanıcı Ayarları > Bildirimleri:

- Etkinlikleri seçin (hangilerine bildir)
- Kanallar (panto, SMS, e-posta)
- Sessizlik aralıkları

## Entegrasyon

WebSocket (canlı) + e-posta (dış):

```json
{
  "Notifications": {
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.example.com"
    },
    "WebSocket": {
      "Enabled": true
    }
  }
}
```

Daha fazla: [Copy Trading →](./copy-trading.md)
