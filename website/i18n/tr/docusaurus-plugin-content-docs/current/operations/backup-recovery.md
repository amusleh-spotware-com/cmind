---
description: "Bu bir alım-satım/finans uygulamasıdır: veritabanı alım-satım hesaplarını, kopyalama profillerini, prop-firm mücadelelerini, denetim zincirlerini ve Data Protection anahtar halkasını tutar…"
---

# Yedekleme ve olağanüstü durum kurtarma

Bu bir alım-satım/finans uygulamasıdır: veritabanı alım-satım hesaplarını, kopyalama profillerini,
prop-firm mücadelelerini, denetim zincirlerini ve Data Protection anahtar halkasını tutar. Onu
kaybetmek para kaybettirir ve düzenleyici/denetim yükümlülüklerini ihlal eder. Yedekleyin ve
**geri yüklemenin çalıştığını kanıtlayın**.

## Hedefler

| Metrik | Hedef | Anlamı |
|--------|--------|---------|
| RPO (maksimum veri kaybı) | ≤ 5 dk | Yalnızca gecelik dökümler değil, zaman-noktası kurtarma (sürekli WAL) kullanın. |
| RTO (maksimum kesinti) | ≤ 1 sa | Geri yükleme + uygulamayı geri yüklenen veritabanına yeniden yönlendirme süresi. |
| Yedek saklama | ≥ 35 gün | Geç fark edilen bir bozulmayı + aylık denetim pencerelerini kapsar. |
| Geri yükleme tatbikatı | aylık | Test edilmemiş bir yedek, yedek değildir. |

## Neler yedeklenmelidir

1. **Postgres veritabanı** — tüm uygulama verileri (tek mantıksal veritabanı `appdb`).
2. **Data Protection anahtar halkası** — veritabanı **içinde** kalıcılaştırılır
   (`PersistKeysToDbContext<DataContext>`) ve `App:DataProtectionCertBase64` aracılığıyla PFX ile
   şifrelenir. DB yedeğiyle birlikte gelir, **ancak koruyucu sertifika + parolası
   (`App:DataProtectionCertPassword`) DB dışında saklanan gizli değerlerdir** — bunları gizli-değer
   yöneticinizde yedekleyin. Sertifika olmadan bir geri yüklemeden sonra gizli değerleri (cTID
   parolaları, Open API belirteçleri, düğüm gizli değerleri, AI anahtarı) çözemezsiniz.

## Yönetilen Postgres (önerilir)

Her iki bulut IaC yolu da yerleşik PITR ile yönetilen Postgres sağlar — saklamayı etkinleştirin +
doğrulayın:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): `backup.backupRetentionDays`'i (≥ 35) ve
  uyumluluğun gerektirdiği yerde `geoRedundantBackup`'ı ayarlayın. Yeni bir sunucuya *Point-in-time
  restore* ile geri yükleyin, ardından uygulamanın `appdb` bağlantı dizesini güncelleyin.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): `backup_retention_period`'i (≥ 35) ve
  `backup_window`'u ayarlayın; otomatik yedekleri + isteğe bağlı bölgeler-arası kopyayı koruyun.
  *RestoreDBInstanceToPointInTime* ile geri yükleyin, ardından uygulamayı yeniden yönlendirin.

Yönetilen PITR, uygulamada değişiklik yapmadan ≤ 5 dk RPO sağlar — uygulamanın yalnızca yeni bağlantı
dizesine ihtiyacı vardır (ve mevcut yeniden-deneyen yürütme stratejisi, bkz.
[scaling.md](../deployment/scaling.md), geçiş kesintisini tolere eder).

## Kendi sunucunuzda barındırılan Postgres

- **Sürekli arşivleme (PITR):** WAL arşivlemeyi (`archive_mode=on`, nesne depolamasına
  `archive_command`) + düzenli bir `pg_basebackup` etkinleştirin. Geri yükleme = temel yedeği geri
  yükle + WAL'ı hedef zamana kadar yeniden oynat. RPO hedefini karşılayan budur.
- **Mantıksal dökümler (ikincil):** taşınabilirlik / kısmi geri yüklemeler için gecelik
  `pg_dump -Fc appdb`'yi ana makine dışı depolamaya. RPO hedefi için tek başına yeterli değildir.
- Yedekleri beklemedeyken şifreleyin; veritabanı ana makinesinin dışında saklayın.

## Geri yükleme tatbikatı (aylık çalıştırın)

1. En son yedeği ("şimdi − 10 dk" için PITR) üretime değil, bir **karalama** veritabanına geri
   yükleyin.
2. Tek kullanımlık bir uygulama örneğini (veya bir psql oturumunu) ona yönlendirin.
3. Şemayı doğrulayın: `dotnet ef migrations list` bekleyen taşıma göstermez, uygulama başlar ve
   `/health`-hazır hale gelir.
4. `IAuditTrailVerifier` aracılığıyla **denetim zincirinin** bozulmadan ve kesintisiz olduğunu
   **doğrulayın** (kurcalamaya-duyarlı `AuditChainInterceptor` zinciri) — geri yüklemeden sonra bozuk
   bir zincir, bozulma veya kurcalama anlamına gelir.
5. Gizli-değer çözümünün çalıştığını doğrulayın (örn. bir Open API yetkilendirmesi çözülür) — Data
   Protection sertifikasının + parolasının doğru geri yüklendiğini kanıtlar.
6. Tatbikat sonucunu kaydedin (RTO'ya karşı geçen süre) ve karalama veritabanını yok edin.

Ortamın izin verdiği yerde 1–4. adımları CI'da otomatikleştirin (tohumlanmış bir yedeği bir
Testcontainer'a geri yükleyin, `dotnet ef migrations list` + denetim-zinciri doğrulamasını çalıştırın)
böylece bozuk-yedek gerilemesi ona ihtiyaç duymadan önce yakalanır.

## Gerçek bir geri yüklemeden sonra

1. DB'yi geri yükleyin (olaydan hemen önceye PITR).
2. Data Protection sertifikasının + parolasının, olaydan önce kullanımda olanlarla **aynı** olduğundan
   emin olun.
3. Uygulamanın `appdb` bağlantı dizesini yeniden yönlendirin; replikaları döndürün.
4. Başlangıç, tavsiye kilidi altında taşımaları çalıştırır (bkz. scaling.md) — N replika ile
   güvenlidir.
5. Kopyalama/prop-firm süpervizörleri kiralamalarını geri alır ve **broker'dan yeniden senkronize
   eder** (cTrader doğruluk kaynağıdır), böylece açık pozisyonlar otomatik olarak yeniden yakınsar —
   bayat yerel durumdan hiçbir şeye güvenilmez.
6. Denetim zincirini doğrulayın + son alım-satım verilerini nokta-kontrol edin.
