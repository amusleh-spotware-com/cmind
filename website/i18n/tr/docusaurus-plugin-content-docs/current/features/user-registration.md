---
description: "Güvenli, white-label-kapılı self-servis kullanıcı kaydı — bir uygulama-içi kayıt sayfası ve bir sunucudan-sunucuya provizyon API'si, yapılandırılabilir kullanıcı öznitelikleri, yönetici-onayı veya e-posta-doğrulama kapılaması ve kötüye-kullanım-karşıtı korumalarla. Varsayılan olarak devre dışı."
---

# Kullanıcı kaydı

Varsayılan olarak **sahip/yönetici kullanıcıları manuel olarak ekler** (Users sayfası → *New User*). Kullanıcıları
ölçekte devreye almak — veya uygulamayı başka bir hizmetle entegre etmek — ihtiyacı olan white-label dağıtımlar için,
cMind ayrıca **güvenli, self-servis bir kayıt** yolu da gönderir. Bu, **varsayılan olarak devre dışıdır**: standart
bir dağıtım değişmez ve bir dağıtım tercih edene kadar hem sayfa hem de API 404 döndürür.

Tek bir domain akışını paylaşan iki giriş noktası vardır:

1. **Uygulama-içi sayfa** (`/register`) — `/login` ile aynı kabukta markalı, mobil-öncelikli bir kayıt sayfası.
2. **Provizyon API'si** (`POST /api/provision`) — entegre olan bir hizmetin hesap oluşturması için, dağıtım-başına
   bir provizyon gizli bilgisiyle kimliği doğrulanan sunucudan-sunucuya bir uç nokta.

## Neler kaydedilir — veri minimizasyonu

cMind bir ticaret **araç setidir**: cBot'ları oluşturur/çalıştırır/backtest eder ve trade'leri her kullanıcının *kendi*
cTrader Open API kimlik bilgileri üzerinden yansıtır. **Ticaret hesapları açmaz veya müşteri parasını saklamaz**,
bu nedenle KYC/AML kimlik doğrulaması **broker'ın** yükümlülüğüdür, bu platformun değil. Bu nedenle kayıt formu
varsayılan olarak **yalnızca bir e-posta** kaydeder — hizmeti sağlamak için gereken minimum (GDPR Madde 5(1)(c) veri
minimizasyonu; yasal dayanak = sözleşme). cMind kasıtlı olarak **hiçbir** ulusal-kimlik / doğum-tarihi /
adres alanı göndermez.

Diğer her öznitelik, `App:Registration:Attributes` aracılığıyla **dağıtım-başına isteğe bağlıdır**, her biri bağımsız olarak
`Off` / `Optional` / `Required`:

| Öznitelik | Notlar |
|---|---|
| `FullName`, `DisplayName`, `Company` | Serbest metin, uzunluk-sınırlı. |
| `Country` | ISO 3166-1 alpha-2, sabit bir kod setine karşı doğrulanır. |
| `Phone` | E.164 formatı (`+14155552671`). |
| `Locale` | BCP-47 şekli (`en-US`), normalleştirilir. |
| `MarketingOptIn` | Ayrı, **işaretsiz** onay kutusu — asla zorunlu onayla paketlenmez (CAN-SPAM). |
| `AgeConfirmation` | Yalnızca bir onay kutusu; **hiçbir** doğum tarihi saklanmaz. |

Öznitelikler, `AppUser` toplamına ait olan `UserProfile` değer nesnesinde bulunur, yapım sırasında
doğrulanır. **GDPR silme** (`AppUser.Anonymize()`), profili ve her türlü doğrulama token'ını temizler.

**Onay.** `RequireTermsAcceptance` açık olduğunda, kullanıcı yayınlanan yasal belgeleri kabul etmelidir
(Şartlar, Gizlilik, Risk Açıklaması). Kabul, mevcut `ConsentRecord` toplamı aracılığıyla kaydedilir —
sürüm-damgalı, zaman-damgalı, kaynak IP ile — MiFID/ESMA-derecesindeki kayıt tutma için başka yerde kullanılan
aynı depo.

