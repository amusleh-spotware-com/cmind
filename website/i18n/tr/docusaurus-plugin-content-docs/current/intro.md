---
slug: /intro
title: cMind'e hoş geldiniz
description: cMind'e sıcak bir giriş — cTrader için açık kaynaklı, kendi kendine barındırılabilen ticaret operasyonları platformu.
sidebar_position: 1
---

# cMind'e hoş geldiniz 👋

:::warning[Alfa yazılım — üretime hazır değil]
cMind aktif geliştirme aşamasındadır. Kaba kenarlar, sürümler arası kırıcı değişiklikler ve hâlâ
geliştirilmekte olan özellikler bekleyin. **Topluluğu şekillendirmemize yardımcı olacak topluluk
test edicileri, hata raporlayıcıları ve erken katkıda bulunanlar arıyoruz.** Bir sorunla
karşılaşırsanız,
[bildirin](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) —
gerçek dünya geri bildiriminiz şu anda katkıda bulunabileceğiniz en değerli şeydir.
:::

Yani ticaret botları oluşturmak, dizüstü bilgisayarınızı eritmeden geriye dönük test etmek, bunları
birkaç makinede çalıştırmak, işlemleri bir düzine hesaba yansıtmak ve siz uyurken bir yapay zekânın
riski gözetmesini istiyorsunuz. **Tam da doğru yerdesiniz.**

cMind, **cTrader için açık kaynaklı, kendi kendine barındırılabilen bir ticaret operasyonları
platformudur**. Onu tüm ticaret masanız gibi düşünün — yazım, yürütme, bir hesaplama filosu, kopya
işlem ve bir yapay zekâ çekirdeği — baştan sona size ait olan sakin, koyu, mobil dostu tek bir
uygulamada toplanmış.

:::tip[Tek cümlede]
cTrader stratejilerinizi kendi sunucularınızda, kendi markanız altında, yerleşik yapay zekâ ile ölçekte
oluşturun → geriye dönük test edin → çalıştırın → kopyalayın.
:::

## Gerçekte ne yapabilir?

| Şunu istiyorsunuz… | cMind bunu yapar | Devamını okuyun |
|---|---|---|
| Tarayıcıda bir cBot yazmak | Monaco IDE + C#/Python şablonları, sanal alan derlemeleri | [Oluşturma ve backtest](./features/build-and-backtest.md) |
| Makineler arası backtest | Kendini onaran düğüm filosu en az meşgul makineyi seçer | [Ölçekleme](./deployment/scaling.md) |
| Bir hesabı birçoğuna kopyalamak | Yeniden eşitlemeli sağlam yansıtma, çift işlem yok | [Kopya işlem](./features/copy-trading.md) |
| Ağır işi yapay zekâya bırakmak | Strateji üretimi, kendi kendini onarma, risk muhafızı, olay sonrası analiz | [Yapay zekâ çekirdeği](./features/ai.md) |
| Prop firma kurallarında kalmak | Canlı öz sermaye takibi + meydan okuma kuralı simülasyonu | [Prop firma](./features/prop-firm.md) |
| Backtest avantajını doğrulamak | PSR / DSR / t-istat aşırı uyum düzeltmesi | [Backtest Bütünlük Laboratuvarı](./features/backtest-integrity.md) |
| Kendi alışkanlıklarını anlamak | Davranışsal sızıntı tespiti + yapay zekâ koçu | [İşlem günlüğü](./features/trading-journal.md) |
| Bir strateji için makro olayları takip etmek | Zaman içinde doğru takvim, haber engeli, cBot API | [Ekonomik takvim](./features/economic-calendar.md) |
| Para birimi makro gücünü puanlamak | Tüm pariteler için yapay zekâ ileriye dönük görünüm | [Para birimi gücü](./features/currency-strength.md) |
| Hesapları 2FA ile güvence altına almak | TOTP kimlik doğrulama uygulaması + yedek kodlar | [İki faktörlü kimlik doğrulama](./features/two-factor-auth.md) |
| Sahiplerin çalışma zamanında ayarlamasına izin vermek | Her beyaz etiket seçeneği Ayarlar → Dağıtım'da canlı | [Sahip ayarları](./features/white-label-owner-settings.md) |
| Herhangi bir dilde çalıştırmak | RTL dahil 23 dil — eksik anahtar olduğunda derleme başarısız | [Yerelleştirme](./features/localization.md) |
| Onu *sizin* ürününüz olarak sunmak | Tam beyaz etiket: ad, renkler, logo, favicon | [Beyaz etiket](./features/white-label.md) |
| Telefonunuzda çalıştırmak | Kurulabilir, mobil öncelikli PWA | [PWA](./features/pwa.md) |
| Bir yapay zekâ istemcisinden sürmek | Yerleşik MCP sunucusu (HTTP + SSE) | [MCP](./features/mcp.md) |

## 5 dakikalık yol ⏱️

Docker'ınız ve beş dakikanız varsa, şu anda gerçek bir cMind örneğini kurcalıyor olabilirsiniz:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Ardından **<http://localhost:8080>** adresini açın, oturum açın ve başlayın. Tam kılavuz (Docker
kaçınılmaz olarak fikir belirttiğinde sorun giderme dahil) **[Yerel olarak çalıştırma](./deployment/local.md)**
sayfasında bulunur.

## Buraya yeni mi geldiniz? Sarı tuğlalı yolu izleyin 🟡

1. **[Bu kimin için?](./audience.md)** — bizim tarz bir baş belası olduğunuzdan emin olun.
2. **[Yerel olarak çalıştırma](./deployment/local.md)** — gerçek bir örnek ayağa kaldırın.
3. **[Özellikler](./features/README.md)** — içindekilerin tam turu.
4. **[Gerçek anlamda dağıtım](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Onu sizin yapın](./white-label-for-business.md)** — işletmeniz için beyaz etiket uygulayın.
6. **[Katkıda bulunun](./contributing.md)** — PR'ler (insan *ve* yapay zekâ destekli) çok memnuniyetle karşılanır.

## Para hakkında kısa bir söz 💸

cMind **gerçek sermayeyi** hareket ettirir. Bunu ciddiye alıyoruz — her değişiklik, hata yolları dahil
(kopan bağlantılar, reddedilen emirler, ölü düğümler) birim, entegrasyon ve uçtan uca testlerle birlikte
gönderilir. Siz de ciddiye almalısınız: **önce bir demo hesapta test edin** ve onu gerçek bir şeye
yöneltmeden önce [uyumluluk notlarını](./features/compliance.md) okuyun. Ticaret risklidir; bu yazılım
bir araçtır, finansal tavsiye değildir.

Pekâlâ — girişlik bu kadar yeter. Hadi bir şeyler inşa edelim. →
