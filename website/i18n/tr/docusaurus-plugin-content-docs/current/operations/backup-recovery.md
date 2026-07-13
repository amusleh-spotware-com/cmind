---
description: "Bu bir ticaret/finansal uygulamadır: veritabanı ticaret hesapları, kopya profilleri, prop-firma zorlukları, denetim zincirleri ve Veri Koruma anahtar halkası tutar…"
---

# Yedekleme ve olağanüstü durum kurtarması

Bu bir ticaret/finansal uygulamadır: veritabanı ticaret hesapları, kopya profilleri, prop-firma
zorlukları, denetim zincirleri ve Veri Koruma anahtar halkası tutar. Bunu kaybetmek para kaybeder
ve düzenleyici/denetim yükümlülüklerini kırar. Yedekleyin ve **geri yüklemenin çalışması kanıtlayın**.

## Hedefler

| Metrik | Hedef | Anlam |
|--------|--------|---------|
| RPO (maksimum veri kaybı) | ≤ 5 dk | Sürekli WAL noktası-zamanında kurtarma kullanın, sadece günlük dökümler değil. |
| RTO (maksimum kesinti) | ≤ 1 h | Geri yükleme zamanı + uygulamayı geri yüklenen veri tabanına yeniden yönlendirin. |
| Yedekleme saklama | ≥ 35 gün | Geç keşfedilen yozlaşma + aylık denetim pencereleri kapsar. |
| Geri yükleme tatbikatı | aylık | Test edilmeyen bir yedekleme yedekleme değildir. |

## Yedeklenmiş şey

1. **Postgres veri tabanı** — tüm uygulama verisi (tek mantık veri tabanı `appdb`).
2. **Veri Koruma anahtar halkası** — **veritabanında** (`PersistKeysToDbContext<DataContext>`) kalıcı ve
   `App:DataProtectionCertBase64` aracılığıyla PFX şifreli. Veritabanı yedeklemesinde sürülür, **ancak
   koruma sertifikası ve şifresi (`App:DataProtectionCertPassword`) veritabanı dışında depolanan
   sırlar** — bunları sırlar yöneticinize yedekleyin. Sertifika olmadan geri yüklemeden sonra sırları
   (cTID şifreleri, Open API jetonları, düğüm sırları, AI anahtarı) şifre çözemezsiniz.

## Yönetilen Postgres (önerilen)

Her iki bulut IaC yolu, yerleşik PITR ile yönetilen Postgres sağlar — etkinleştirin + saklama doğrulayın:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): `backup.backupRetentionDays` (≥ 35) ve uyum
  gerektiğinde `geoRedundantBackup` ayarlayın. **Noktada zamanında geri yükleme** ile yeni sunucuya geri
  yükleyin, sonra uygulamanın `appdb` bağlantı dizesini güncelleyin.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): `backup_retention_period` (≥ 35) ve `backup_window`
  ayarlayın; otomatik yedeklemeleri + isteğe bağlı bölgeler arası kopya tutun. **RestoreDBInstanceToPointInTime**
  ile geri yükleyin, sonra uygulamayı yeniden yönlendirin.

Yönetilen PITR, uygulama değişiklikleri olmadan ≤ 5 dk RPO verir — uygulama sadece yeni bağlantı
dizesine ihtiyaç duyar (ve var olan yenileme yürütme stratejisi, bkz. [scaling.md](../deployment/scaling.md),
değiştirme titreşimini tolere eder).

## Kendi kendini barındıran Postgres

- **Sürekli arşivleme (PITR):** WAL arşivlemesini (`archive_mode=on`, `archive_command` nesne depolamaya)
  + periyodik `pg_basebackup` etkinleştirin. Geri yükleme = temel yedeklemesini geri yükle + WAL'ı hedef
  saate oynat. Bu RPO hedefini karşılayan şeydir.
- **Mantık dökümü (ikincil):** taşınabilirlik için her gece `pg_dump -Fc appdb` kutu depolamaya /
