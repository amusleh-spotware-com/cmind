---
description: "Kopya stratejilerinin göz atılabilir dizini. Sağlayıcı, kopya profilini doğrulanmış-canlı rozetiyle (strateji kaynak hesabı gerçek para işler, demo değil) bir liste olarak yayınlar…"
---

# Kopya sağlayıcı pazarı (Faz 4)

Kopya stratejilerinin göz atılabilir dizini. Sağlayıcı, kopya profilini **doğrulanmış-canlı** rozetiyle
(strateji kaynak hesabı gerçek para işler, demo değil) artı performans ücretiyle bir liste olarak
**yayınlar**. Takipçiler, yürütme-şeffaflığı verilerinden öngörülen performans puanına göre sıralanmış
pazara göz atar.

## Model

- `CopyProviderListing` = toplam: `UserId`, `ProfileId`, görünen ad, açıklama, performans ücreti,
  `VerifiedLive`, `Published` + `PublishedAt`. Profil başına bir liste (benzersiz indeks).
- **Doğrulanmış-canlı**, yayın zamanında profil kaynağı `TradingAccount.IsLive`'dan türetilir — sağlayıcı
  kendini onaylayamaz.
- Performans istatistikleri **listede saklanmaz** — `CopyExecution` şeffaflık günlüğü üzerinden okuma-modeli
  projeksiyonu (dolum oranı, ort. gecikme, ort. gerçekleşen kayma), böylece pazar her zaman canlı yürütme
  kalitesini yansıtır.

## Sıralama

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 puan:
dolum oranı baskındır (×60), düşük gecikme + düşük kayma ekler (her biri ×20), doğrulanmış-canlı rozeti küçük
bir güven bonusu ekler. Deterministik + monotonik, böylece sıralama kararlıdır.

## API

- `POST /api/copy/profiles/{id}/publish` — profil listesini yayınla/güncelle (`DisplayName`, `Description`,
  `PerformanceFeePercent`); doğrulanmış-canlı kaynak hesaptan ayarlanır.
- `DELETE /api/copy/profiles/{id}/publish` — yayından kaldır.
- `GET /api/copy/marketplace` — yayınlanan tüm listeler, sıralanmış, her biri performans özetiyle
  (yürütmeler, dolum oranı, ort. gecikme, ort. kayma, puan) + doğrulanmış-canlı rozeti.

## Testler

- **Birim** (`CopyProviderListingTests`) — toplam değişmezleri: görünen ad gerekli; yayın zaman damgası
  ayarlar; yayından kaldırma gizler; güncelleme görünen alanları + ücreti + rozeti değiştirir.
- **Entegrasyon** (`CopyMarketplaceTests`, gerçek Postgres) — yayınlanan liste rozetle kalıcılaşır; profil
  başına bir liste (benzersiz indeks); sıralama puanı doğrulanmış/yüksek-dolumlu sağlayıcıları tercih eder.

Kopya host'u değişmez (yalnızca listeler + okuma modeli), böylece kopya DST stres paketi etkilenmez.
