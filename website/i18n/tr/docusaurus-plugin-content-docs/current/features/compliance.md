---
description: "Perakende FX/CFD/kripto brokerleri yasal + kayıt tutma görevlerine taşıyın. Modül dört endüstri standart sütununun uygulanması: risk açıklaması onayı…"
---

# Yasal ve uyum

Perakende FX/CFD/kripto brokerleri yasal + kayıt tutma görevlerine taşıyın. Modül dört endüstri standart sütununun uygulanması: **risk açıklaması rızası**, **kurcalama kanıtı denetim izi**, **MiFID/ESMA stil kayıt tutma**, **GDPR veri hakları**. Tümü `Compliance` özellik bayrağı ile kapılı.

## 1. Sürümlü yasal belgeler + rıza

- `LegalDocument` (aggregate) — sürümlü Hizmet Koşulları, CFD **Risk Açıklaması** veya Gizlilik Politikası. Sürüm taslağı, ardından **yayımlandı**; yayımlanmış sürümler **değişmez** (düzenle atma), bu nedenle kullanıcının rıza gösterdiği tam metin her zaman kurtarılabilir. Bir tür için aktif belge = en yüksek yayımlanmış sürümü.
- `ConsentRecord` (aggregate) — kullanıcının belirli belge sürümünü bir saatte kabul ettiğinin değişmez kaydı, menşei IP ile.
- **Yürütme:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` bloğu eylemi `403` ile, o tür yayımlanmış belge mevcut ve kullanıcı aktif sürüme rıza göstermedi. **Kopya profili oluşturmaya** uygulanır (`RiskDisclosure`). Hiçbir şey yayımlanmadı → eylemler izin verildi — henüz rıza için hiçbir şey yok — bu nedenle modülü etkinleştirme hiçbir şey geriye çevrilmiş olarak engellemiyor, sonra açıklamada yayımlanacak.

## 2. Kurcalama kanıtı denetim izi

`AuditLog` girdileri karma zincir: her satır `PrevHash` ve `Hash = SHA-256(prev | kanonik alanlar)` depolar. `AuditChainInterceptor` zincirleme şeffaf bir şekilde `SaveChanges` da uygulanır, bu nedenle mevcut denetim çağrı siteleri değişmez. `IAuditTrailVerifier.VerifyAsync` zincir yeniden yürütmek, depolanan karma veya geri bağlantı artık eşleşmediği ilk satırı bildir — geçmiş kaydın düzenlemesini veya silinmesini algılayın. Sahibi uç noktası: `GET /api/compliance/audit/verify`.

## 3. Kayıt tutma (MiFID II / ESMA RTS)

Kayıt tutma **değişmez, karma zincir denetim günlüğü** artı **tutulmuş rıza kayıtları** ve yumuşak silinen (asla sert silinen değil) etki alanı kayıtları ile sağlanır. UTC zaman damgaları enjekte `TimeProvider` dan. Rıza kayıtları belge sürümü + IP tutun; yayımlanmış yasal belgeler asla mutasyon. Tutma = bu tabloları temizleme değil (yalnızca ekle / yumuşak silme).

## 4. GDPR veri hakları

- `GET /api/compliance/export` — arayanın verilerinin makine tarafından okunabilir dışa aktarımı (profil, rizalar, kopya profilleri, prop-firm zorlukları).
- `POST /api/compliance/erase` — silme hakkı: `AppUser.Anonymize()` PII (e-posta, MFA) kaşırır ve satır yumuşak silindi, referansiyel/denetim geçmişi uyumlu tutarak.

## API özeti

| Yöntem | Rota | Rol | Amaç |
|--------|------|-----|---------|
| GET | `/api/compliance/documents/active` | User+ | aktif yayımlanmış belgeler |
| GET | `/api/compliance/consent/status` | User+ | hangi rizalar beklemede |
| POST | `/api/compliance/consent` | User+ | belgenin aktif sürümünü kabul et |
| GET | `/api/compliance/export` | User+ | GDPR veri dışa aktarımı |
| POST | `/api/compliance/erase` | User+ | kendi hesabın GDPR silinmesi |
| POST | `/api/compliance/documents` | Sahibi | belge tasla |
| POST | `/api/compliance/documents/{id}/publish` | Sahibi | sürüm yayımla |
| GET | `/api/compliance/audit/verify` | Sahibi | denetim karma zincirini doğrula |

UI: `/settings/legal` (nav *Ayarlar → Yasal ve Gizlilik*, `Compliance` ile kapılı) olağanüstü sözleşmeleri kabul düğmeleriyle + GDPR dışa aktarım/silme eylemlerileriyle gösterir.

## Testler

- **Birim** — `UnitTests/Compliance/LegalDocumentTests.cs` (taslak/yayımla/değişmezlik, rıza yakalama), `AuditChainTests.cs` (karma bağlantılar, kurcalama tespiti, içerik duyarlılığı).
- **Entegrasyon** — `IntegrationTests/CompliancePersistenceTests.cs` (aktif sürüm + rıza sorguları gerçek Postgres), `AuditChainIntegrityTests.cs` (zincir bozunmamış doğrula, ardından SQL seviyesi kurcalama algılayın), `ComplianceFlowTests.cs` (WebApplicationFactory, izole DB: rıza kapı kopya oluşturmayı engelle, risk açıklaması kabul edilene; GDPR dışa aktarımı; denetim doğrula).
- **E2E** — `E2ETests/ComplianceTests.cs`: Yasal ve Gizlilik sayfası oluşturulur ve GDPR dışa aktarımı gerçek tarayıcıda kullanıcı verisi döndürür.
