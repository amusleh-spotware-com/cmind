---
slug: /contributing
title: Katkılama
description: cMind'a katkıda bulunmak — insan veya AI destekli PR'ler hoşlanılır. İlk katkı 10 dakikada.
sidebar_position: 5
---

# cMind'a Katkılama 🛠️

Burada olduğunuz için teşekkürler. cMind her zaman biraz daha iyi olur ne zaman birisi bir sorun açsa,
kesin cTrader davranışını bildirir, bu dokümanlar da bir yazım hatasını düzeltir veya bir PR gönderir.
**.NET sihirbazı olmanız gerekmez** — test edenler, tüccarlar ve doküman düzeltmeciler toplama yazanlar kadar değerlidir.

:::tip Kanonik rehber repo'da yaşar
Bu sayfa dostane on-ramp. Tam, her zaman güncel işlem — temel kurallar, kodlama
kuralları, inceleme akışı — **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)** içindedir.
:::

## İlk katkınız ~10 dakikada

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 uyarı, yoksa CI nezaketle sizi reddeder
dotnet test           # birim + entegrasyon + E2E
```

Düzeltilecek bir şey mi buldunuz? Dallanın, değiştirin, test ekleyin ve bir PR açın. Tüm döngü bu.

## Yardımcı olmanın yolları (hepsi kod değil)

| Katkı | Çaba | Nerede |
|---|---|---|
| 🐛 Yeniden üretilebilir bir hata bildirin | 10 dk | [Hata raporu](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Bir özellik önerin | 10 dk | [Özellik isteği](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Bu dokümanları iyileştirin | 15 dk | `website/docs/` altında düzenleyin ve PR gönderin |
| 🧪 Eksik bir test ekleyin | 30 dk | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Kesin cTrader davranışını bildirin | 10 dk | [Bir Tartışma Açın](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Ev kuralları (kısa versiyon)

cMind **gerçek para** hareket ettiriyor, bu nedenle birkaç şey tartışmasız —
ve dürüst olmak gerekirse, bu kod tabanını işlenmesi güzel hale getirir:

- **Katı Alan Odaklı Tasarım.** İş mantığı toplama ve değer nesneleri üzerinde yaşar, asla
  endpoints veya UI'de. (Repo'da bunu için dostane bir oyun kitabı var.)
- **Üç test katmanı, her değişiklik.** Birim + entegrasyon + E2E, *dahil* başarısızlık yolları (düşen
  bağlantılar, reddedilen siparişler, ölü düğümler). Yeşil testler kabul fiyatı.
- **Sıfır uyarı.** `TreatWarningsAsErrors=true`. Modern C# 14 deyimleri.
- **Sır yok, sihirli dize yok, hiçbir zaman `DateTime.UtcNow`** (`TimeProvider` yerine enjekte edin).
- **Dokümanlar aynı commit'te.** Davranış değişti → dokümanını güncelleyin. Evet, bu siteyi de içerir.

Tam detay, her kural arkasındaki *neden* ile, bkz.
[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) ve
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## AI ile katkı yapın 🤖

**AI destekli PR'leri** gerçekten hoşlanıyoruz — bu proje insanlar ve ajanlar tarafından çalışılmak için inşa
edilmiştir. Claude, Copilot veya benzeri'ni sürüyorsanız: bunu
[AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md) 'e işaret edin, iç içe
`CLAUDE.md` dosyalarını okumasına izin verin ve onu aynı seviyeye tutun (testler, sıfır uyarı, DDD).
Bir iyi AI PR, bir iyi insan PR'den ayırt edilemez — aynı inceleme, aynı hoşlanış.

## Birbirine harika ol

[Davranış Kurallarımız](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md) var.
Özü: nazik ol, iyi niyeti varsay ve diğer tarafta bir kişi (veya kişinin ajanı) olduğunu hatırla.
Erkenden soru sor — bu bir kuvvet, değil bir rahatsızlık.

Hoşgeldiniz. Sizin inşa ettiğiniz şeyi görmek için sabırsız kalıyoruz. 🎉
