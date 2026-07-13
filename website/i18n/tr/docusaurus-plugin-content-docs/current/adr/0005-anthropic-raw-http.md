---
title: "ADR-0005: Anthropic raw HTTP"
---

# ADR-0005: Anthropic raw HTTP

## Bağlam

AI özellikleri dinamik komut istemleri gönderir (kullanıcı veri, zaman damgası, durum). SDK bellek
kaldırır; HTTP istek/yanıt denetim sağlar.

## Karar

`IAiClient`, **Anthropic SDK değildir**; ham HTTP (typed `HttpClient`, JSON el ile):

```csharp
var request = new { model = "claude-3-5-sonnet", messages = [...], max_tokens = ... };
var response = await _http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", request);
```

- Cevap üstün kontrol
- Akış kolayı (Server-Sent Events)
- SDK yükseltme riski yok

## Sonuçlar

- AI yanıtı test edilebilir, sahte hale getirilebilir.
- Başarısız istek yeniden denenebilir, kısaltılabilir.
- Başka sağlayıcılara geçmek basit.
