---
description: "Strateji Sağlığı & Alfa Azalması — bir stratejinin son Sharpe değerini daha önceki performansıyla karşılaştıran ve en büyük ortalama kaymasını bulan (CUSUM değişim noktası) belirleyici azalma algılaması, Healthy / Degrading / Decayed verdikti döndürür."
---

# Strateji Sağlığı & Alfa Azalması

Her kenar azalır — araştırma, nicel bir stratejinin yarı ömrünün yıllardan aylara düştüğü konusunda açıktır, bu yüzden *uyarlama keşiften daha iyidir*. Strateji Sağlığı monitörü, bir stratejinin kendi getiri geçmişinden, kenarın hala orada olup olmadığını size söyler.

**cBots → Strategy Health** (`/quant/health`) sayfasını açın.

## Ne yapar

Verilen bir getiri serisi (veya hisse senedi eğrisi, en eski önce) için:

- geçmişi bir **daha erken** ve bir **son** yarısına böler ve Sharpe oranlarını karşılaştırır;
- ortalama en açık şekilde kaydığı gözlemi (rejim değişikliği) bulunacak şekilde **CUSUM değişim noktası** taraması çalıştırır, yalnızca sapma istatistiksel olarak dikkate değer olduğunda raporlanır;
- bir verdikt döndürür:

| Verdikt | Anlamı |
|---|---|
| **Healthy** | Son performans, daha önceki kayıtla uyum içinde veya daha iyidir. |
| **Degrading** | Son Sharpe, daha önceki kayıttan önemli ölçüde daha zayıftır — yakından izleyin. |
| **Decayed** | Kenar etkili bir şekilde son pencerede kaybolmuştur — duraklatmayı düşünün. |
| **Unknown** | Hükme varmak için yeterli geçmiş yoktur. |

- **Bir backtest çalışmasından doğrudan — kopyala-yapıştır yok.** Tamamlanan her backtest, **Backtest** liste satırında ve örnek detay görünümünde bir kalp **Strateji sağlığını denetle** simgesini ortaya çıkarır; bir tıkla, monitörü o çalışmanın saklı hisse senedi eğrisinde çalıştırır ve verdikti bir diyalogda gösterir. Simge, backtest tamamlanıp bir rapor üretilinceye kadar devre dışıdır, bu yüzden hiçbir zaman ölü bir kontrol değildir. Kaputun altında bu `POST /api/quant/health/backtest/{instanceId}` şeklindedir, saklı raporun hisse senedi eğrisini okur.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Neden güvenilirdir

Altyapı bağımlılığı veya harici çağrı olmayan saf, belirleyici etki alanı kodudur (`Core.Health`) — azalmış, azalan, sağlıklı ve çok kısa durumlar ve değişim noktası yerelleştirmesi için birim testli. Bu, otonomus ajanların arkasında yer alan her zaman açık sağlık kontrollerine manuel bir arkadaştır: aynı istatistikler, kenarı solmakta olan canlı bir stratejinin riskini azaltan devre kesiciyi yönlendirir.