## Kapılama modları

Self-kayıtlı bir hesap, kapısını (`App:Registration:Mode`) geçene kadar oturum açamaz:

- **`AdminApproval`** (varsayılan) — hesap kuyruğa alınır; bir sahip/yönetici onu **Users** sayfasında onaylar
  (*Pending approval* bölümü). Hiçbir posta altyapısı gerektirmez.
- **`EmailVerification`** — tek kullanımlık, süresi dolan bir doğrulama bağlantısı e-postalanır; bağlantı
  açıldığında hesap etkinleşir. Bir e-posta taşıması gerektirir (`App:Email`). **Hiçbir taşıma yapılandırılmamışsa, bu mod
  başlangıçta otomatik olarak `AdminApproval`'a düşer**, böylece kaydı etkinleştirmek asla sessizce bozulmaz.
- **`Open`** — hesap hemen etkindir (yalnızca güvenilir/geliştirici).

Self-kayıtlı kullanıcılar her zaman **`User`** olarak oluşturulur (veya yapılandırılmışsa `Viewer`) — domain,
self-kayıt yoluyla bir Owner/Admin oluşturmayı **kesinlikle reddeder**.

## Güvenlik ve kötüye-kullanım-karşıtı

- **Numaralandırma-karşıtı.** Yinelenen bir e-posta, yeni bir kayıtla **aynı** nötr `202 Accepted`'i verir ve
  hiçbir şey oluşturmaz — uygulama, bir adresin zaten bir hesabı olup olmadığını asla açıklamaz.
- **Hız sınırlama.** Genel uç noktalar IP başına kısıtlanır (auth sınırlayıcısından daha sıkı).
- **Parola politikası.** Minimum uzunluk uygulanır; parolalar hash'lenir (`IPasswordHasher` aracılığıyla Argon2);
  doğrulama token'ları yalnızca SHA-256 hash'leri olarak saklanır ve tek kullanımlık + süresi dolar.
- **E-posta hijyeni.** İsteğe bağlı e-posta domainleri izin-listesi ve tek-kullanımlık-sağlayıcı engelleme-listesi.
- **CAPTCHA (isteğe bağlı).** Paylaşılan doğrulama sözleşmeleri aracılığıyla reCAPTCHA / hCaptcha / Turnstile.
- **Giriş kapısı.** Bekleyen bir hesap, girişte nötr bir yanıtla reddedilir.

## Provizyon API'si (entegrasyon)

`App:Registration:Api:Enabled` ve bir `Secret` ayarlıyken, başka bir hizmet kullanıcı oluşturabilir:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Gizli bilgi sabit zamanda karşılaştırılır. Provizyonlanmış hesaplar, `Api.ActivateImmediately` /
`Api.InviteMustChangePassword`'a bağlı olarak **etkin** (veya `MustChangePassword` ile davet edilmiş) oluşturulur.

## Etkinleştirme

Kayıt **hem** özellik bayrağını **hem de** ana anahtarı gerektirir:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // or EmailVerification / Open
    "DefaultRole": "User",             // never Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

`App:Email` bölümü (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`), `EmailVerification` modunun kullandığı taşımayı yapılandırır; posta olmadan çalıştırmak için `Host`'u
ayarsız bırakın (no-op gönderici). Dağıtımların özellikleri nasıl açtığı ve yeniden markaladığı için
[özellik geçişleri](./feature-toggles.md) ve [white-label](./white-label.md)'a bakın. Kayıt etkinleştirildiğinde, giriş sayfası bir **Create
account** bağlantısı gösterir.

## Test edildi

Birim (profil doğrulaması, `SelfRegister` rol koruması, etkinleştirme geçişleri, tek kullanımlık token'lar, silme),
entegrasyon (varsayılan-kapalı 404, onay akışı, e-posta-doğrulama düşürme, numaralandırma-karşıtı, kötüye-kullanım
korumaları, gerekli öznitelikler, provizyon + kötü gizli bilgi) ve E2E (varsayılan-kapalı girişte kayıt bağlantısı yok;
`/register` sayfası markalı kapalı durumunu oluşturur).
