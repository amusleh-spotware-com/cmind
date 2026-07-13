---
title: Yüklenebilir Uygulamalar (PWA)
description: cMind'i masaüstü ve mobil olarak yükleyin — çevrimdışı özellikler, push bildirimleri, başlangıç.
sidebar_position: 32
---

# Yüklenebilir Web Uygulaması (PWA)

cMind PWA: Safari, Chrome, Edge'de yüklenebilir.

## Kurulum

### Mac / Windows / Linux

1. **Chrome / Edge**: Adres çubuğunda "Yükle" seçeneğini tıklayın (veya aplikasyon menüsü)
2. **Safari**: Paylaş > Ana Ekrana Ekle

### iOS

Safari:
1. Paylaş simgesine vurmak
2. Ana Ekrana Ekle
3. Adı ayarlayın ve ekleyin

### Android

Chrome:
1. Menü (3 nokta)
2. Uygulamayı Yükle
3. Onaylayın

## Özellikler

- **Başlangıç Ekranından Başlatma**: Bir tıkla açıyor
- **Çevrimdışı**: Çevrimdışı öz hizmet (sınırlı)
- **Push Bildirimleri** (İsteğe bağlı): Pano güncellemeleri, uyarılar
- **Masaüstü Ikon**: Beyaz etiketli ikon

## Yapılandırma

PWA adı, ikon, teması beyaz etiketli ayarlardan:

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "LogoUrl": "/branding/logo.png"
    }
  }
}
```

Daha fazla: [Beyaz Etiketli →](./white-label.md)
