---
slug: /contributing
title: Katkı Sağlama
description: cMind'e nasıl katkıda bulunacağınız — insan veya AI destekli PR'lar hoş geldiniz. İlk katkı 10 dakikada.
sidebar_position: 5
---

# cMind'e Katkı Sağlama 🛠️

Burada olduğunuz için teşekkürler. cMind, birisi bir sorun açtığında, kesin cTrader davranışını bildirdiğinde, bu belgelerdeki bir yazım hatası düzeltdiğinde veya bir PR gönderdiğinde her seferinde daha iyi hale gelir. **.NET sihirbazı olmak zorunda değilsiniz** — test edenler, tüccarlar ve belge düzenleyenleri, agregalar yazan insanlar kadar değerlidir.

:::tip Kanonik kılavuz repo'da yaşıyor
Bu sayfa, dostça bir giriş yoludur. Tam, her zaman güncel olan süreç — temel kurallar, kodlama kuralları, inceleme akışı — **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)** içindedir.
:::

## Yaklaşık 10 dakikada ilk katkınız

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 uyarı, yoksa CI nazikçe sizi reddedecek
dotnet test           # birim + entegrasyon + E2E
```

Düzeltilecek bir şey buldunuz mu? Dallanın, değiştirin, bir test ekleyin ve bir PR açın. Tüm döngü bu.

## Yardımcı olmanın yolları (hepsi kod değil)

| Katkı | Çaba | Nerede |
|---|---|---|
| 🐛 Tekrarlanabilir bir hata bildirin | 10 dk | [Hata raporu](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Bir özellik önerin | 10 dk | [Özellik isteği](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Bu belgeleri iyileştirin | 15 dk | `website/docs/` altında düzenleyin ve PR gönderin |
| 🧪 Eksik bir test ekleyin | 30 dk | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Kesin cTrader davranışını bildirin | 10 dk | [Bir Tartışma Açın](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Ev kuralları (kısa sürüm)

cMind **gerçek para** taşıyor, bu nedenle birkaç şey pazarlanamaz — ve dürüst olmak gerekirse, kodu kullanmak bir zevktir:

- **Kesin Domain-Driven Design.** İş mantığı agregalar ve değer nesneleri üzerinde yaşar, hiçbir zaman uç noktalar veya UI'de değil. (Repo'da onun için dostça bir oyun kitabı var.)
- **Her değişiklik için üç test katmanı.** Birim + entegrasyon + E2E, *başarısız yollar dahil* (düşen bağlantılar, reddedilen emirler, ölü düğümler). Yeşil testler giriş bedeli.
- **Sıfır uyarı.** `TreatWarningsAsErrors=true`. Modern C# 14 deyimleri.
- **Sır yok, sihirli dizeler yok, asla `DateTime.UtcNow`** (`TimeProvider` enjekte edin).
- **Belgeleri aynı commit'te.** Davranışı değiştir → belgesini güncelle. Evet, bu siteyi de içeriyor.

Her kuralın arkasındaki *neden* ile birlikte tam detay, [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) ve [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md) içinde.

## AI ile Katkı Sağlama 🤖

Biz gerçekten **AI destekli PR'ları** memnuniyetle karşılıyoruz — bu proje insanlar kadar ajanlar tarafından çalışılmak için oluşturulmuştur. Eğer Claude, Copilot veya benzeri bir şeyi yönlendiriyorsanız: [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md) adresine işaret edin, iç içe `CLAUDE.md` dosyalarını okumasına izin verin ve aynı standardı tutun (testler, sıfır uyarı, DDD). İyi bir AI PR, iyi bir insan PR'dan ayırt edilemez — aynı inceleme, aynı hoş geldiniz.

## Birbirinize mükemmel davranın

[Davranış Kuralları](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md) var.
İçeriği: kibar olun, iyi niyeti varsayın ve diğer ucunda bir kişinin (veya kişinin ajanının) olduğunu hatırlayın. Erken sorular sorun — bu bir güçlü bir taraftır, rahatsız etmek değildir.

Hoş geldiniz. Nelerin inşa edeceğinizi görmek için sabırsızlanıyoruz. 🎉
