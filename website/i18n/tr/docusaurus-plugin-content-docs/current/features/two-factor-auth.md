---
description: "Kimlik doğrulayıcı-uygulama kaydı, tek-kullanımlık yedek kodlar ve tüm kullanıcılar için zorunlu kılan bir white-label anahtarıyla isteğe bağlı TOTP iki-faktörlü kimlik doğrulama."
---

# İki-faktörlü kimlik doğrulama (2FA)

Hesaplar, parolanın üstünde **zaman-tabanlı tek-kullanımlık parola (TOTP)** iki-faktörlü kimlik doğrulamayla
korunabilir. Varsayılan olarak kullanıcının profilinden **isteğe bağlıdır** ve bir white-label dağıtımı onu
herkes için **zorunlu** kılabilir. Herhangi bir RFC 6238 kimlik doğrulayıcı uygulaması çalışır — Google
Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — çünkü uygulama standarttır (SHA-1, 6 hane,
30-saniye adım); tescilli bir sunucu bileşeni yer almaz.

## Nasıl çalışır

- **Alan.** MFA, `AppUser` toplamında (Access bağlamı) yaşar. Bir kullanıcı, niyet-ortaya-çıkaran yöntemlerle
  kaydedilir — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`,
  `DisableMfa` — böylece değişmezler (bir sır etkinleşmeden önce onaylanmalıdır; bir yedek kod tek-kullanımlıktır)
  tek bir yerde zorunlu kılınır.
- **TOTP.** Üretim ve doğrulama, Infrastructure'da **Otp.NET** kütüphanesiyle uygulanan Core
  `ITotpAuthenticator` arayüzünün arkasında yer alır. Doğrulama ±1 zaman-adımı saat sapmasına tolerans gösterir.
- **Durağan sır.** Kimlik doğrulayıcı sırrı, `ISecretProtector` (`EncryptionPurposes.MfaSecret`) aracılığıyla
  **şifreli** saklanır — asla düz metin olarak değil.
- **Yedek kodlar.** Kayıtta on tek-kullanımlık kurtarma kodu verilir, **bir kez** gösterilir ve yalnızca
  SHA-256 karmaları olarak saklanır (`MfaBackupCodes`). Her biri tam olarak bir kez çalışır; harcanan bir kod
  sonrasında reddedilir.

## Etkinleştirme (profil)

**Account** sayfasında (`/account`) *Two-factor authentication* bölümü mevcut durumu gösterir:

1. **Enable two-factor**, bir **QR kodu** (`Net.Codecrete.QrCodeGenerator` aracılığıyla sunucu-tarafında SVG
   olarak oluşturulmuş) artı manuel kurulum anahtarıyla bir MudBlazor iletişim kutusu açar.
2. Onu tarayın, onaylamak için 6-haneli kodu girin — bu, etkinleştirmeden önce bekleyen sırrı doğrular.
3. İletişim kutusu ardından **yedek kodları** gösterir; onları kaydedin. 2FA artık açık.

Aynı bölüm, kayıtlı bir kullanıcının **yedek kodları yeniden oluşturmasına** veya 2FA'yı **kapatmasına** izin
verir — her ikisi de onaylamak için hesap parolasını gerektirir.

## 2FA ile oturum açma

2FA etkinleştirildiğinde giriş **iki-adımlı** bir akıştır:

1. **Parola adımı** (`POST /api/auth/login`). Başarıda kimlik çerezi **henüz** verilmez; bunun yerine
   kısa-ömürlü (5-dakika), şifreli bir *bekleyen* çerez ayarlanır ve kullanıcı `/login/2fa`'ya gönderilir.
2. **Meydan okuma adımı** (`POST /api/auth/login/verify-2fa`). Kullanıcı bir TOTP kodu **veya** kullanılmamış
   herhangi bir yedek kodu girer. Başarıda bekleyen çerez düşürülür ve gerçek kimlik çerezi verilir.

Başarısız ikinci-faktör denemeleri mevcut hesap **kilitlenmesine** (`AuthLockout`) sayılır ve kimlik uç
noktaları hız-limitlidir.

## Bir white-label dağıtımı için zorunlu 2FA

Düzenlenmiş bir satıcı, **her** hesap için 2FA gerektirebilir:

```jsonc
// appsettings / ortam
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

`RequireMfa` açıkken ve 2FA'sız bir kullanıcı giriş yaptığında, parola adımı `mfaSetupRequired` raporlar ve
`MfaEnforcementMiddleware`, kayıt tamamlanana kadar sayfa gezinmelerini `/account`'a yönlendirir. Varsayılan
`false`'tur, böylece yapılandırılmamış bir dağıtım 2FA'yı isteğe bağlı tutar. Bkz. [White-label](white-label.md).

## Uç noktalar

| Yöntem ve rota | Amaç |
| --- | --- |
| `POST /api/auth/login` | Parola adımı; `mfaRequired` (meydan okuma) döndürür veya giriş yapar |
| `POST /api/auth/login/verify-2fa` | İkinci-faktör adımı (TOTP veya yedek kod) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, bekleyen, kalan yedek-kod sayısı |
| `POST /api/auth/mfa/setup` | Kaydı başlat — sır, `otpauth://` URI, QR SVG döndürür |
| `POST /api/auth/mfa/confirm` | Bir kodu onayla, etkinleştir, yedek kodları döndür |
| `POST /api/auth/mfa/disable` | Kapat (parola-onaylı) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Yeni bir set ver (parola-onaylı) |

## Testler

- **Birim** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vektörleri),
  `AppUserMfaTests.cs` (kayıt/geçiş/tek-kullanım değişmezleri), `MfaBackupCodesTests.cs`.
- **Entegrasyon** — `IntegrationTests/MfaPersistenceTests.cs` (kayıt → onayla → tüket, basamaklı silme) ve
  `MfaFlowTests.cs` (TOTP + yedek kod ile tam HTTP iki-adımlı giriş ve zorunlu-kayıt kapısı).
- **E2E** — `E2ETests/MfaFlowTests.cs`: profilden etkinleştir (QR + onayla + yedek kodlar) ve masaüstü ve mobil
  görünüm alanlarında meydan-okunmuş bir girişi tamamla.
