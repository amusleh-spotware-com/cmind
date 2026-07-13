---
title: Uyum
description: Denetim günlüğü, 2FA, veri tutma, kişisel bilgi koruma, kuralı izi.
sidebar_position: 20
---

# Uyum

cMind tarafından finanse edilen tüccar zorlukları ve brokerage düzenleme tarafından uyum.

## Denetim Günlüğü

Tüm önemli olaylar Postgres'te kaydedilir:

- Oturum açma / Oturum kapatma
- Hesap Ekleme/Kaldırma
- Ticaret Yürütme
- Para Alış/Satışı
- 2FA Değişikliği

```
SELECT * FROM audit_logs WHERE user_id = ? ORDER BY created_at DESC;
```

## 2FA Zorunlu

2FA'yı dağıtımda zorunlu:

```json
{
  "App": {
    "Branding": {
      "FeaturesOptions": {
        "RequireMfa": true
      }
    }
  }
}
```

## Veri Tutma

Borsaya göre yapılandırın — ticaret günlükleri (7 yıl), denetim günlükleri (5 yıl).

## Kişisel Bilgiler

- Şifreleme durum: `ISecretProtector`
- GDPR: İhracat / silme uç noktaları (planlı)

Daha fazla: [Two-Factor Auth →](./two-factor-auth.md)
