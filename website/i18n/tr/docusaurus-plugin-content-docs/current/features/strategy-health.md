---
description: "Strateji Sağlığı ve Alfa Çürümesi — bir stratejinin son Sharpe'ını daha önceki kaydıyla karşılaştıran ve en büyük ortalama-kaymayı (CUSUM değişim-noktası) bulan deterministik çürüme tespiti, Sağlıklı / Bozuluyor / Çürümüş bir kararı döndürür."
---

# Strateji Sağlığı ve Alfa Çürümesi

Her avantaj çürür — araştırma, bir nicel stratejinin yarı-ömrünün yıllardan aylara çöktüğü konusunda
açıktır, bu yüzden *adaptasyon, keşiften üstündür*. Strateji Sağlığı monitörü, bir stratejinin kendi getiri
geçmişinden, avantajın hâlâ orada olup olmadığını size söyler.

**cBots → Strategy Health** (`/quant/health`) sayfasını açın.

## Ne yapar

Bir getiri serisi (veya özkaynak eğrisi, en eski önce) verildiğinde:

- geçmişi bir **daha önceki** ve bir **son** yarıya böler ve Sharpe oranlarını karşılaştırır;
- ortalamanın en açık şekilde kaydığı gözlemi (bir rejim kırılması) bulmak için bir **CUSUM değişim-noktası**
  taraması çalıştırır, yalnızca sapma istatistiksel olarak dikkate değer olduğunda raporlanır;
- bir karar döndürür:

| Karar | Anlamı |
|---|---|
| **Sağlıklı** | Son performans, daha önceki kayıtla uyumlu (veya daha iyi). |
| **Bozuluyor** | Son Sharpe, daha önceki kayıttan önemli ölçüde zayıf — yakından izleyin. |
| **Çürümüş** | Avantaj, son pencerede etkili biçimde kayboldu — duraklatmayı düşünün. |
| **Bilinmiyor** | Yargılamak için yeterli geçmiş yok. |

```http
POST /api/quant/health
{ "returns": [...] }   // veya { "equity": [...] }
```

## Neden güvenilir

Altyapı bağımlılığı ve dış çağrısı olmayan saf, deterministik alan kodudur (`Core.Health`) — çürümüş,
bozulan, sağlıklı ve çok-kısa durumlar için ve değişim-noktası konumlandırması için birim testlidir. Otonom
ajanları destekleyen her-zaman-açık sağlık kontrollerinin manuel arkadaşıdır: aynı istatistikler, avantajı
solan canlı bir stratejinin riskini azaltan devre kesiciyi yönlendirir.
