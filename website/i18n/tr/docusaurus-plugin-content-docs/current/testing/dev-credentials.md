---
description: "Test paketlerinin ihtiyaç duyduğu tüm kimlik bilgileri tek bir gitignore'lu dosyada yaşar: secrets/dev-credentials.local.json. İşlenen şablonu kopyalayın ve elinizdekini doldurun"
---

# Geliştirici kimlik bilgileri — her test için tek dosya

Test paketlerinin ihtiyaç duyduğu tüm kimlik bilgileri tek bir gitignore'lu dosyada yaşar:
`secrets/dev-credentials.local.json`. İşlenen şablonu kopyalayın ve elinizdekini doldurun — her değer
isteğe bağlıdır ve eksik bir değere ihtiyaç duyan testler temiz biçimde atlanır.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# secrets/dev-credentials.local.json dosyasını düzenleyin
```

## Her test katmanı neyi okur

| Katman | İhtiyaç | Kaynak |
|------|-------|------|
| **Birim** (`tests/UnitTests`) | hiçbir şey | — deterministik, sır yok, ağ yok |
| **Entegrasyon** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — otomatik |
| **Canlı kopya** (`tests/IntegrationTests/CopyLive`) | OpenAPI uygulaması + belirteç önbelleği | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E katılım** (`tests/E2ETests/CopyLive`) | OpenAPI uygulaması + cID girişleri | `OpenApi.App`, `OpenApi.Cids` |
| **E2E gerçek çalıştırma/backtest** (`CBotRealRunBacktestTests`) | bir cID girişi + bir **demo** hesap numarası | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI özellikleri** | Anthropic anahtarı | `Ai.ApiKey` (ayarlanmazsa ⇒ AI özellikleri devre dışı döner, uygulama yine çalışır) |

## Şema

Depo kökündeki `dev-credentials.example.json`'a bakın. Bölümler:

- `OpenApi.App` — cTrader Open API uygulamasının `{ ClientId, ClientSecret }`'i.
- `OpenApi.Cids` — başsız OAuth katılımı tarafından kullanılan cTrader ID girişleri. Her giriş ayrıca bir
  **`Accounts`** dizisi taşır — o cID altındaki, test altyapısının uygulamaya bağlamasına ve sürmesine izin
  verilen cTrader işlem-hesabı numaraları (giriş/hesap numarası, örn. `3635817`). `CBotRealRunBacktestTests`,
  boş olmayan bir `Accounts` dizisine sahip ilk girişi okur, o cID + hesabı uygulamaya ekler, ardından
  üzerinde gerçekten bir cBot çalıştırır ve backtest eder. **Buraya yalnızca demo hesap numaraları koyun** —
  asla canlı bir hesap; çalıştırma/backtest testleri listelediğiniz hesapta gerçek emirler verir.
  Boş/atlanmış `Accounts` ⇒ gerçek çalıştırma/backtest testi temiz biçimde atlanır.
- `OpenApi.Tokens` — çoklu-cID belirteç önbelleği (yetkilendirilmiş her cID için yenileme/erişim belirteci +
  hesap listesiyle bir giriş). Katılım ve belirteç-yenileme adımı tarafından otomatik yazılır; nadiren elle
  düzenlersiniz.
- `Owner` — E2E altında uygulama için tohum sahip girişi.
- `Database.ConnectionString` — yalnızca testleri Testcontainers yerine harici bir Postgres'e yönlendirirken.
- `Ai.ApiKey` — AI özellikleri için Anthropic API anahtarı.

## Öncelik

1. **Ortam değişkenleri** her şeyi geçersiz kılar (örn. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — birleşik dosya (tercih edilen).
3. **Eski bölünmüş dosyalar** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` birleşik dosya yokken hâlâ okunur, böylece mevcut makineler çalışmaya devam
   eder. Yeni kurulumlar tek dosyayı kullanmalıdır.

## Güvenlik

- `secrets/` ve `*.local.json` gitignore'ludur — buradaki hiçbir şey asla işlenmez.
- Canlı kopya testleri demo-olmayan hesaplara karşı çalışmayı reddeder (`IsLive` hesaplar `LiveCopyFixture`
  tarafından filtrelenir). Belirteç önbelleğinde yalnızca demo hesaplar tutun.
- Küme-içi (Kubernetes) çalıştırmalar dosyayı salt-okunur bir Secret olarak bağlar; belirteç yenilemeleri
  bellekte tutulur ve salt-okunur geri-yazma sessiz bir no-op'tur.
