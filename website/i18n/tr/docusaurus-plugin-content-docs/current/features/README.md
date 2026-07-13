---
title: Özellikler
sidebar_position: 1
---

# cMind Özellikleri

cMind'in temel yetenekleri — ne yapabilir, nasıl çalışır, nerede öğrenmek gerekirse.

## Çekirdek

- **[Derle & Backtest](./build-and-backtest.md)** — Monaco IDE'de C# ve Python; docker'da tahmin edilebilir backtesting
- **[cBot API](./calendar-cbot-api.md)** — Ekonomik takvim verilerine erişim
- **[Kopyalama Ticareti](./copy-trading.md)** — Kaynak hesaplarından canlı yansıtma, token rotasyonu, resync

## AI & Otomasyon

- **[AI Çekirdeği](./ai.md)** — Prompt → cBot kodu, parameter tuning, backtest analizi, risk koruma
- **[AI Kopyalama Tavsiyesi](./ai-copy-recommender.md)** — AI destekli kopya profili seçimi
- **[Ajan Stüdyosu](./agent-studio.md)** — Uzun akış görevleri ve ajanlar

## Analiz & Kontrol

- **[Pano](./dashboard.md)** — Gerçek zamanlı KPI'lar, grafikler, ticaret beslemesi
- **[Ticaret Günlüğü](./trading-journal.md)** — Trade post-mortem ve P&L analizi
- **[Yürütme TCA](./execution-tca.md)** — Kaymalar, gecikmeler, sipariş kalitesi ölçümü
- **[Strateji Sağlığı](./strategy-health.md)** — Canlı bot performansı ve anomali tespiti
- **[Rejim Laboratuvarı](./regime-lab.md)** — Pazar rejimi analizi ve adaptif ticaret

## İşletme & Yönetim

- **[Prop-Firm Kuralları](./prop-firm.md)** — Finansmanlı tüccar zorlukları, canlı sermaye izleme
- **[Uyum](./compliance.md)** — Denetim günlüğü, 2FA, uyum saklı tutma
- **[Beyaz Etiketli](./white-label.md)** — Markalamayı tam kontrol: ad, logo, renkler, alan adı
- **[Beyaz Etiketli Sahip Ayarları](./white-label-owner-settings.md)** — Sahip, dağıtım ayarlarını çalışma zamanında ayarlayabilir

## Entegrasyon & Uzantı

- **[Açık API Paylaşılan Uygulama](./open-api-shared-app.md)** — Tek cTrader OAuth uygulaması tüm kullanıcılar için
- **[Ortak Ağ Protokolü](./mcp.md)** — AI istemcileriyle cMind araçlarını ifşa edin
- **[Özellik Geçiş Anahtarları](./feature-toggles.md)** — Her dağıtımda yetenekleri etkinleştir/devre dışı bırak

## Veri & Gözlemlenebilirlik

- **[Ekonomik Takvim](./economic-calendar.md)** — Haberler ve göstergeler, cBot tetikleme
- **[Para Gücü](./currency-strength.md)** — AI makro analizi, çift görünümü
- **[Konumlandırma](./position-sizing.md)** — Risk kararları ve toplu boyutlandırma
- **[İki Faktörlü Kimlik Doğrulama](./two-factor-auth.md)** — TOTP, yedek kodlar, zorunlu moda

## Kullanıcı Deneyimi

- **[PWA](./pwa.md)** — Yüklenebilir, çevrimdışı-yetkin mobil uygulama
- **[Yerelleştirme](./localization.md)** — 23 dil, RTL destek
- **[Kullanıcı Kaydı](./user-registration.md)** — Kendi barındırılan kaydolma, e-posta doğrulama

## Kopyalama Ticareti Derinliği

- **[Kopyalama Uygulaması Saydamlığı](./copy-execution-transparency.md)** — Eşleştirme, tahsisler, kayma günlüğü
- **[Kopyalama Bildirimleri](./copy-notifications.md)** — Profil durum güncellemeleri ve uyarıları
- **[Kopyalama Performans Ücretleri](./copy-performance-fees.md)** — Kazaç payları ve uzlaşmalar
- **[Kopyalama Sağlayıcı Pazarı](./copy-provider-marketplace.md)** — Keşif ve seçim
- **[İçgüdüsel Konumlandırma](./contrarian-positioning.md)** — Karşı ticaret stratejisi

## Test & Doğrulama

- **[Backtest Bütünlüğü](./backtest-integrity.md)** — Kaçma olmadan, fair kilitli tahminler
- **[Token Yaşam Döngüsü](./token-lifecycle.md)** — Yenileme, rotasyon, süresi dolan token yönetimi
- **[Kopyalama Ticareti Doğrulama Çalışması](../testing/copy-trading-verification-run.md)** — Kod değişiklikleri test etme
- **[Stres Testi](../testing/stress-testing.md)** — Belirlenmişken ölçek ve kayıp yönetimi

Özellik geçiş anahtarlarıyla olanları ve olmayan olanları etkinleştirin → [Feature toggles](./feature-toggles.md).
