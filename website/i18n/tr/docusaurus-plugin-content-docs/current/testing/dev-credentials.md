---
title: Geliştirme Kimlik Bilgileri
description: Test hesapları, Sandbox Open API, yerel FakeTradingSession.
---

# Geliştirme Kimlik Bilgileri

Test etmek için geçici kimlik bilgileri — hiçbir zaman üretim anahtarları commit'leyin.

## Paylaşılan Dosya

Repo'da `dev-credentials.json` (veya bireysel kullanıcı doküman dosyası):

```json
{
  "openapi": {
    "clientId": "...",
    "clientSecret": "...",
    "demoAccountId": 123456
  },
  "ai": {
    "apiKey": "sk-..."
  }
}
```

## CI & Local

Local development:
- `dev-credentials.json` gitignore'da
- Bireysel makinelerde oku

CI:
- Secrets yöneticisinden oku (GitHub, Azure)
- Hiçbir zaman commit'leme

## FakeTradingSession

Unit/integration testlerinde:

```csharp
var session = new FakeTradingSession();
session.PlaceOrder(...);
```

Dış API yok, deterministik.

Daha fazla: [Stres Testi →](./stress-testing.md)
